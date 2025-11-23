using Microsoft.AspNetCore.Mvc;

namespace Proxy.Controllers;

[ApiController]
[Route("health")]
public class HealthController : ControllerBase
{
    private readonly ILogger<HealthController> _logger;
    private readonly Proxy.Services.LoadBalancerService _loadBalancer;
    private readonly Proxy.Services.CacheService _cacheService;

    public HealthController(
        ILogger<HealthController> logger,
        Proxy.Services.LoadBalancerService loadBalancer,
        Proxy.Services.CacheService cacheService)
    {
        _logger = logger;
        _loadBalancer = loadBalancer;
        _cacheService = cacheService;
    }

    [HttpGet]
    public IActionResult HealthCheck()
    {
        var servers = _loadBalancer.GetAllServers();
        
        return Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow.ToString("O"),
            backendServers = servers,
            cacheEnabled = true
        });
    }
}

