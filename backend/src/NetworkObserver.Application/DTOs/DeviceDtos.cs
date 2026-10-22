namespace NetworkObserver.Application.DTOs;

/// <summary>
/// Authoritative Device Inventory DTOs complying with api_contracts_specification.md Section 6.
/// </summary>
public record LatestReachabilityDto(
    short StatusId,
    string StatusCode,
    int? LatencyUs,
    decimal LossPercentage,
    int? NtpOffsetUs,
    bool IsSuppressed,
    DateTimeOffset RecordedAt
);

public record LatestHealthDto(
    short StatusId,
    string StatusCode,
    decimal? CpuUtilizationPct,
    decimal? RamUtilizationPct,
    DateTimeOffset RecordedAt
);

public record DeviceListItemDto(
    int DeviceId,
    string MacAddress,
    string PrimaryIp,
    string DisplayName,
    short TypeId,
    string TypeCode,
    int GroupId,
    string GroupName,
    int? ParentDeviceId,
    string? ParentDeviceName,
    string? Vendor,
    string? Model,
    string? Zone,
    bool IsMonitored,
    LatestReachabilityDto? LatestReachability,
    LatestHealthDto? LatestHealth
);

public record PaginationMetadata(
    int TotalRecords,
    int CurrentPage,
    int PageSize,
    int TotalPages,
    bool HasNext,
    bool HasPrev
);

public record PagedResponse<T>(
    IReadOnlyList<T> Data,
    PaginationMetadata Pagination
);

public record DeviceDetailDto(
    int DeviceId,
    string MacAddress,
    string PrimaryIp,
    string DisplayName,
    short TypeId,
    string TypeCode,
    int GroupId,
    string GroupName,
    int? ParentDeviceId,
    string? ParentDeviceName,
    string? Vendor,
    string? Model,
    string? Zone,
    bool IsMonitored,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    LatestReachabilityDto? LatestReachability,
    LatestHealthDto? LatestHealth
);

public record CreateDeviceRequest(
    string DisplayName,
    string PrimaryIp,
    string MacAddress,
    short TypeId,
    int GroupId,
    int? ParentDeviceId = null,
    string? Vendor = null,
    string? Model = null,
    string? Zone = null,
    bool IsMonitored = true
);

public record CreatedDeviceResponse(
    int DeviceId,
    string MacAddress,
    string PrimaryIp,
    string DisplayName,
    short TypeId,
    int GroupId,
    int? ParentDeviceId,
    string? Vendor,
    string? Model,
    string? Zone,
    bool IsMonitored,
    DateTimeOffset CreatedAt
);

public record UpdateDeviceRequest(
    string DisplayName,
    string PrimaryIp,
    short TypeId,
    int GroupId,
    int? ParentDeviceId = null,
    string? Vendor = null,
    string? Model = null,
    string? Zone = null,
    bool IsMonitored = true,
    string? MacAddress = null
);

public record UpdatedDeviceResponse(
    int DeviceId,
    string MacAddress,
    string PrimaryIp,
    string DisplayName,
    short TypeId,
    int GroupId,
    int? ParentDeviceId,
    string? Vendor,
    string? Model,
    string? Zone,
    bool IsMonitored,
    DateTimeOffset UpdatedAt
);

public record DeleteDeviceResponse(
    int DeletedDeviceId,
    string DeletedDisplayName,
    int OrphanedChildrenCount,
    IReadOnlyList<int> OrphanedChildrenDeviceIds
);

public record DeviceMaintenanceRequest(
    bool IsMonitored,
    string? Reason = null
);

public record DeviceMaintenanceResponse(
    int DeviceId,
    bool IsMonitored,
    string StatusCode,
    long AuditLogId
);
