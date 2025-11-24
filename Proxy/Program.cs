using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Ocelot.Provider.Polly;

var builder = WebApplication.CreateBuilder(args);

// Ocelot Configuration - load ocelot.json file
builder.Configuration
    .SetBasePath(builder.Environment.ContentRootPath)
    .AddJsonFile("ocelot.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables();

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddControllers(options =>
    {
        options.RespectBrowserAcceptHeader = true; // Respect Accept header from client
    })
    .AddXmlSerializerFormatters()  // Support XML format for content negotiation
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = null; // Preserve original property names
    });

// Ocelot setup - handles routing, load balancing, and caching
builder.Services.AddOcelot(builder.Configuration)
    .AddPolly()  // Required for QoSOptions (Circuit Breaker, Timeout)
    .AddDelegatingHandler<Proxy.DelegatingHandlers.CacheLoggingHandler>();

// Add logging - always enable console logging for Docker
builder.Logging.AddConsole();
if (builder.Environment.IsDevelopment())
{
    builder.Logging.AddDebug();
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

// Add request logging middleware before Ocelot
app.UseMiddleware<Proxy.Middleware.RequestLoggingMiddleware>();

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
