using Api.Services;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services
    .AddFusionCache()
    .WithDistributedCache(_ =>
    {
        var connectionString = builder.Configuration["Redis:ConnectionString"];
        var options = new RedisCacheOptions
        {
            Configuration = connectionString,

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
