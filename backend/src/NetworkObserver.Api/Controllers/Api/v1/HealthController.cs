using Microsoft.AspNetCore.Mvc;

namespace NetworkObserver.Api.Controllers.Api.v1;

/// <summary>
/// Operational health check endpoint for monitoring system status and API availability.
/// Complies with api_contracts_specification.md Section 7.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetHealth()
    {
        return Ok(new
        {
            status = "UP",
            version = "2.2",
            timestamp = DateTimeOffset.UtcNow
        });
    }
}
