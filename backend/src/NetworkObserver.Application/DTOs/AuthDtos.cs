namespace NetworkObserver.Application.DTOs;

public record LoginRequest(string Username, string Password);

public record UserSummaryDto(int UserId, string Username, short RoleId, int SiteId);

public record LoginResponse(string Token, UserSummaryDto User, DateTimeOffset ExpiresAt);

public record UserProfileDto(int UserId, string Username, short RoleId, int SiteId, bool IsActive, DateTimeOffset CreatedAt);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public record ChangePasswordResponse(string Message, DateTimeOffset UpdatedAt);

public record ErrorDetail(string Code, string Message, object? Details, DateTimeOffset Timestamp);

public record ErrorResponse(ErrorDetail Error);
