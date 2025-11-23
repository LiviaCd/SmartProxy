using Microsoft.AspNetCore.Mvc;

namespace Proxy.Controllers;

[ApiController]
[Route("health")]
public class HealthController : ControllerBase
{
    private readonly ILogger<HealthController> _logger;
    private readonly IConfiguration _configuration;

    public HealthController(
        ILogger<HealthController> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    [HttpGet]
    public IActionResult HealthCheck()
    {
        // Ocelot gestionează load balancing, deci citim configurația din ocelot.json
        return Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow.ToString("O"),
            gateway = "Ocelot",
            cacheEnabled = true,
            message = "Ocelot API Gateway is running"
        });
    }
}

