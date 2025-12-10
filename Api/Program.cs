using Api.Services;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using StackExchange.Redis;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

var builder = WebApplication.CreateBuilder(args);

// =====================
// Configure FusionCache + Redis
// =====================
builder.Services
    .AddFusionCache()
    .WithDistributedCache(_ =>
    {
        var host = builder.Configuration["Redis:Host"]
            ?? Environment.GetEnvironmentVariable("REDIS_HOST")
            ?? throw new InvalidOperationException("Redis:Host is not configured");

        var portStr = builder.Configuration["Redis:Port"]
            ?? Environment.GetEnvironmentVariable("REDIS_PORT");
        var port = portStr != null ? int.Parse(portStr)
            : throw new InvalidOperationException("Redis:Port is not configured");

        var user = builder.Configuration["Redis:User"]
            ?? Environment.GetEnvironmentVariable("REDIS_USER")
            ?? throw new InvalidOperationException("Redis:User is not configured");

        var password = builder.Configuration["Redis:Password"]
            ?? Environment.GetEnvironmentVariable("REDIS_PASSWORD")
            ?? throw new InvalidOperationException("Redis:Password is not configured");

        // Create Redis connection
        // Azure Cache for Redis uses SSL/TLS on port 6380
        var configOptions = new ConfigurationOptions
        {
            EndPoints = { { host, port } },
            User = user,
            Password = password,
            // Don't abort on connect fail - allow lazy connection
            AbortOnConnectFail = false,
            // Allow reconnection
            ReconnectRetryPolicy = new ExponentialRetry(1000, 10000),
            // Connect timeout
            ConnectTimeout = 5000,
            // Async timeout
            AsyncTimeout = 5000
        };
        
        // Enable SSL for Azure Cache for Redis (port 6380)
        // For local Redis (port 6379), SSL is usually not needed
        if (port == 6380 || host.Contains("redis.cache.windows.net"))
        {
            configOptions.Ssl = true;
        }
        
        // Create Redis connection with lazy initialization
        // Connection will be established on first use, not during service registration
        // This allows the app to start even if Redis is temporarily unavailable
        ConnectionMultiplexer muxer;
        try
        {
            // Try to connect, but don't block if it fails
            // With AbortOnConnectFail = false, connection will retry in background
            muxer = ConnectionMultiplexer.Connect(configOptions);
            
            // Log connection status (but don't wait for it to be ready)
            if (!muxer.IsConnected)
            {
                Console.WriteLine($"[Redis] Connection established but not yet ready. Will retry automatically.");
            }
        }
        catch (Exception ex)
        {
            // Log warning but don't fail startup
            // Connection will be retried automatically due to ReconnectRetryPolicy
            Console.WriteLine($"[Redis] Warning: Failed to establish initial Redis connection: {ex.Message}. " +
                             "Application will start, but cache operations may fail until connection is established.");
            
            // Still create the multiplexer - it will retry in the background
            muxer = ConnectionMultiplexer.Connect(configOptions);
        }

        var options = new RedisCacheOptions
        {
            ConnectionMultiplexerFactory = () => Task.FromResult<IConnectionMultiplexer>(muxer),
            InstanceName = "book-app"
        };

        return new RedisCache(options);
    })
    .WithSerializer(new FusionCacheSystemTextJsonSerializer());

// =====================
// Controllers + JSON/XML
// =====================
builder.Services.AddControllers(options =>
{
    options.RespectBrowserAcceptHeader = true;
})
    .AddXmlSerializerFormatters()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
    });

// =====================
// Swagger
// =====================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// =====================
// Cassandra singleton - use lazy initialization to avoid blocking startup
// =====================
builder.Services.AddSingleton<CassandraService>(serviceProvider =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var logger = serviceProvider.GetRequiredService<ILogger<CassandraService>>();
    return new CassandraService(configuration, logger);
});

var app = builder.Build();

// =====================
// Middleware
// =====================

// Add exception handling middleware FIRST to catch all exceptions
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";
        
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        var exceptionHandlerPathFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerPathFeature>();
        var exception = exceptionHandlerPathFeature?.Error;
        
        if (exception != null)
        {
            logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);
            
            var response = new
            {
                error = "An error occurred while processing your request.",
                message = app.Environment.IsDevelopment() ? exception.Message : "Internal Server Error",
                traceId = context.TraceIdentifier
            };
            
            await context.Response.WriteAsJsonAsync(response);
        }
    });
});

// Add logging middleware
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    var startTime = DateTime.UtcNow;
    
    logger.LogInformation("→ {Method} {Path}", context.Request.Method, context.Request.Path);
    
    try
    {
        await next();
        var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
        logger.LogInformation("← {Method} {Path} {StatusCode} {ElapsedMs}ms", 
            context.Request.Method, context.Request.Path, context.Response.StatusCode, elapsed);
    }
    catch (Exception ex)
    {
        var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
        logger.LogError(ex, "✗ {Method} {Path} {ElapsedMs}ms", 
            context.Request.Method, context.Request.Path, elapsed);
        throw;
    }
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
app.UseAuthorization();

app.MapControllers();

// =====================
// Bind to all network interfaces for Docker / ACI
// =====================
// ASPNETCORE_URLS is set in docker-compose (http://+:8080)
// Kestrel will automatically use ASPNETCORE_URLS if set
// Only set default if ASPNETCORE_URLS is not configured
var urls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
if (string.IsNullOrEmpty(urls))
{
    // Default to 8080 for Docker compatibility
    app.Urls.Add("http://0.0.0.0:8080");
}

app.Run();

public partial class Program { }
