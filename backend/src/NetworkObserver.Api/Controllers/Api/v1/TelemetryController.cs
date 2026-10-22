using Microsoft.AspNetCore.Mvc;
using NetworkObserver.Api.Filters;
using NetworkObserver.Application.DTOs;
using NetworkObserver.Application.Interfaces.Services;
using NetworkObserver.Domain.Enums;

namespace NetworkObserver.Api.Controllers.Api.v1;

/// <summary>
/// Telemetry operational controller providing live HUD metrics, node inspection,
/// time-series telemetry history, and on-demand sweep triggers.
/// Complies with api_contracts_specification.md Section 7.
/// Boundary Rule: Controllers never inject DbContext; all operations route through ITelemetryService.
/// </summary>
[ApiController]
[Route("api/v1")]
public class TelemetryController : ControllerBase
{
    private readonly ITelemetryService _telemetryService;

    public TelemetryController(ITelemetryService telemetryService)
    {
        _telemetryService = telemetryService;
    }

    /// <summary>
    /// Top HUD Critical Metrics Summary.
    /// Delivers aggregated reachability counters (total, reachable, outage, suppressed, unknown)
    /// and host health stats for top dashboard cards.
    /// Accessible to all authenticated operational roles.
    /// </summary>
    [HttpGet("telemetry/summary")]
    [AuthorizeRole(RoleType.NetworkAdmin, RoleType.NetworkEngineer, RoleType.ItSupport)]
    [ProducesResponseType(typeof(TelemetrySummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetSummary(CancellationToken ct)
    {
        var summary = await _telemetryService.GetSummaryAsync(ct);
        return Ok(summary);
    }

    /// <summary>
    /// Triggers an immediate concurrent raw socket ICMP reachability sweep across all monitored devices.
    /// Bounded to 50 concurrent sockets within the 500MB RAM budget.
    /// Accessible to NetworkAdmin and NetworkEngineer.
    /// </summary>
    [HttpPost("telemetry/poll")]
    [AuthorizeRole(RoleType.NetworkAdmin, RoleType.NetworkEngineer)]
    [ProducesResponseType(typeof(PollSweepResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> TriggerPollSweep(CancellationToken ct)
    {
        var result = await _telemetryService.ExecuteReachabilitySweepAsync(ct);
        return Ok(result);
    }

    /// <summary>
    /// Triggers an immediate decoupled host health telemetry sweep across all monitored devices.
    /// Queries CPU, RAM, network throughput, and multi-volume disk storage.
    /// Accessible to NetworkAdmin and NetworkEngineer.
    /// </summary>
    [HttpPost("telemetry/health/poll")]
    [AuthorizeRole(RoleType.NetworkAdmin, RoleType.NetworkEngineer)]
    [ProducesResponseType(typeof(HealthSweepResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> TriggerHealthSweep(CancellationToken ct)
    {
        var result = await _telemetryService.ExecuteHealthSweepAsync(ct);
        return Ok(result);
    }

    /// <summary>
    /// Slide-Over Telemetry Inspector (Live Node Details).
    /// Returns latest reachability and host health metrics for a specific device.
    /// Strict N/A Rule: Missing or timed-out metrics return explicit null.
    /// Accessible to all authenticated operational roles.
    /// </summary>
    [HttpGet("devices/{id:int}/telemetry/live")]
    [AuthorizeRole(RoleType.NetworkAdmin, RoleType.NetworkEngineer, RoleType.ItSupport)]
    [ProducesResponseType(typeof(DeviceLiveTelemetryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLiveTelemetry([FromRoute] int id, CancellationToken ct)
    {
        var telemetry = await _telemetryService.GetLiveTelemetryAsync(id, ct);
        return Ok(telemetry);
    }

    /// <summary>
    /// Historical Telemetry Time-Series Queries.
    /// Returns historical reachability and latency samples within the specified window.
    /// Accessible to all authenticated operational roles.
    /// </summary>
    [HttpGet("devices/{id:int}/telemetry/history")]
    [AuthorizeRole(RoleType.NetworkAdmin, RoleType.NetworkEngineer, RoleType.ItSupport)]
    [ProducesResponseType(typeof(DeviceHistoryTelemetryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTelemetryHistory(
        [FromRoute] int id,
        [FromQuery] DateTimeOffset? start,
        [FromQuery] DateTimeOffset? end,
        [FromQuery] string? interval,
        CancellationToken ct)
    {
        var windowStart = start ?? DateTimeOffset.UtcNow.AddHours(-1);
        var windowEnd = end ?? DateTimeOffset.UtcNow;

        var history = await _telemetryService.GetTelemetryHistoryAsync(id, windowStart, windowEnd, interval ?? "raw", ct);
        return Ok(history);
    }
}
