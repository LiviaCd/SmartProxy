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
        try
        {
            // Check Cassandra connection with fallback support
            _cassandraService.ExecuteWithFallback("SELECT now() FROM system.local");
            
            _logger.LogDebug("Health check: Cassandra connected");
            return Ok(new
            {
                status = "healthy",
                timestamp = DateTime.UtcNow.ToString("O"),
                cassandra = "connected"
            });
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Health check: Cassandra connection failed");
            return StatusCode(503, new
            {
                status = "unhealthy",
                timestamp = DateTime.UtcNow.ToString("O"),
                cassandra = "disconnected",
                error = e.Message
            });
        }
    }
}

