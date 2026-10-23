using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using NetworkObserver.Api.Filters;
using NetworkObserver.Application.DTOs;
using NetworkObserver.Application.Interfaces.Repositories;
using NetworkObserver.Application.Interfaces.Services;
using NetworkObserver.Domain.Enums;
using NetworkObserver.Domain.Exceptions;

namespace NetworkObserver.Api.Controllers.Api.v1;

/// <summary>
/// Device inventory management controller implementing 1:1 schema field mapping,
/// tiered role-based equipment authorization, and immutable MAC address enforcement.
/// Boundary Rule: Controllers never inject DbContext; all operations route through IDeviceService.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public class DevicesController : ControllerBase
{
    private readonly IDeviceService _deviceService;

    public DevicesController(IDeviceService deviceService)
    {
        _deviceService = deviceService;
    }

    /// <summary>
    /// Lists paginated and filtered devices matching 10 schema fields.
    /// Supports alternative single device lookup via ?mac=XX:XX:XX:XX:XX:XX query parameter.
    /// Accessible to all authenticated operational roles (Admin, Engineer, IT Support).
    /// </summary>
    [HttpGet]
    [AuthorizeRole(RoleType.NetworkAdmin, RoleType.NetworkEngineer, RoleType.ItSupport)]
    [ProducesResponseType(typeof(PagedResponse<DeviceListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(DeviceDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDevices(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 50,
        [FromQuery] int? group_id = null,
        [FromQuery] short? type_id = null,
        [FromQuery] string? zone = null,
        [FromQuery] string? search = null,
        [FromQuery] string sort_by = "primary_ip",
        [FromQuery] string order = "asc",
        [FromQuery] string? mac = null,
        CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(mac))
        {
            var device = await _deviceService.GetDeviceByMacAsync(mac, ct);
            return Ok(device);
        }

        var parameters = new DeviceQueryParameters(
            Page: page,
            Limit: limit,
            GroupId: group_id,
            TypeId: type_id,
            Zone: zone,
            Search: search,
            SortBy: sort_by,
            Order: order
        );

        var pagedDevices = await _deviceService.GetDevicesAsync(parameters, ct);
        return Ok(pagedDevices);
    }

    /// <summary>
    /// Retrieves single device detailed profile.
    /// Accessible to all authenticated operational roles.
    /// </summary>
    [HttpGet("{id:int}")]
    [AuthorizeRole(RoleType.NetworkAdmin, RoleType.NetworkEngineer, RoleType.ItSupport)]
    [ProducesResponseType(typeof(DeviceDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDeviceById([FromRoute] int id, CancellationToken ct)
    {
        var device = await _deviceService.GetDeviceByIdAsync(id, ct);
        return Ok(device);
    }

    /// <summary>
    /// Provisions a new monitored device with tiered equipment role authorization:
    /// - Backbone Routers / Aggregate Switches (Types 1-2): NetworkAdmin exclusively.
    /// - Access Switches (Type 3): NetworkAdmin, NetworkEngineer.
    /// - Leaf Endpoints / PCs / Servers (Types 4-5): NetworkAdmin, NetworkEngineer, ITSupport.
    /// </summary>
    [HttpPost]
    [AuthorizeRole(RoleType.NetworkAdmin, RoleType.NetworkEngineer, RoleType.ItSupport)]
    [ProducesResponseType(typeof(CreatedDeviceResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateDevice([FromBody] CreateDeviceRequest request, CancellationToken ct)
    {
        var role = GetCallerRole();
        var userId = GetCallerUserId();
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        var created = await _deviceService.CreateDeviceAsync(request, role, userId, ipAddress, ct);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    /// <summary>
    /// Updates device attributes with immutable MAC address enforcement (422 DEVICE_MAC_IMMUTABLE).
    /// Guarded by tiered equipment role authorization.
    /// </summary>
    [HttpPut("{id:int}")]
    [AuthorizeRole(RoleType.NetworkAdmin, RoleType.NetworkEngineer, RoleType.ItSupport)]
    [ProducesResponseType(typeof(UpdatedDeviceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateDevice(
        [FromRoute] int id,
        [FromBody] UpdateDeviceRequest request,
        CancellationToken ct)
    {
        var role = GetCallerRole();
        var userId = GetCallerUserId();
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        var updated = await _deviceService.UpdateDeviceAsync(id, request, role, userId, ipAddress, ct);
        return Ok(updated);
    }

    /// <summary>
    /// Safely deletes a device. Upstream switches safely orphan child devices (parent_device_id = NULL).
    /// Guarded by tiered equipment role authorization.
    /// </summary>
    [HttpDelete("{id:int}")]
    [AuthorizeRole(RoleType.NetworkAdmin, RoleType.NetworkEngineer, RoleType.ItSupport)]
    [ProducesResponseType(typeof(DeleteDeviceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteDevice([FromRoute] int id, CancellationToken ct)
    {
        var role = GetCallerRole();
        var userId = GetCallerUserId();
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        var response = await _deviceService.DeleteDeviceAsync(id, role, userId, ipAddress, ct);
        return Ok(response);
    }

    /// <summary>
    /// Toggles operational maintenance mode for a device (api_contracts_specification.md Section 8.3).
    /// Guarded by tiered equipment role authorization:
    /// - Switches & Routers: NetworkAdmin, NetworkEngineer
    /// - Leaf Endpoints: NetworkAdmin, NetworkEngineer, ITSupport
    /// </summary>
    [HttpPut("{id:int}/maintenance")]
    [AuthorizeRole(RoleType.NetworkAdmin, RoleType.NetworkEngineer, RoleType.ItSupport)]
    [ProducesResponseType(typeof(DeviceMaintenanceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ToggleMaintenance(
        [FromRoute] int id,
        [FromBody] DeviceMaintenanceRequest request,
        CancellationToken ct)
    {
        var role = GetCallerRole();
        var userId = GetCallerUserId();
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

        var response = await _deviceService.ToggleMaintenanceAsync(id, request, userId, role, ipAddress, ct);
        return Ok(response);
    }

    private int GetCallerUserId()
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdStr, out var id) ? id : 1;
    }

    private RoleType GetCallerRole()
    {
        var roleIdStr = User.FindFirst("role_id")?.Value;
        if (short.TryParse(roleIdStr, out var roleId) && Enum.IsDefined(typeof(RoleType), roleId))
        {
            return (RoleType)roleId;
        }

        var roleName = User.FindFirst(ClaimTypes.Role)?.Value;
        return roleName switch
        {
            "NetworkAdmin" => RoleType.NetworkAdmin,
            "NetworkEngineer" => RoleType.NetworkEngineer,
            "ItSupport" => RoleType.ItSupport,
            _ => throw new UnauthenticatedException("Missing or invalid operator role claim.")
        };
    }
}
