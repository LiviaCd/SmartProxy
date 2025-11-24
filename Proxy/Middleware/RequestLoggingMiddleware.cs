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
        var originalBodyStream = context.Response.Body;

        // Capture request details
        var method = context.Request.Method;
        var path = context.Request.Path.Value ?? "";
        var queryString = context.Request.QueryString.Value ?? "";
        var acceptHeader = context.Request.Headers["Accept"].FirstOrDefault() ?? "not specified";
        var contentType = context.Request.ContentType ?? "none";
        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var userAgent = context.Request.Headers["User-Agent"].FirstOrDefault() ?? "unknown";

        // Log incoming request
        _logger.LogInformation(
            "[INCOMING REQUEST] {Method} {Path}{QueryString} | Client IP: {ClientIp} | Accept: {AcceptHeader} | Content-Type: {ContentType} | User-Agent: {UserAgent}",
            method,
            path,
            queryString,
            clientIp,
            acceptHeader,
            contentType,
            userAgent);

        // Capture response body for logging
        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();

            // Get response details
            var statusCode = context.Response.StatusCode;
            var responseContentType = context.Response.ContentType ?? "none";
            
            // Detect response format
            var responseFormat = "unknown";
            if (responseContentType.Contains("json", StringComparison.OrdinalIgnoreCase))
                responseFormat = "JSON";
            else if (responseContentType.Contains("xml", StringComparison.OrdinalIgnoreCase))
                responseFormat = "XML";
            else if (!string.IsNullOrEmpty(responseContentType) && responseContentType != "none")
                responseFormat = responseContentType;

            // Detect cache status based on response time and headers
            var cacheStatus = "MISS";
            if (context.Response.Headers.ContainsKey("X-Cache"))
            {
                var cacheHeader = context.Response.Headers["X-Cache"].FirstOrDefault();
                cacheStatus = cacheHeader ?? "UNKNOWN";
            }
            else if (stopwatch.ElapsedMilliseconds < 5 && method == "GET")
            {
                // Very fast response (< 5ms) for GET requests likely means cache HIT
                cacheStatus = "HIT (inferred)";
            }

            // Get response size
            responseBody.Seek(0, SeekOrigin.Begin);
            var responseBodyText = await new StreamReader(responseBody).ReadToEndAsync();
            var responseSize = Encoding.UTF8.GetByteCount(responseBodyText);
            responseBody.Seek(0, SeekOrigin.Begin);

            // Log response with cache information
            _logger.LogInformation(
                "[OUTGOING RESPONSE] {Method} {Path}{QueryString} | Status: {StatusCode} | Format: {ResponseFormat} | Cache: {CacheStatus} | Time: {ElapsedMs}ms | Size: {Size} bytes | Accept Requested: {AcceptHeader}",
                method,
                path,
                queryString,
                statusCode,
                responseFormat,
                cacheStatus,
                stopwatch.ElapsedMilliseconds,
                responseSize,
                acceptHeader);

            // Copy response back to original stream
            await responseBody.CopyToAsync(originalBodyStream);
        }
    }
}

