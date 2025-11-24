using System.Diagnostics;
using System.Text;

namespace Proxy.Middleware;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();

        // Capture request details
        var method = context.Request.Method;
        var path = context.Request.Path.Value ?? "";
        var queryString = context.Request.QueryString.Value ?? "";
        var acceptHeader = context.Request.Headers["Accept"].FirstOrDefault() ?? "not specified";
        var contentType = context.Request.ContentType ?? "none";
        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // Log incoming request to proxy
        _logger.LogInformation(
            "[PROXY REQUEST] → {Method} {Path}{QueryString} | Client IP:{ClientIp} | Accept:{AcceptHeader} | Content-Type:{ContentType}",
            method,
            path,
            queryString,
            clientIp,
            acceptHeader,
            contentType);

        // Don't intercept Response.Body - this interferes with Ocelot caching
        // Ocelot needs to read the response body to cache it, so we can't replace it
        
        await _next(context);
        
        stopwatch.Stop();

        // Get response details after Ocelot has processed the request
        var statusCode = context.Response.StatusCode;
        var responseContentType = context.Response.ContentType ?? "none";
        var responseSize = context.Response.ContentLength ?? 0;
        
        // Detect response format
        var responseFormat = "unknown";
        if (responseContentType.Contains("json", StringComparison.OrdinalIgnoreCase))
            responseFormat = "JSON";
        else if (responseContentType.Contains("xml", StringComparison.OrdinalIgnoreCase))
            responseFormat = "XML";
        else if (!string.IsNullOrEmpty(responseContentType) && responseContentType != "none")
            responseFormat = responseContentType;

        // Detect cache status based on response time and headers
        // Cache HIT: Very fast response (< 10ms) for GET requests
        // Cache MISS: Slower response (> 10ms) means request went to backend
        var cacheStatus = "MISS";
        var isCacheHit = false;
        
        if (context.Response.Headers.ContainsKey("X-Cache"))
        {
            var cacheHeader = context.Response.Headers["X-Cache"].FirstOrDefault();
            cacheStatus = cacheHeader ?? "UNKNOWN";
            isCacheHit = cacheStatus.Contains("HIT", StringComparison.OrdinalIgnoreCase);
        }
        else if (stopwatch.ElapsedMilliseconds < 10 && method == "GET")
        {
            // Very fast response (< 10ms) for GET requests = likely cache HIT
            cacheStatus = "HIT";
            isCacheHit = true;
        }

        // Log cache status explicitly
        if (isCacheHit)
        {
            _logger.LogInformation(
                "[CACHE HIT] 🎯 Serving from cache: {Method} {Path}{QueryString} | Format:{ResponseFormat} | Time:{ElapsedMs}ms",
                method,
                path,
                queryString,
                responseFormat,
                stopwatch.ElapsedMilliseconds);
        }

        // Log response from proxy
        var statusEmoji = statusCode >= 200 && statusCode < 300 ? "✅" : statusCode >= 400 ? "❌" : "⚠️";
        _logger.LogInformation(
            "[PROXY RESPONSE] ← {Method} {Path}{QueryString} | {StatusEmoji} {StatusCode} | Format:{ResponseFormat} | Cache:{CacheStatus} | Time:{ElapsedMs}ms | Size:{Size}B | Accept:{AcceptHeader}",
            method,
            path,
            queryString,
            statusEmoji,
            statusCode,
            responseFormat,
            cacheStatus,
            stopwatch.ElapsedMilliseconds,
            responseSize,
            acceptHeader);
    }
}

