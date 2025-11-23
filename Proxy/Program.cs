using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Proxy.Services;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Ocelot Configuration - single ocelot.json file in read-only mode
builder.Configuration
    .SetBasePath(builder.Environment.ContentRootPath)
    .AddOcelot(); // single ocelot.json file in read-only mode

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddControllers();

// Add Redis for cache inspection (CacheController)
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

// Ocelot setup - handles routing, load balancing, and caching
builder.Services.AddOcelot(builder.Configuration);

// Register CacheService only for cache inspection API
builder.Services.AddSingleton<CacheService>();

// Add logging
if (builder.Environment.IsDevelopment())
{
    builder.Logging.AddConsole();
    builder.Logging.AddDebug();
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

// Use Ocelot middleware - must be called before other middleware
// Note: Ocelot pipeline is not compatible with app.Map*() methods
await app.UseOcelot();

// Add other middleware after Ocelot
app.UseRouting();
app.UseAuthorization();
app.UseStaticFiles();

// Map controllers and Razor pages (these work with Ocelot as they're not minimal API)
app.MapControllers();
app.MapRazorPages();

await app.RunAsync();
