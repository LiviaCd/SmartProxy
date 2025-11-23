using Proxy.Services;
using System.Net.Http.Headers;
using System.Text;

namespace Proxy.Middleware;

public class ReverseProxyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly LoadBalancerService _loadBalancer;
    private readonly CacheService _cacheService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ReverseProxyMiddleware> _logger;
    private readonly bool _enableCaching;

    public ReverseProxyMiddleware(
        RequestDelegate next,
        LoadBalancerService loadBalancer,
        CacheService cacheService,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<ReverseProxyMiddleware> logger)
    {
        _next = next;
        _loadBalancer = loadBalancer;
        _cacheService = cacheService;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _enableCaching = configuration.GetValue<bool>("Proxy:EnableCaching", true);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip proxy for static files, health checks, and API endpoints
        if (context.Request.Path.StartsWithSegments("/health") ||
            context.Request.Path.StartsWithSegments("/swagger") ||
            context.Request.Path.StartsWithSegments("/_") ||
            context.Request.Path.StartsWithSegments("/api"))
        {
            await _next(context);
            return;
        }

        var method = context.Request.Method;
        var path = context.Request.Path.Value ?? "/";
        var queryString = context.Request.QueryString.Value;

        // Generate cache key
        var cacheKey = _cacheService.GenerateCacheKey(method, path, queryString);

        // Try to get from cache for GET requests
        if (_enableCaching && method == "GET" && !string.IsNullOrEmpty(cacheKey))
        {
            var cachedResponse = await _cacheService.GetAsync<CachedResponse>(cacheKey);
            if (cachedResponse != null)
            {
                _logger.LogInformation("Cache HIT for {Path}", path);
                await WriteCachedResponse(context, cachedResponse);
                return;
            }
            _logger.LogInformation("Cache MISS for {Path}", path);
        }

        // Get next backend server using load balancing
        var backendServer = _loadBalancer.GetNextServer();
        var targetUrl = $"{backendServer}{path}{queryString}";

        _logger.LogInformation("Proxying {Method} {Path} to {BackendServer}", method, path, backendServer);

        try
        {
            // Forward request to backend
            var response = await ForwardRequest(context, targetUrl);

            // Cache the response if it's a successful GET request
            if (_enableCaching && method == "GET" && response.IsSuccessStatusCode && !string.IsNullOrEmpty(cacheKey))
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                var cachedResponse = new CachedResponse
                {
                    StatusCode = (int)response.StatusCode,
                    Headers = response.Headers.ToDictionary(h => h.Key, h => h.Value.FirstOrDefault() ?? string.Empty),
                    Content = responseBody,
                    ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json"
                };

                await _cacheService.SetAsync(cacheKey, cachedResponse);
            }

            // Invalidate cache for write operations
            if ((method == "POST" || method == "PUT" || method == "DELETE") && _enableCaching)
            {
                // Invalidate related cache entries
                await InvalidateRelatedCache(path);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error proxying request to {BackendServer}", backendServer);
            context.Response.StatusCode = 502; // Bad Gateway
            await context.Response.WriteAsync("Error connecting to backend server");
        }
    }

    private async Task<HttpResponseMessage> ForwardRequest(HttpContext context, string targetUrl)
    {
        var client = _httpClientFactory.CreateClient();
        var request = new HttpRequestMessage(new HttpMethod(context.Request.Method), targetUrl);

        // Copy request headers (except Host and Connection)
        foreach (var header in context.Request.Headers)
        {
            if (header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase) ||
                header.Key.Equals("Connection", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!request.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()))
            {
                request.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }
        }

        // Copy request body for POST, PUT, PATCH
        if (context.Request.ContentLength > 0 && 
            (context.Request.Method == "POST" || context.Request.Method == "PUT" || context.Request.Method == "PATCH"))
        {
            using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            request.Content = new StringContent(body, Encoding.UTF8, context.Request.ContentType ?? "application/json");
        }

        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);
        
        // Copy response headers
        foreach (var header in response.Headers)
        {
            context.Response.Headers[header.Key] = header.Value.ToArray();
        }

        foreach (var header in response.Content.Headers)
        {
            context.Response.Headers[header.Key] = header.Value.ToArray();
        }

        context.Response.StatusCode = (int)response.StatusCode;

        // Copy response body
        await response.Content.CopyToAsync(context.Response.Body);

        return response;
    }

    private async Task WriteCachedResponse(HttpContext context, CachedResponse cachedResponse)
    {
        context.Response.StatusCode = cachedResponse.StatusCode;
        
        foreach (var header in cachedResponse.Headers)
        {
            context.Response.Headers[header.Key] = header.Value;
        }

        context.Response.ContentType = cachedResponse.ContentType;
        await context.Response.WriteAsync(cachedResponse.Content);
    }

    private async Task InvalidateRelatedCache(string path)
    {
        // Invalidate cache for the specific path and related paths
        var keysToInvalidate = new List<string>
        {
            _cacheService.GenerateCacheKey("GET", path)
        };

        // If it's a specific resource, also invalidate the list endpoint
        if (path.Contains("/books/") && path.Split('/').Length > 2)
        {
            keysToInvalidate.Add(_cacheService.GenerateCacheKey("GET", "/books"));
        }

        foreach (var key in keysToInvalidate.Where(k => !string.IsNullOrEmpty(k)))
        {
            await _cacheService.RemoveAsync(key);
        }
    }
}

public class CachedResponse
{
    public int StatusCode { get; set; }
    public Dictionary<string, string> Headers { get; set; } = new();
    public string Content { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/json";
}

