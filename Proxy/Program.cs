using Proxy.Middleware;
using Proxy.Services;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddControllers();

// Add HttpClient for proxying requests
builder.Services.AddHttpClient();

// Add Redis for caching
var redisConnectionString = builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379";
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConnectionString;
});

// Add Redis connection for direct access (for cache inspection)
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    return ConnectionMultiplexer.Connect(redisConnectionString);
});

// Register services
builder.Services.AddSingleton<LoadBalancerService>();
builder.Services.AddSingleton<CacheService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseRouting();
app.UseAuthorization();

// Add reverse proxy middleware - should be early in pipeline
app.UseMiddleware<ReverseProxyMiddleware>();

app.UseStaticFiles();
app.MapControllers();
app.MapRazorPages();

app.Run();
