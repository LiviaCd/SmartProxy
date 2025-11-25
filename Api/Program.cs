using Api.Services;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using StackExchange.Redis;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services
    .AddFusionCache()
    .WithDistributedCache(_ =>
    {
        // Configure Redis connection using ConfigurationOptions (exactly like in the example)
        // Read all values from configuration (appsettings.json or environment variables)
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
        
        // Create ConfigurationOptions exactly like in the example
        var muxer = ConnectionMultiplexer.Connect(
            new ConfigurationOptions
            {
                EndPoints = { { host, port } },
                User = user,
                Password = password
            }
        );
        
        var options = new RedisCacheOptions
        {
            ConnectionMultiplexerFactory = () => Task.FromResult<IConnectionMultiplexer>(muxer)
        };
        options.InstanceName = "book-app";

        return new RedisCache(options);
    })
    .WithSerializer(new FusionCacheSystemTextJsonSerializer());

builder.Services.AddControllers(options =>
    {
        options.RespectBrowserAcceptHeader = true; // Respect Accept header from client
    })
    .AddXmlSerializerFormatters()  // Support XML format
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = null; // Preserve original property names
    });

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register Cassandra service as singleton
builder.Services.AddSingleton<CassandraService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

app.Run();
