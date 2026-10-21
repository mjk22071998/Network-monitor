using Microsoft.AspNetCore.Mvc;
using NetworkObserver.Api.Filters;
using NetworkObserver.Application.DTOs;
using NetworkObserver.Application.Interfaces.Services;
using NetworkObserver.Domain.Enums;

namespace NetworkObserver.Api.Controllers.Api.v1;

/// <summary>
/// Group & Subgroup hierarchy management controller.
/// Enforces Master RBAC matrix rules and adheres to api_contracts_specification.md Section 5.
/// Boundary Rule: Controllers never inject DbContext; all operations route through IGroupService.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public class GroupsController : ControllerBase
{
    private readonly IGroupService _groupService;

    public GroupsController(IGroupService groupService)
    {
        _groupService = groupService;
    }

    /// <summary>
    /// Retrieves full recursive group tree or subtree with children: [...] hierarchy and direct device counts.
    /// Accessible to all authenticated operational roles (Admin, Engineer, IT Support).
    /// </summary>
    [HttpGet]
    [AuthorizeRole(RoleType.NetworkAdmin, RoleType.NetworkEngineer, RoleType.ItSupport)]
    [ProducesResponseType(typeof(GroupTreeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetGroups([FromQuery] int? parent_id, CancellationToken ct)
    {
        var response = await _groupService.GetGroupTreeAsync(parent_id, ct);
        return Ok(response);
    }

    /// <summary>
    /// Creates a new group or subgroup.
    /// Accessible to NetworkAdmin and NetworkEngineer.
    /// </summary>
    [HttpPost]
    [AuthorizeRole(RoleType.NetworkAdmin, RoleType.NetworkEngineer)]
    [ProducesResponseType(typeof(GroupDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateGroup([FromBody] CreateGroupRequest request, CancellationToken ct)
    {
        var created = await _groupService.CreateGroupAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    /// <summary>
    /// Renames, moves, or updates descriptions for an existing group or subgroup.
    /// Protected root Group 1 cannot be reparented. Cyclic dependencies strictly rejected.
    /// Accessible to NetworkAdmin and NetworkEngineer.
    /// </summary>
    [HttpPut("{id:int}")]
    [AuthorizeRole(RoleType.NetworkAdmin, RoleType.NetworkEngineer)]
    [ProducesResponseType(typeof(GroupDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateGroup(int id, [FromBody] UpdateGroupRequest request, CancellationToken ct)
    {
        var updated = await _groupService.UpdateGroupAsync(id, request, ct);
        return Ok(updated);
    }

    /// <summary>
    /// Safely removes a group. Member devices are automatically reassigned to Group 1 (Unassigned Group).
    /// Child subgroups are reparented. Root Group 1 cannot be deleted.
    /// Accessible to NetworkAdmin and NetworkEngineer.
    /// </summary>
    [HttpDelete("{id:int}")]
    [AuthorizeRole(RoleType.NetworkAdmin, RoleType.NetworkEngineer)]
    [ProducesResponseType(typeof(DeleteGroupResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteGroup(int id, CancellationToken ct)
    {
        var response = await _groupService.DeleteGroupAsync(id, ct);
        return Ok(response);
    }
}
