using Microsoft.AspNetCore.Mvc;
using NetworkObserver.Api.Filters;
using NetworkObserver.Application.DTOs;
using NetworkObserver.Application.Interfaces.Services;
using NetworkObserver.Domain.Enums;

namespace NetworkObserver.Api.Controllers.Api.v1;

/// <summary>
/// Global monitoring cadence and telemetry engine configuration controller.
/// Complies with api_contracts_specification.md Section 7.5 and 7.6.
/// Boundary Rule: Controllers never inject DbContext; all operations route through IMonitoringConfigService.
/// </summary>
[ApiController]
[Route("api/v1/monitoring/config")]
public class MonitoringController : ControllerBase
{
    private readonly IMonitoringConfigService _configService;

    public MonitoringController(IMonitoringConfigService configService)
    {
        _configService = configService;
    }

    /// <summary>
    /// Returns the active global polling cadences, timeouts, and cascading suppression state.
    /// Accessible to all authenticated operational roles (Admin, Engineer, IT Support).
    /// </summary>
    [HttpGet]
    [AuthorizeRole(RoleType.NetworkAdmin, RoleType.NetworkEngineer, RoleType.ItSupport)]
    [ProducesResponseType(typeof(MonitoringConfigDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetConfig(CancellationToken ct)
    {
        var config = await _configService.GetConfigAsync(ct);
        return Ok(config);
    }

    /// <summary>
    /// Updates global polling cadences, timeouts, and toggles cascade suppression.
    /// Accessible to NetworkAdmin and NetworkEngineer. Denied (403) for IT Support.
    /// </summary>
    [HttpPut]
    [AuthorizeRole(RoleType.NetworkAdmin, RoleType.NetworkEngineer)]
    [ProducesResponseType(typeof(MonitoringConfigDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateConfig(
        [FromBody] UpdateMonitoringConfigRequest request,
        CancellationToken ct)
    {
        var userId = GetCallerUserId();
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        var updated = await _configService.UpdateConfigAsync(request, userId, ipAddress, ct);
        return Ok(updated);
    }

    private int GetCallerUserId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        return claim != null && int.TryParse(claim.Value, out var uid) ? uid : 1;
    }
}
