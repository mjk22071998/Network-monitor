using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using NetworkObserver.Api.Filters;
using NetworkObserver.Application.DTOs;
using NetworkObserver.Application.Interfaces.Security;
using NetworkObserver.Domain.Enums;

namespace NetworkObserver.Api.Controllers.Api.v1;

/// <summary>
/// Operator account governance and provisioning controller.
/// Enforces NETWORK_ADMIN role restriction across all endpoints per security_and_rbac_specification.md Section 3.
/// Inviolable Boundary Rule: Controllers never inject DbContext; all operations route through IUserService.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[AuthorizeRole(RoleType.NetworkAdmin)]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    /// <summary>
    /// GET /api/v1/users: List all operator accounts in the enclave.
    /// Accessible exclusively by NETWORK_ADMIN.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(UserListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetUsers(CancellationToken ct)
    {
        var response = await _userService.GetUsersAsync(ct);
        return Ok(response);
    }

    /// <summary>
    /// GET /api/v1/users/{id}: Retrieve detailed profile of a specific operator.
    /// Accessible exclusively by NETWORK_ADMIN.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(UserDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserById([FromRoute] int id, CancellationToken ct)
    {
        var user = await _userService.GetUserByIdAsync(id, ct);
        return Ok(user);
    }

    /// <summary>
    /// POST /api/v1/users: Pre-provision a new operator account with assigned role and temporary password.
    /// Accessible exclusively by NETWORK_ADMIN. Zero public self-registration.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CreateUserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        var result = await _userService.CreateUserAsync(request, ct);
        return Created($"/api/v1/users/{result.UserId}", result);
    }

    /// <summary>
    /// PUT /api/v1/users/{id}: Update operator role, site, active state, or administratively reset password.
    /// Accessible exclusively by NETWORK_ADMIN.
    /// </summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(UpdateUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateUser([FromRoute] int id, [FromBody] UpdateUserRequest request, CancellationToken ct)
    {
        var result = await _userService.UpdateUserAsync(id, request, ct);
        return Ok(result);
    }

    /// <summary>
    /// DELETE /api/v1/users/{id}: Deactivate or delete operator account.
    /// Accessible exclusively by NETWORK_ADMIN. Prevents self-deletion of active administrator.
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteUser([FromRoute] int id, CancellationToken ct)
    {
        var currentUserIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(currentUserIdStr, out var currentUserId))
        {
            return Unauthorized();
        }

        await _userService.DeleteUserAsync(id, currentUserId, ct);
        return NoContent();
    }
}
