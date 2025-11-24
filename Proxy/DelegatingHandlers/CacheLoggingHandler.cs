using System.Net.Http.Headers;

namespace Proxy.DelegatingHandlers;

public class CacheLoggingHandler : DelegatingHandler
{
    private readonly ILogger<CacheLoggingHandler> _logger;

    public CacheLoggingHandler(ILogger<CacheLoggingHandler> logger)
    {
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        // Log request details
        var acceptHeader = request.Headers.Accept?.FirstOrDefault()?.ToString() ?? "not specified";
        var downstreamUrl = request.RequestUri?.ToString() ?? "unknown";
        
        _logger.LogInformation(
            "[DOWNSTREAM REQUEST] {Method} {Url} | Accept: {AcceptHeader} | Content-Type: {ContentType}",
            request.Method,
            downstreamUrl,
            acceptHeader,
            request.Content?.Headers?.ContentType?.ToString() ?? "none");

        var response = await base.SendAsync(request, cancellationToken);
        
        stopwatch.Stop();

        // Verifică dacă răspunsul vine din cache prin header-uri
        var cacheStatus = "MISS";
        if (response.Headers.Contains("X-Cache"))
        {
            var cacheHeader = response.Headers.GetValues("X-Cache").FirstOrDefault();
            cacheStatus = cacheHeader ?? "UNKNOWN";
        }
        else if (stopwatch.ElapsedMilliseconds < 10) // Cache hits sunt foarte rapide
        {
            cacheStatus = "LIKELY_HIT";
        }

        // Detect response format
        var responseFormat = "unknown";
        var contentType = response.Content?.Headers?.ContentType?.MediaType ?? "";
        if (contentType.Contains("json"))
            responseFormat = "JSON";
        else if (contentType.Contains("xml"))
            responseFormat = "XML";
        else if (!string.IsNullOrEmpty(contentType))
            responseFormat = contentType;

        _logger.LogInformation(
            "[DOWNSTREAM RESPONSE] {Method} {Path} | Status: {StatusCode} | Format: {ResponseFormat} | Cache: {CacheStatus} | Time: {ElapsedMs}ms | Size: {Size} bytes",
            request.Method,
            request.RequestUri?.PathAndQuery,
            (int)response.StatusCode,
            responseFormat,
            cacheStatus,
            stopwatch.ElapsedMilliseconds,
            response.Content?.Headers?.ContentLength ?? 0);

        return response;
    }
}

