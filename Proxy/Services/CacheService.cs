using Microsoft.Extensions.Caching.Distributed;
using System.Text;
using System.Text.Json;

namespace Proxy.Services;

public class CacheService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<CacheService> _logger;
    private readonly int _cacheExpirationSeconds;

    public CacheService(
        IDistributedCache cache, 
        IConfiguration configuration,
        ILogger<CacheService> logger)
    {
        _cache = cache;
        _logger = logger;
        _cacheExpirationSeconds = configuration.GetValue<int>("Proxy:CacheExpirationSeconds", 300); // Default 5 minutes
    }

    public async Task<T?> GetAsync<T>(string key) where T : class
    {
        try
        {
            var cachedValue = await _cache.GetStringAsync(key);
            if (cachedValue == null)
            {
                return null;
            }

            return JsonSerializer.Deserialize<T>(cachedValue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cache value for key: {Key}", key);
            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value, int? expirationSeconds = null)
    {
        try
        {
            var serializedValue = JsonSerializer.Serialize(value);
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(expirationSeconds ?? _cacheExpirationSeconds)
            };

            await _cache.SetStringAsync(key, serializedValue, options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting cache value for key: {Key}", key);
        }
    }

    public async Task RemoveAsync(string key)
    {
        try
        {
            await _cache.RemoveAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cache value for key: {Key}", key);
        }
    }

    public string GenerateCacheKey(string method, string path, string? queryString = null)
    {
        // Only cache GET requests
        if (method != "GET")
        {
            return string.Empty;
        }

        var key = $"{method}:{path}";
        if (!string.IsNullOrEmpty(queryString))
        {
            key += $"?{queryString}";
        }
        return key;
    }

    public async Task<Dictionary<string, string?>> GetAllCacheKeysAsync()
    {
        var result = new Dictionary<string, string?>();
        try
        {
            // Redis doesn't have a direct "get all keys" in IDistributedCache
            // We need to use StackExchange.Redis directly for this
            // For now, return empty - will be implemented in controller
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all cache keys");
            return result;
        }
    }
}

