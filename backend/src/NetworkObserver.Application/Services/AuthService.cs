using NetworkObserver.Application.DTOs;
using NetworkObserver.Application.Interfaces.Repositories;
using NetworkObserver.Application.Interfaces.Security;
using NetworkObserver.Domain.Exceptions;

namespace NetworkObserver.Application.Services;

/// <summary>
/// Application service implementing operator authentication, session issuance, and password security.
/// Conforms to Single Responsibility Principle and uses Dependency Inversion for cryptography and storage.
/// </summary>
public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ISessionManager _sessionManager;

    public AuthService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        ISessionManager sessionManager)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _sessionManager = sessionManager;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            _passwordHasher.DummyHash();
            throw new InvalidCredentialsException();
        }

        var user = await _userRepository.GetByUsernameAsync(request.Username.Trim(), ct);

        // Timing-attack mitigation: execute dummy hash on non-existent or inactive accounts
        if (user == null)
        {
            _passwordHasher.DummyHash();
            throw new InvalidCredentialsException();
        }

        if (!user.IsActive)
        {
            _passwordHasher.DummyHash();
            throw new AccountDeactivatedException();
        }

        var isPasswordValid = _passwordHasher.VerifyPassword(request.Password, user.PasswordHash);
        if (!isPasswordValid)
        {
            throw new InvalidCredentialsException();
        }

        // Issue new 256-bit cryptographically secure session
        var session = _sessionManager.CreateSession(user.UserId, user.Username, user.RoleId, user.SiteId);

        return new LoginResponse(
            Token: session.Token,
            User: new UserSummaryDto(user.UserId, user.Username, user.RoleId, user.SiteId),
            ExpiresAt: session.ExpiresAt
        );
    }

    public async Task<UserProfileDto> GetCurrentUserAsync(int userId, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user == null)
        {
            throw new UserNotFoundException(userId);
        }

        return new UserProfileDto(
            UserId: user.UserId,
            Username: user.Username,
            RoleId: user.RoleId,
            SiteId: user.SiteId,
            IsActive: user.IsActive,
            CreatedAt: user.CreatedAt
        );
    }

    public async Task<ChangePasswordResponse> ChangePasswordAsync(int userId, ChangePasswordRequest request, CancellationToken ct = default)
    {
        ValidatePasswordComplexity(request.NewPassword);

        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user == null)
        {
            throw new UserNotFoundException(userId);
        }

        // Verify current password
        var isCurrentPasswordValid = _passwordHasher.VerifyPassword(request.CurrentPassword, user.PasswordHash);
        if (!isCurrentPasswordValid)
        {
            throw new InvalidCredentialsException("Current password is incorrect.");
        }

        // Prevent password reuse
        if (request.CurrentPassword == request.NewPassword || _passwordHasher.VerifyPassword(request.NewPassword, user.PasswordHash))
        {
            throw new PasswordReuseException();
        }

        // Hash new password using Argon2id
        var newHash = _passwordHasher.HashPassword(request.NewPassword);
        user.PasswordHash = newHash;
        user.UpdatedAt = DateTimeOffset.UtcNow;

        await _userRepository.UpdateAsync(user, ct);

        // Security requirement: Invalidate all active sessions for this user upon password update
        _sessionManager.RevokeAllUserSessions(user.UserId);

        return new ChangePasswordResponse(
            Message: "Passphrase updated successfully.",
            UpdatedAt: user.UpdatedAt
        );
    }

    public Task LogoutAsync(string token, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(token))
        {
            _sessionManager.RevokeSession(token);
        }
        return Task.CompletedTask;
    }

    private static void ValidatePasswordComplexity(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 12 || password.Length > 128)
        {
            throw new PasswordComplexityException("Password must be between 12 and 128 characters.");
        }

        var hasUpper = password.Any(char.IsUpper);
        var hasLower = password.Any(char.IsLower);
        var hasDigit = password.Any(char.IsDigit);
        var hasSymbol = password.Any(ch => !char.IsLetterOrDigit(ch));

        if (!hasUpper || !hasLower || !hasDigit || !hasSymbol)
        {
            throw new PasswordComplexityException("Password must include at least one uppercase letter, one lowercase letter, one digit, and one symbol.");
        }
    }
}
