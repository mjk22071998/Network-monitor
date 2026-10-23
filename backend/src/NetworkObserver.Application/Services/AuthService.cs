using NetworkObserver.Application.DTOs;
using NetworkObserver.Application.Interfaces.Repositories;
using NetworkObserver.Application.Interfaces.Security;
using NetworkObserver.Domain.Constants;
using NetworkObserver.Domain.Entities;
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
    private readonly IAuditLogRepository _auditLogRepository;

    public AuthService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        ISessionManager sessionManager,
        IAuditLogRepository? auditLogRepository = null)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _sessionManager = sessionManager;
        _auditLogRepository = auditLogRepository ?? new NullAuditLogRepository();
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, string ipAddress = "127.0.0.1", CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            _passwordHasher.DummyHash();
            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserId = null,
                SiteId = 1,
                ActionCode = AuditActionCodes.AuthFailure,
                TargetEntity = AuditTargetEntities.User,
                TargetId = request.Username,
                Details = "Failed authentication attempt: empty credentials.",
                IpAddress = string.IsNullOrWhiteSpace(ipAddress) ? "127.0.0.1" : ipAddress.Trim(),
                LoggedAt = DateTimeOffset.UtcNow
            }, ct);
            throw new InvalidCredentialsException();
        }

        var user = await _userRepository.GetByUsernameAsync(request.Username.Trim(), ct);

        // Timing-attack mitigation: execute dummy hash on non-existent or inactive accounts
        if (user == null)
        {
            _passwordHasher.DummyHash();
            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserId = null,
                SiteId = 1,
                ActionCode = AuditActionCodes.AuthFailure,
                TargetEntity = AuditTargetEntities.User,
                TargetId = request.Username.Trim(),
                Details = $"Failed authentication attempt for non-existent username '{request.Username.Trim()}'.",
                IpAddress = string.IsNullOrWhiteSpace(ipAddress) ? "127.0.0.1" : ipAddress.Trim(),
                LoggedAt = DateTimeOffset.UtcNow
            }, ct);
            throw new InvalidCredentialsException();
        }

        if (!user.IsActive)
        {
            _passwordHasher.DummyHash();
            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserId = user.UserId,
                SiteId = user.SiteId,
                ActionCode = AuditActionCodes.AuthFailure,
                TargetEntity = AuditTargetEntities.User,
                TargetId = user.UserId.ToString(),
                Details = $"Failed authentication attempt for deactivated account '{user.Username}'.",
                IpAddress = string.IsNullOrWhiteSpace(ipAddress) ? "127.0.0.1" : ipAddress.Trim(),
                LoggedAt = DateTimeOffset.UtcNow
            }, ct);
            throw new AccountDeactivatedException();
        }

        var isPasswordValid = _passwordHasher.VerifyPassword(request.Password, user.PasswordHash);
        if (!isPasswordValid)
        {
            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserId = user.UserId,
                SiteId = user.SiteId,
                ActionCode = AuditActionCodes.AuthFailure,
                TargetEntity = AuditTargetEntities.User,
                TargetId = user.UserId.ToString(),
                Details = $"Failed authentication attempt: invalid passphrase for user '{user.Username}'.",
                IpAddress = string.IsNullOrWhiteSpace(ipAddress) ? "127.0.0.1" : ipAddress.Trim(),
                LoggedAt = DateTimeOffset.UtcNow
            }, ct);
            throw new InvalidCredentialsException();
        }

        // Issue new 256-bit cryptographically secure session
        var session = _sessionManager.CreateSession(user.UserId, user.Username, user.RoleId, user.SiteId);

        await _auditLogRepository.AddAsync(new AuditLog
        {
            UserId = user.UserId,
            SiteId = user.SiteId,
            ActionCode = AuditActionCodes.AuthLogin,
            TargetEntity = AuditTargetEntities.User,
            TargetId = user.UserId.ToString(),
            Details = $"Operator '{user.Username}' authenticated successfully.",
            IpAddress = string.IsNullOrWhiteSpace(ipAddress) ? "127.0.0.1" : ipAddress.Trim(),
            LoggedAt = DateTimeOffset.UtcNow
        }, ct);

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

    public async Task<ChangePasswordResponse> ChangePasswordAsync(int userId, ChangePasswordRequest request, string ipAddress = "127.0.0.1", CancellationToken ct = default)
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

        await _auditLogRepository.AddAsync(new AuditLog
        {
            UserId = user.UserId,
            SiteId = user.SiteId,
            ActionCode = AuditActionCodes.PasswordChange,
            TargetEntity = AuditTargetEntities.User,
            TargetId = user.UserId.ToString(),
            Details = $"Operator '{user.Username}' updated password. All active sessions invalidated.",
            IpAddress = string.IsNullOrWhiteSpace(ipAddress) ? "127.0.0.1" : ipAddress.Trim(),
            LoggedAt = DateTimeOffset.UtcNow
        }, ct);

        return new ChangePasswordResponse(
            Message: "Passphrase updated successfully.",
            UpdatedAt: user.UpdatedAt
        );
    }

    public async Task LogoutAsync(string token, int? userId = null, string ipAddress = "127.0.0.1", CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(token))
        {
            _sessionManager.RevokeSession(token);

            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserId = userId,
                SiteId = 1,
                ActionCode = AuditActionCodes.AuthLogout,
                TargetEntity = AuditTargetEntities.User,
                TargetId = userId?.ToString() ?? "0",
                Details = "Operator session terminated.",
                IpAddress = string.IsNullOrWhiteSpace(ipAddress) ? "127.0.0.1" : ipAddress.Trim(),
                LoggedAt = DateTimeOffset.UtcNow
            }, ct);
        }
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
