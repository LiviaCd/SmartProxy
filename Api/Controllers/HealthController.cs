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
            // Check Cassandra connection
            var statement = _cassandraService.CreateStatement("SELECT now() FROM system.local");
            _cassandraService.Session.Execute(statement);
            
            return Ok(new
            {
                status = "healthy",
                timestamp = DateTime.UtcNow.ToString("O"),
                cassandra = "connected"
            });
        }
        catch (Exception e)
        {
            return Ok(new
            {
                status = "unhealthy",
                timestamp = DateTime.UtcNow.ToString("O"),
                cassandra = "disconnected",
                error = e.Message
            });
        }
    }
}

