using Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("health")]
public class HealthController : ControllerBase
{
    private readonly CassandraService _cassandraService;
    private readonly ILogger<HealthController> _logger;

    public HealthController(CassandraService cassandraService, ILogger<HealthController> logger)
    {
        _cassandraService = cassandraService;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult HealthCheck()
    {
        // Always return 200 OK for health checks
        // Azure Container Apps uses this to determine if the app is running
        // Database connectivity issues should not make the app appear unhealthy
        // Use separate endpoints or logging for detailed diagnostics
        
        // Try to check Cassandra, but don't fail health check if it's unavailable
        try
        {
            _cassandraService.ExecuteWithFallback("SELECT now() FROM system.local");
            _logger.LogDebug("Health check: Cassandra connected");
            
            return Ok(new
            {
                status = "healthy",
                timestamp = DateTime.UtcNow.ToString("O"),
                service = "smartproxy-api",
                cassandra = "connected"
            });
        }
        catch (Exception e)
        {
            // Log warning but don't fail health check
            // App is healthy even if database is temporarily unavailable
            _logger.LogWarning(e, "Health check: Cassandra connection failed, but app is still healthy");
            
            return Ok(new
            {
                status = "healthy",
                timestamp = DateTime.UtcNow.ToString("O"),
                service = "smartproxy-api",
                cassandra = "disconnected",
                warning = "Database temporarily unavailable"
            });
        }
    }
}

