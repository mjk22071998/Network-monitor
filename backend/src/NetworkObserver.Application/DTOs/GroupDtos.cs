namespace NetworkObserver.Application.DTOs;

/// <summary>
/// Authoritative Group & Subgroup hierarchy DTOs complying with
/// api_contracts_specification.md Section 5.
/// </summary>
public record GroupTreeNodeDto(
    int GroupId,
    int SiteId,
    int? ParentGroupId,
    string GroupName,
    string? Description,
    int DeviceCount,
    IReadOnlyList<GroupTreeNodeDto> Children
);

public record GroupTreeResponse(
    IReadOnlyList<GroupTreeNodeDto> Groups
);

public record CreateGroupRequest(
    string GroupName,
    int? ParentGroupId,
    string? Description
);

public record GroupDetailDto(
    int GroupId,
    int SiteId,
    int? ParentGroupId,
    string GroupName,
    string? Description,
    DateTimeOffset CreatedAt
);

public record UpdateGroupRequest(
    string GroupName,
    int? ParentGroupId,
    string? Description
);

public record DeleteGroupResponse(
    int DeletedGroupId,
    int ReassignedDevicesCount,
    int TargetGroupId,
    string TargetGroupName
);
