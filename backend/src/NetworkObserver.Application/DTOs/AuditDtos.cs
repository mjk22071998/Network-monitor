namespace NetworkObserver.Application.DTOs;

/// <summary>
/// Individual immutable audit log entry complying with api_contracts_specification.md Section 11.
/// </summary>
public record AuditLogDto(
    long LogId,
    int? UserId,
    string? Username,
    string ActionCode,
    string TargetEntity,
    string? TargetId,
    string? Details,
    string IpAddress,
    DateTimeOffset LoggedAt
);

/// <summary>
/// Query parameters for paginated and filtered audit log queries.
/// </summary>
public record AuditLogQueryParameters(
    int Page = 1,
    int Limit = 50,
    int? UserId = null,
    string? ActionCode = null,
    DateTimeOffset? Start = null,
    DateTimeOffset? End = null
);

/// <summary>
/// Paginated audit log response with standard tactical pagination metadata.
/// </summary>
public record AuditLogsPagedResponse(
    IReadOnlyList<AuditLogDto> Data,
    PaginationMetadata Pagination
);
