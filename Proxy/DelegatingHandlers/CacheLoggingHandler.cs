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

        _logger.LogInformation(
            "Request: {Method} {Path} | Status: {StatusCode} | Cache: {CacheStatus} | Time: {ElapsedMs}ms",
            request.Method,
            request.RequestUri?.PathAndQuery,
            (int)response.StatusCode,
            cacheStatus,
            stopwatch.ElapsedMilliseconds);

        return response;
    }
}

