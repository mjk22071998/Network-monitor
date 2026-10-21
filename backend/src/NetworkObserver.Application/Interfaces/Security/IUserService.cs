using NetworkObserver.Application.DTOs;

namespace NetworkObserver.Application.Interfaces.Security;

/// <summary>
/// Application service abstraction for user administration and account provisioning.
/// Accessible exclusively by NETWORK_ADMIN operators.
/// </summary>
public interface IUserService
{
    Task<UserListResponse> GetUsersAsync(CancellationToken ct = default);
    Task<UserDetailDto> GetUserByIdAsync(int userId, CancellationToken ct = default);
    Task<CreateUserResponse> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default);
    Task<UpdateUserResponse> UpdateUserAsync(int targetUserId, UpdateUserRequest request, CancellationToken ct = default);
    Task DeleteUserAsync(int targetUserId, int currentUserId, CancellationToken ct = default);
}
