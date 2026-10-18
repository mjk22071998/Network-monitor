using Microsoft.AspNetCore.Mvc;
using NetworkObserver.Application.DTOs;
using NetworkObserver.Application.Interfaces;

namespace NetworkObserver.Api.Controllers.Api.v1;

/// <summary>
/// Operational health check endpoint for monitoring system status, API availability,
/// and live PostgreSQL ORM connectivity.
/// Complies with api_contracts_specification.md Section 7 and software_architecture_and_libraries_specification.md.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public class HealthController : ControllerBase
{
    private readonly IDatabaseHealthService _databaseHealthService;

    public HealthController(IDatabaseHealthService databaseHealthService)
    {
        _databaseHealthService = databaseHealthService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(HealthCheckResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HealthCheckResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetHealth(CancellationToken cancellationToken)
    {
        var dbHealth = await _databaseHealthService.CheckHealthAsync(cancellationToken);

        var response = new HealthCheckResponse(
            Status: dbHealth.IsConnected ? "UP" : "DEGRADED",
            Version: "2.2",
            Timestamp: DateTimeOffset.UtcNow,
            Database: dbHealth
        );

        if (!dbHealth.IsConnected)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, response);
        }

        return Ok(response);
    }
}
