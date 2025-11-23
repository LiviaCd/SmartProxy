namespace Proxy.Services;

public class LoadBalancerService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<LoadBalancerService> _logger;
    private readonly List<string> _backendServers;
    private int _currentIndex = 0;
    private readonly object _lockObject = new object();

    public LoadBalancerService(IConfiguration configuration, ILogger<LoadBalancerService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        
        // Get backend servers from configuration
        var servers = _configuration["Proxy:BackendServers"]?.Split(",") 
            ?? new[] { "http://localhost:5000" };
        
        _backendServers = servers.Select(s => s.Trim()).ToList();
        
        _logger.LogInformation("Load balancer initialized with {Count} backend servers", _backendServers.Count);
    }

    /// <summary>
    /// Round-Robin algorithm: selects the next server in rotation
    /// </summary>
    public string GetNextServer()
    {
        lock (_lockObject)
        {
            var server = _backendServers[_currentIndex];
            _currentIndex = (_currentIndex + 1) % _backendServers.Count;
            return server;
        }
    }

    public List<string> GetAllServers() => _backendServers.ToList();
}

