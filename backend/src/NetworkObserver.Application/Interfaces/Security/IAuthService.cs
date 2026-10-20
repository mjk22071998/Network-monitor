using NetworkObserver.Application.DTOs;

namespace NetworkObserver.Application.Interfaces.Security;

/// <summary>
/// Application service contract for operator authentication and password management.
/// Single Responsibility: Orchestrates authentication workflows, security rules, and token issuance.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Authenticates operator credentials against the Argon2id hash in users table.
    /// Issues a new 256-bit session token upon success.
    /// Executes dummy Argon2id calculation on inactive or non-existent accounts to defeat timing attacks.
    /// </summary>
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);

    /// <summary>
    /// Retrieves user profile and permissions for the currently authenticated session.
    /// </summary>
    Task<UserProfileDto> GetCurrentUserAsync(int userId, CancellationToken ct = default);

    /// <summary>
    /// Authenticated self-service password update.
    /// Validates current password, enforces password complexity rules, prevents password reuse,
    /// and invalidates all existing active sessions.
    /// </summary>
    Task<ChangePasswordResponse> ChangePasswordAsync(int userId, ChangePasswordRequest request, CancellationToken ct = default);

    /// <summary>
    /// Terminates an active operator session, revoking the token from memory.
    /// </summary>
    Task LogoutAsync(string token, CancellationToken ct = default);
}
