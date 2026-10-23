using NetworkObserver.Application.DTOs;
using NetworkObserver.Application.Interfaces.Repositories;
using NetworkObserver.Application.Interfaces.Services;
using NetworkObserver.Domain.Constants;
using NetworkObserver.Domain.Entities;
using NetworkObserver.Domain.Exceptions;

namespace NetworkObserver.Application.Services;

/// <summary>
/// Domain application service implementing unified recursive group hierarchy operations.
/// Enforces circular-reference guards, root Group 1 protection, and device reassignment cascades.
/// </summary>
public class GroupService : IGroupService
{
    private const int RootUnassignedGroupId = 1;
    private readonly IGroupRepository _groupRepository;
    private readonly IAuditLogRepository _auditLogRepository;

    public GroupService(
        IGroupRepository groupRepository,
        IAuditLogRepository? auditLogRepository = null)
    {
        _groupRepository = groupRepository;
        _auditLogRepository = auditLogRepository ?? new NullAuditLogRepository();
    }

    public async Task<GroupTreeResponse> GetGroupTreeAsync(int? parentId = null, CancellationToken ct = default)
    {
        var allGroups = await _groupRepository.GetAllAsync(ct);
        var deviceCounts = await _groupRepository.GetDeviceCountsByGroupAsync(ct);

        // Group entities by ParentGroupId for fast O(N) recursive tree generation
        var lookup = allGroups.ToLookup(g => g.ParentGroupId);

        List<GroupTreeNodeDto> BuildSubtree(int? currentParentId)
        {
            var nodes = new List<GroupTreeNodeDto>();
            foreach (var group in lookup[currentParentId])
            {
                var count = deviceCounts.TryGetValue(group.GroupId, out var c) ? c : 0;
                var children = BuildSubtree(group.GroupId);
                nodes.Add(new GroupTreeNodeDto(
                    GroupId: group.GroupId,
                    SiteId: group.SiteId,
                    ParentGroupId: group.ParentGroupId,
                    GroupName: group.GroupName,
                    Description: group.Description,
                    DeviceCount: count,
                    Children: children
                ));
            }
            return nodes;
        }

        var tree = BuildSubtree(parentId);
        return new GroupTreeResponse(tree);
    }

