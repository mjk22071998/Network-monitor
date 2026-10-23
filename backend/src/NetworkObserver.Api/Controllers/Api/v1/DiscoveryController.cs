using Microsoft.AspNetCore.Mvc;
using NetworkObserver.Api.Filters;
using NetworkObserver.Application.DTOs;
using NetworkObserver.Application.Interfaces.Discovery;
using NetworkObserver.Domain.Enums;

namespace NetworkObserver.Api.Controllers.Api.v1;

/// <summary>
/// Operational controller for Asynchronous Subnet Discovery Sweeps complying with
/// api_contracts_specification.md Section 8.
/// Boundary Rule: Controllers never inject DbContext; all operations route through ISubnetDiscoveryService.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public class DiscoveryController : ControllerBase
{
    private readonly ISubnetDiscoveryService _discoveryService;

    public DiscoveryController(ISubnetDiscoveryService discoveryService)
    {
        _discoveryService = discoveryService;
    }

    /// <summary>
    /// Dispatches an asynchronous subnet sweep (ICMP sweep, ARP cache read, SNMP switch bridge interrogation).
    /// Guarded by strict role check: NetworkAdmin and NetworkEngineer only (403 for IT Support).
    /// </summary>
    [HttpPost("scan")]
    [AuthorizeRole(RoleType.NetworkAdmin, RoleType.NetworkEngineer)]
    [ProducesResponseType(typeof(SubnetScanInitResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> TriggerScan(
        [FromBody] SubnetScanRequest request,
        CancellationToken ct)
    {
        // Enclave single-site scope defaults to Site 1
        var siteId = 1;
        var userId = GetCallerUserId();
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        var response = await _discoveryService.TriggerScanAsync(request, siteId, userId, ipAddress, ct);
        return Accepted(response);
    }

    /// <summary>
    /// Polls discovery scan progress percentage and discovered assets list.
    /// Accessible to NetworkAdmin and NetworkEngineer.
    /// </summary>
    [HttpGet("scan/{scanId}")]
    [AuthorizeRole(RoleType.NetworkAdmin, RoleType.NetworkEngineer)]
    [ProducesResponseType(typeof(SubnetScanStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetScanStatus(
        [FromRoute] string scanId,
        CancellationToken ct)
    {
        var response = await _discoveryService.GetScanStatusAsync(scanId, ct);
        return Ok(response);
    }

    /// <summary>
    /// Cancels an in-progress subnet discovery sweep.
    /// </summary>
    [HttpPost("scan/{scanId}/cancel")]
    [AuthorizeRole(RoleType.NetworkAdmin, RoleType.NetworkEngineer)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelScan(
        [FromRoute] string scanId,
        CancellationToken ct)
    {
        var userId = GetCallerUserId();
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        var cancelled = await _discoveryService.CancelScanAsync(scanId, userId, ipAddress, ct);
        return Ok(new { scan_id = scanId, cancelled });
    }

    private int GetCallerUserId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        return claim != null && int.TryParse(claim.Value, out var uid) ? uid : 1;
    }
}
