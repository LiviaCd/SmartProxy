using Microsoft.AspNetCore.Mvc;
using Proxy.Services;
using StackExchange.Redis;
using System.Text.Json;

namespace Proxy.Controllers;

[ApiController]
[Route("api/cache")]
public class CacheController : ControllerBase
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<CacheController> _logger;
    private readonly CacheService _cacheService;

    public CacheController(
        IConnectionMultiplexer redis,
        ILogger<CacheController> logger,
        CacheService cacheService)
    {
        _redis = redis;
        _logger = logger;
        _cacheService = cacheService;
    }

    [HttpGet("keys")]
    public IActionResult GetAllKeys()
    {
        try
        {
            var db = _redis.GetDatabase();
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            
            var keys = server.Keys(pattern: "*").ToList();
            var result = new List<CacheKeyInfo>();

            foreach (var key in keys)
            {
                var value = db.StringGet(key);
                var ttl = db.KeyTimeToLive(key);
                
                result.Add(new CacheKeyInfo
                {
                    Key = key.ToString(),
                    Value = value.HasValue ? value.ToString() : null,
                    TtlSeconds = ttl?.TotalSeconds ?? -1,
                    IsExpired = !ttl.HasValue || ttl.Value.TotalSeconds <= 0
                });
            }

            return Ok(new
            {
                count = result.Count,
                keys = result.OrderBy(k => k.Key)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cache keys");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("keys/{key}")]
    public IActionResult GetKey(string key)
    {
        try
        {
            var db = _redis.GetDatabase();
            var value = db.StringGet(key);
            var ttl = db.KeyTimeToLive(key);

            if (!value.HasValue)
            {
                return NotFound(new { error = "Key not found" });
            }

            // Try to parse as JSON for better display
            object? parsedValue = null;
            try
            {
                parsedValue = JsonSerializer.Deserialize<object>(value.ToString());
            }
            catch
            {
                parsedValue = value.ToString();
            }

            return Ok(new CacheKeyInfo
            {
                Key = key,
                Value = value.ToString(),
                ParsedValue = parsedValue,
                TtlSeconds = ttl?.TotalSeconds ?? -1,
                ExpiresAt = ttl.HasValue ? DateTime.UtcNow.Add(ttl.Value) : (DateTime?)null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cache key: {Key}", key);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpDelete("keys/{key}")]
    public IActionResult DeleteKey(string key)
    {
        try
        {
            var db = _redis.GetDatabase();
            var deleted = db.KeyDelete(key);

            if (deleted)
            {
                return Ok(new { message = $"Key '{key}' deleted successfully" });
            }
            else
            {
                return NotFound(new { error = "Key not found" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting cache key: {Key}", key);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpDelete("keys")]
    public IActionResult ClearAllCache()
    {
        try
        {
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            var db = _redis.GetDatabase();
            
            var keys = server.Keys(pattern: "*").ToList();
            var deletedCount = 0;

            foreach (var key in keys)
            {
                if (db.KeyDelete(key))
                {
                    deletedCount++;
                }
            }

            return Ok(new { message = $"Deleted {deletedCount} keys" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing cache");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("stats")]
    public IActionResult GetStats()
    {
        try
        {
            var db = _redis.GetDatabase();
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            var info = server.Info("stats");

            var keys = server.Keys(pattern: "*").ToList();
            var totalMemory = server.Info("memory").FirstOrDefault(x => x.Key == "used_memory_human")?.Value ?? "N/A";

            return Ok(new
            {
                totalKeys = keys.Count,
                memoryUsed = totalMemory,
                connectedClients = server.Info("clients").FirstOrDefault(x => x.Key == "connected_clients")?.Value ?? "N/A",
                uptime = server.Info("server").FirstOrDefault(x => x.Key == "uptime_in_seconds")?.Value ?? "N/A"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cache stats");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

public class CacheKeyInfo
{
    public string Key { get; set; } = string.Empty;
    public string? Value { get; set; }
    public object? ParsedValue { get; set; }
    public double TtlSeconds { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsExpired { get; set; }
}