    public async Task<GroupDetailDto> CreateGroupAsync(CreateGroupRequest request, int? userId = null, string ipAddress = "127.0.0.1", CancellationToken ct = default)
    {
        ValidateGroupName(request.GroupName);

        if (request.ParentGroupId.HasValue)
        {
            var parentExists = await _groupRepository.ExistsAsync(request.ParentGroupId.Value, ct);
            if (!parentExists)
            {
                throw new InvalidParentGroupException(request.ParentGroupId.Value);
            }
        }

        var group = new Group
        {
            GroupName = request.GroupName.Trim(),
            ParentGroupId = request.ParentGroupId,
            Description = request.Description?.Trim(),
            SiteId = 1,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _groupRepository.AddAsync(group, ct);

        await _auditLogRepository.AddAsync(new AuditLog
        {
            UserId = userId,
            SiteId = 1,
            ActionCode = AuditActionCodes.GroupCreate,
            TargetEntity = AuditTargetEntities.Group,
            TargetId = group.GroupId.ToString(),
            Details = $"Created group '{group.GroupName}' (Parent: {group.ParentGroupId?.ToString() ?? "None"}).",
            IpAddress = string.IsNullOrWhiteSpace(ipAddress) ? "127.0.0.1" : ipAddress.Trim(),
            LoggedAt = DateTimeOffset.UtcNow
        }, ct);

        return new GroupDetailDto(
            GroupId: group.GroupId,
            SiteId: group.SiteId,
            ParentGroupId: group.ParentGroupId,
            GroupName: group.GroupName,
            Description: group.Description,
            CreatedAt: group.CreatedAt
        );
    }

    public async Task<GroupDetailDto> UpdateGroupAsync(int groupId, UpdateGroupRequest request, int? userId = null, string ipAddress = "127.0.0.1", CancellationToken ct = default)
    {
        ValidateGroupName(request.GroupName);

        if (groupId == RootUnassignedGroupId)
        {
            if (request.ParentGroupId != null)
            {
                throw new ProtectedGroupException("Root Group 1 (Unassigned Group) must remain top-level and cannot have a parent group.");
            }
        }

        var group = await _groupRepository.GetByIdAsync(groupId, ct);
        if (group == null)
        {
            throw new GroupNotFoundException(groupId);
        }

        if (request.ParentGroupId.HasValue)
        {
            var newParentId = request.ParentGroupId.Value;

            if (newParentId == groupId)
            {
                throw new CircularGroupHierarchyException("A group cannot be set as its own parent.");
            }

            var parentExists = await _groupRepository.ExistsAsync(newParentId, ct);
            if (!parentExists)
            {
                throw new InvalidParentGroupException(newParentId);
            }

            // Cycle detection: ensure newParentId is not a descendant of groupId
            var allGroups = await _groupRepository.GetAllAsync(ct);
            var groupDict = allGroups.ToDictionary(g => g.GroupId);

            var current = newParentId;
            while (groupDict.TryGetValue(current, out var ancestor))
            {
                if (ancestor.ParentGroupId == null)
                {
                    break;
                }
                if (ancestor.ParentGroupId.Value == groupId)
                {
                    throw new CircularGroupHierarchyException("Setting this parent would create a cyclic dependency in the group hierarchy.");
                }
                current = ancestor.ParentGroupId.Value;
            }
        }

        group.GroupName = request.GroupName.Trim();
        group.ParentGroupId = request.ParentGroupId;
        group.Description = request.Description?.Trim();

        await _groupRepository.UpdateAsync(group, ct);

        await _auditLogRepository.AddAsync(new AuditLog
        {
            UserId = userId,
            SiteId = 1,
            ActionCode = AuditActionCodes.GroupUpdate,
            TargetEntity = AuditTargetEntities.Group,
            TargetId = group.GroupId.ToString(),
            Details = $"Updated group '{group.GroupName}' (Parent: {group.ParentGroupId?.ToString() ?? "None"}).",
            IpAddress = string.IsNullOrWhiteSpace(ipAddress) ? "127.0.0.1" : ipAddress.Trim(),
            LoggedAt = DateTimeOffset.UtcNow
        }, ct);

        return new GroupDetailDto(
            GroupId: group.GroupId,
            SiteId: group.SiteId,
            ParentGroupId: group.ParentGroupId,
            GroupName: group.GroupName,
            Description: group.Description,
            CreatedAt: group.CreatedAt
        );
    }

    public async Task<DeleteGroupResponse> DeleteGroupAsync(int groupId, int? userId = null, string ipAddress = "127.0.0.1", CancellationToken ct = default)
    {
        if (groupId == RootUnassignedGroupId)
        {
            throw new ProtectedGroupException("System root Group 1 (Unassigned Group) is protected and cannot be deleted.");
        }

        var group = await _groupRepository.GetByIdAsync(groupId, ct);
        if (group == null)
        {
            throw new GroupNotFoundException(groupId);
        }

        var reassignedCount = await _groupRepository.DeleteAndReassignAsync(groupId, RootUnassignedGroupId, ct);

        await _auditLogRepository.AddAsync(new AuditLog
        {
            UserId = userId,
            SiteId = 1,
            ActionCode = AuditActionCodes.GroupDelete,
            TargetEntity = AuditTargetEntities.Group,
            TargetId = groupId.ToString(),
            Details = $"Deleted group '{group.GroupName}'. Reassigned {reassignedCount} devices to Group 1.",
            IpAddress = string.IsNullOrWhiteSpace(ipAddress) ? "127.0.0.1" : ipAddress.Trim(),
            LoggedAt = DateTimeOffset.UtcNow
        }, ct);

        return new DeleteGroupResponse(
            DeletedGroupId: groupId,
            ReassignedDevicesCount: reassignedCount,
            TargetGroupId: RootUnassignedGroupId,
            TargetGroupName: "Unassigned Group"
        );
    }

    private static void ValidateGroupName(string groupName)
    {
        if (string.IsNullOrWhiteSpace(groupName))
        {
            throw new GroupValidationException("Group name is required and cannot be empty.");
        }

        if (groupName.Trim().Length > 100)
        {
            throw new GroupValidationException("Group name cannot exceed 100 characters.");
        }
    }
}
