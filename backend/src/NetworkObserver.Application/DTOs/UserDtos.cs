namespace NetworkObserver.Application.DTOs;

public record UserListItemDto(
    int UserId,
    string Username,
    string Role,
    string SiteName,
    bool IsActive,
    DateTimeOffset CreatedAt
);

public record UserListResponse(IReadOnlyList<UserListItemDto> Users);

public record CreateUserRequest(
    string Username,
    string Password,
    short RoleId,
    int SiteId
);

public record CreateUserResponse(
    int UserId,
    string Username,
    short RoleId,
    int SiteId,
    bool IsActive,
    DateTimeOffset CreatedAt
);

public record UpdateUserRequest(
    short? RoleId,
    int? SiteId,
    bool? IsActive,
    string? ResetPassword
);

public record UpdateUserResponse(
    int UserId,
    string Username,
    short RoleId,
    int SiteId,
    bool IsActive,
    DateTimeOffset UpdatedAt
);

public record UserDetailDto(
    int UserId,
    string Username,
    short RoleId,
    string Role,
    int SiteId,
    string SiteName,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);
