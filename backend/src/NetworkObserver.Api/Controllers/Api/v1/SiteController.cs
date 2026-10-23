using Microsoft.AspNetCore.Mvc;
using NetworkObserver.Api.Filters;
using NetworkObserver.Application.DTOs;
using NetworkObserver.Application.Interfaces.Services;
using NetworkObserver.Domain.Entities;
using NetworkObserver.Domain.Enums;

namespace NetworkObserver.Api.Controllers.Api.v1;

/// <summary>
/// Site enclave API providing single-site boundary configuration and metadata.
/// Complies with api_contracts_specification.md Section 4 and Master RBAC Matrix.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public class SiteController : ControllerBase
{
    private readonly ISiteService _siteService;

    public SiteController(ISiteService siteService)
    {
        _siteService = siteService;
    }

    /// <summary>
    /// Retrieves active enclave site metadata (site_id = 1).
    /// Accessible to all authenticated operational roles (Admin, Engineer, IT Support).
    /// </summary>
    [HttpGet]
    [AuthorizeRole(RoleType.NetworkAdmin, RoleType.NetworkEngineer, RoleType.ItSupport)]
    [ProducesResponseType(typeof(SiteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetSite(CancellationToken ct)
    {
        var site = await _siteService.GetActiveSiteAsync(ct);
        return Ok(site);
    }

    /// <summary>
    /// Updates installation enclave site metadata.
    /// Exclusively restricted to NetworkAdmin.
    /// </summary>
    [HttpPut]
    [AuthorizeRole(RoleType.NetworkAdmin)]
    [ProducesResponseType(typeof(UpdateSiteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateSite([FromBody] UpdateSiteRequest request, CancellationToken ct)
    {
        var userId = GetCallerUserId();
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        var response = await _siteService.UpdateActiveSiteAsync(request, userId, ipAddress, ct);
        return Ok(response);
    }

    private int GetCallerUserId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        return claim != null && int.TryParse(claim.Value, out var uid) ? uid : 1;
    }
}
