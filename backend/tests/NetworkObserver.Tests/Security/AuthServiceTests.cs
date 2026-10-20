using NetworkObserver.Application.DTOs;
using NetworkObserver.Application.Interfaces.Repositories;
using NetworkObserver.Application.Interfaces.Security;
using NetworkObserver.Application.Services;
using NetworkObserver.Domain.Entities;
using NetworkObserver.Domain.Exceptions;
using NetworkObserver.Infrastructure.Security;
using Xunit;

namespace NetworkObserver.Tests.Security;

public class AuthServiceTests
{
    private readonly InMemoryUserRepository _userRepo;
    private readonly Argon2idPasswordHasher _hasher;
    private readonly SessionTokenFactory _tokenFactory;
    private readonly SessionManager _sessionManager;
    private readonly AuthService _authService;

    public AuthServiceTests()
    {
        _userRepo = new InMemoryUserRepository();
        _hasher = new Argon2idPasswordHasher();
        _tokenFactory = new SessionTokenFactory();
        _sessionManager = new SessionManager(_tokenFactory);
        _authService = new AuthService(_userRepo, _hasher, _sessionManager);

        // Seed initial test user
        var passwordHash = _hasher.HashPassword("Admin@NetworkObserver2026!");
        _userRepo.AddSync(new User
        {
            UserId = 1,
            SiteId = 1,
            Username = "admin",
            PasswordHash = passwordHash,
            RoleId = 1,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        // Seed deactivated user
        _userRepo.AddSync(new User
        {
            UserId = 2,
            SiteId = 1,
            Username = "deactivated_user",
            PasswordHash = passwordHash,
            RoleId = 3,
            IsActive = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
    }

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsLoginResponseAndSessionToken()
    {
        // Arrange
        var request = new LoginRequest("admin", "Admin@NetworkObserver2026!");

        // Act
        var response = await _authService.LoginAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.StartsWith("obs_tok_", response.Token);
        Assert.Equal(1, response.User.UserId);
        Assert.Equal("admin", response.User.Username);
        Assert.Equal(1, response.User.RoleId);
        Assert.Equal(1, response.User.SiteId);
        Assert.Equal(1, _sessionManager.ActiveSessionCount);
    }

    [Fact]
    public async Task LoginAsync_WithInvalidPassword_ThrowsInvalidCredentialsException()
    {
        // Arrange
        var request = new LoginRequest("admin", "WrongPassword123!");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidCredentialsException>(() => _authService.LoginAsync(request));
        Assert.Equal(0, _sessionManager.ActiveSessionCount);
    }

    [Fact]
    public async Task LoginAsync_WithNonExistentUser_ThrowsInvalidCredentialsException()
    {
        // Arrange
        var request = new LoginRequest("ghost_operator", "AnyPassword123!");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidCredentialsException>(() => _authService.LoginAsync(request));
    }

    [Fact]
    public async Task LoginAsync_WithDeactivatedUser_ThrowsAccountDeactivatedException()
    {
        // Arrange
        var request = new LoginRequest("deactivated_user", "Admin@NetworkObserver2026!");

        // Act & Assert
        await Assert.ThrowsAsync<AccountDeactivatedException>(() => _authService.LoginAsync(request));
    }

    [Fact]
    public async Task GetCurrentUserAsync_WithValidUserId_ReturnsUserProfile()
    {
        // Act
        var profile = await _authService.GetCurrentUserAsync(1);

        // Assert
        Assert.NotNull(profile);
        Assert.Equal(1, profile.UserId);
        Assert.Equal("admin", profile.Username);
        Assert.Equal(1, profile.RoleId);
        Assert.True(profile.IsActive);
    }

    [Fact]
    public async Task ChangePasswordAsync_WithValidPasswords_UpdatesPasswordAndRevokesSessions()
    {
        // Arrange - create active session for user 1
        var login = await _authService.LoginAsync(new LoginRequest("admin", "Admin@NetworkObserver2026!"));
        Assert.NotNull(_sessionManager.ValidateSession(login.Token));

        var changeRequest = new ChangePasswordRequest(
            CurrentPassword: "Admin@NetworkObserver2026!",
            NewPassword: "BrandNewSecurePassword2026#"
        );

        // Act
        var response = await _authService.ChangePasswordAsync(1, changeRequest);

        // Assert
        Assert.NotNull(response);
        Assert.Equal("Passphrase updated successfully.", response.Message);

        // Previous session must be revoked
        Assert.Null(_sessionManager.ValidateSession(login.Token));

        // Can log in with new password
        var newLogin = await _authService.LoginAsync(new LoginRequest("admin", "BrandNewSecurePassword2026#"));
        Assert.NotNull(newLogin);
        Assert.Equal(1, newLogin.User.UserId);
    }

    [Fact]
    public async Task ChangePasswordAsync_WithWeakPassword_ThrowsPasswordComplexityException()
    {
        // Arrange (less than 12 characters)
        var request = new ChangePasswordRequest(
            CurrentPassword: "Admin@NetworkObserver2026!",
            NewPassword: "Short1!"
        );

        // Act & Assert
        await Assert.ThrowsAsync<PasswordComplexityException>(() => _authService.ChangePasswordAsync(1, request));
    }

    [Fact]
    public async Task ChangePasswordAsync_WithSamePassword_ThrowsPasswordReuseException()
    {
        // Arrange
        var request = new ChangePasswordRequest(
            CurrentPassword: "Admin@NetworkObserver2026!",
            NewPassword: "Admin@NetworkObserver2026!"
        );

        // Act & Assert
        await Assert.ThrowsAsync<PasswordReuseException>(() => _authService.ChangePasswordAsync(1, request));
    }

    [Fact]
    public async Task LogoutAsync_RevokesActiveSession()
    {
        // Arrange
        var login = await _authService.LoginAsync(new LoginRequest("admin", "Admin@NetworkObserver2026!"));
        Assert.Equal(1, _sessionManager.ActiveSessionCount);

        // Act
        await _authService.LogoutAsync(login.Token);

        // Assert
        Assert.Equal(0, _sessionManager.ActiveSessionCount);
        Assert.Null(_sessionManager.ValidateSession(login.Token));
    }
}

/// <summary>
/// Fast in-memory stub of IUserRepository for testing.
/// </summary>
internal class InMemoryUserRepository : IUserRepository
{
    private readonly List<User> _users = new();

    public void AddSync(User user) => _users.Add(user);

    public Task<User?> GetByIdAsync(int userId, CancellationToken ct = default)
    {
        return Task.FromResult(_users.FirstOrDefault(u => u.UserId == userId));
    }

    public Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default)
    {
        return Task.FromResult(_users.FirstOrDefault(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase)));
    }

    public Task<IReadOnlyList<User>> GetAllAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<User>>(_users.ToList());
    }

    public Task AddAsync(User user, CancellationToken ct = default)
    {
        _users.Add(user);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(User user, CancellationToken ct = default)
    {
        var existingIndex = _users.FindIndex(u => u.UserId == user.UserId);
        if (existingIndex >= 0)
        {
            _users[existingIndex] = user;
        }
        return Task.CompletedTask;
    }

    public Task DeleteAsync(User user, CancellationToken ct = default)
    {
        _users.RemoveAll(u => u.UserId == user.UserId);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsByUsernameAsync(string username, CancellationToken ct = default)
    {
        return Task.FromResult(_users.Any(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase)));
    }
}
