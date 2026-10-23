using Microsoft.AspNetCore.Mvc;
using NetworkObserver.Api.Filters;
using NetworkObserver.Application.DTOs;
using NetworkObserver.Application.Interfaces.Services;
using NetworkObserver.Domain.Enums;

namespace NetworkObserver.Api.Controllers.Api.v1;

/// <summary>
/// Operational controller for querying immutable security audit logs.
/// Complies with api_contracts_specification.md Section 11 and security_and_rbac_specification.md Section 8.
/// Access Level: NETWORK_ADMIN exclusively.
/// Boundary Rule: Controllers never inject DbContext; all operations route through IAuditService.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public class AuditController : ControllerBase
{
    private readonly IAuditService _auditService;

    public AuditController(IAuditService auditService)
    {
        _auditService = auditService;
    }

    /// <summary>
    /// Paginated query of immutable security audit logs with optional filtering.
    /// Denied 403 Forbidden for NETWORK_ENGINEER and IT_SUPPORT.
    /// </summary>
    [HttpGet("logs")]
    [AuthorizeRole(RoleType.NetworkAdmin)]
    [ProducesResponseType(typeof(AuditLogsPagedResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 50,
        [FromQuery] int? user_id = null,
        [FromQuery] string? action_code = null,
        [FromQuery] DateTimeOffset? start = null,
        [FromQuery] DateTimeOffset? end = null,
        CancellationToken ct = default)
    {
        var parameters = new AuditLogQueryParameters(
            Page: page,
            Limit: limit,
            UserId: user_id,
            ActionCode: action_code,
            Start: start,
            End: end
        );

        var response = await _auditService.GetAuditLogsPagedAsync(parameters, ct);
        return Ok(response);
    }
}
