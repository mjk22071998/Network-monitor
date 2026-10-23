using NetworkObserver.Application.DTOs;
using NetworkObserver.Application.Interfaces.Repositories;
using NetworkObserver.Application.Interfaces.Security;
using NetworkObserver.Domain.Constants;
using NetworkObserver.Domain.Entities;
using NetworkObserver.Domain.Enums;
using NetworkObserver.Domain.Exceptions;

namespace NetworkObserver.Application.Services;

/// <summary>
/// Application service implementing administrative user governance and account lifecycle management.
/// Conforms to Single Responsibility Principle and enforces zero self-registration and self-deletion prevention.
/// </summary>
public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly ISiteRepository _siteRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ISessionManager _sessionManager;
    private readonly IAuditLogRepository _auditLogRepository;

    public UserService(
        IUserRepository userRepository,
        ISiteRepository siteRepository,
        IPasswordHasher passwordHasher,
        ISessionManager sessionManager,
        IAuditLogRepository? auditLogRepository = null)
    {
        _userRepository = userRepository;
        _siteRepository = siteRepository;
        _passwordHasher = passwordHasher;
        _sessionManager = sessionManager;
        _auditLogRepository = auditLogRepository ?? new NullAuditLogRepository();
    }

    public async Task<UserListResponse> GetUsersAsync(CancellationToken ct = default)
    {
        var users = await _userRepository.GetAllAsync(ct);

        var list = users.Select(u => new UserListItemDto(
            UserId: u.UserId,
            Username: u.Username,
            Role: u.Role?.RoleName ?? GetRoleDisplayName(u.RoleId),
            SiteName: u.Site?.SiteName ?? "Primary Site Enclave",
            IsActive: u.IsActive,
            CreatedAt: u.CreatedAt
        )).ToList();

        return new UserListResponse(list);
    }

    public async Task<UserDetailDto> GetUserByIdAsync(int userId, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user == null)
        {
            throw new UserNotFoundException(userId);
        }

        return new UserDetailDto(
            UserId: user.UserId,
            Username: user.Username,
            RoleId: user.RoleId,
            Role: user.Role?.RoleName ?? GetRoleDisplayName(user.RoleId),
            SiteId: user.SiteId,
            SiteName: user.Site?.SiteName ?? "Primary Site Enclave",
            IsActive: user.IsActive,
            CreatedAt: user.CreatedAt,
            UpdatedAt: user.UpdatedAt
        );
    }

    public async Task<CreateUserResponse> CreateUserAsync(CreateUserRequest request, int? adminUserId = null, string ipAddress = "127.0.0.1", CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
        {
            throw new InvalidUsernameException();
        }

        ValidateRole(request.RoleId);
        ValidatePasswordComplexity(request.Password);

        var siteExists = await _siteRepository.ExistsAsync(request.SiteId, ct);
        if (!siteExists)
        {
            throw new InvalidSiteException(request.SiteId);
        }

        var usernameExists = await _userRepository.ExistsByUsernameAsync(request.Username.Trim(), ct);
        if (usernameExists)
        {
            throw new DuplicateUsernameException(request.Username.Trim());
        }

        var passwordHash = _passwordHasher.HashPassword(request.Password);

        var user = new User
        {
            Username = request.Username.Trim(),
            PasswordHash = passwordHash,
            RoleId = request.RoleId,
            SiteId = request.SiteId,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await _userRepository.AddAsync(user, ct);

        await _auditLogRepository.AddAsync(new AuditLog
        {
            UserId = adminUserId,
            SiteId = user.SiteId,
            ActionCode = AuditActionCodes.UserCreate,
            TargetEntity = AuditTargetEntities.User,
            TargetId = user.UserId.ToString(),
            Details = $"Admin created operator account '{user.Username}' (Role ID: {user.RoleId}).",
            IpAddress = string.IsNullOrWhiteSpace(ipAddress) ? "127.0.0.1" : ipAddress.Trim(),
            LoggedAt = DateTimeOffset.UtcNow
        }, ct);

        return new CreateUserResponse(
            UserId: user.UserId,
            Username: user.Username,
            RoleId: user.RoleId,
            SiteId: user.SiteId,
            IsActive: user.IsActive,
            CreatedAt: user.CreatedAt
        );
    }

    public async Task<UpdateUserResponse> UpdateUserAsync(int targetUserId, UpdateUserRequest request, int? adminUserId = null, string ipAddress = "127.0.0.1", CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdAsync(targetUserId, ct);
        if (user == null)
        {
            throw new UserNotFoundException(targetUserId);
        }

        var sessionInvalidationRequired = false;

        if (request.RoleId.HasValue && request.RoleId.Value != user.RoleId)
        {
            ValidateRole(request.RoleId.Value);
            user.RoleId = request.RoleId.Value;
            sessionInvalidationRequired = true;
        }

        if (request.SiteId.HasValue && request.SiteId.Value != user.SiteId)
        {
            var siteExists = await _siteRepository.ExistsAsync(request.SiteId.Value, ct);
            if (!siteExists)
            {
                throw new InvalidSiteException(request.SiteId.Value);
            }
            user.SiteId = request.SiteId.Value;
        }

        if (request.IsActive.HasValue && request.IsActive.Value != user.IsActive)
        {
            user.IsActive = request.IsActive.Value;
            if (!user.IsActive)
            {
                sessionInvalidationRequired = true;
            }
        }

        if (!string.IsNullOrWhiteSpace(request.ResetPassword))
        {
            ValidatePasswordComplexity(request.ResetPassword);
            user.PasswordHash = _passwordHasher.HashPassword(request.ResetPassword);
            sessionInvalidationRequired = true;
        }

        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _userRepository.UpdateAsync(user, ct);

        if (sessionInvalidationRequired)
        {
            _sessionManager.RevokeAllUserSessions(user.UserId);
        }

        await _auditLogRepository.AddAsync(new AuditLog
        {
            UserId = adminUserId,
            SiteId = user.SiteId,
            ActionCode = AuditActionCodes.UserUpdate,
            TargetEntity = AuditTargetEntities.User,
            TargetId = user.UserId.ToString(),
            Details = $"Admin updated operator account '{user.Username}' (Role ID: {user.RoleId}, Active: {user.IsActive}).",
            IpAddress = string.IsNullOrWhiteSpace(ipAddress) ? "127.0.0.1" : ipAddress.Trim(),
            LoggedAt = DateTimeOffset.UtcNow
        }, ct);

        return new UpdateUserResponse(
            UserId: user.UserId,
            Username: user.Username,
            RoleId: user.RoleId,
            SiteId: user.SiteId,
            IsActive: user.IsActive,
            UpdatedAt: user.UpdatedAt
        );
    }

    public async Task DeleteUserAsync(int targetUserId, int currentUserId, string ipAddress = "127.0.0.1", CancellationToken ct = default)
    {
        if (targetUserId == currentUserId)
        {
            throw new SelfDeletionException();
        }

        var user = await _userRepository.GetByIdAsync(targetUserId, ct);
        if (user == null)
        {
            throw new UserNotFoundException(targetUserId);
        }

        // Revoke all existing sessions for this deleted operator
        _sessionManager.RevokeAllUserSessions(targetUserId);

        await _userRepository.DeleteAsync(user, ct);

        await _auditLogRepository.AddAsync(new AuditLog
        {
            UserId = currentUserId,
            SiteId = user.SiteId,
            ActionCode = AuditActionCodes.UserDelete,
            TargetEntity = AuditTargetEntities.User,
            TargetId = targetUserId.ToString(),
            Details = $"Admin deleted operator account '{user.Username}' (ID: {targetUserId}).",
            IpAddress = string.IsNullOrWhiteSpace(ipAddress) ? "127.0.0.1" : ipAddress.Trim(),
            LoggedAt = DateTimeOffset.UtcNow
        }, ct);
    }

    private static void ValidateRole(short roleId)
    {
        if (!Enum.IsDefined(typeof(RoleType), roleId))
        {
            throw new InvalidRoleException(roleId);
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

    private static string GetRoleDisplayName(short roleId) => roleId switch
    {
        1 => "Network Admin",
        2 => "Network Engineer",
        3 => "IT Support",
        _ => "Unknown Role"
    };
}
