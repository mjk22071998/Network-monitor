using NetworkObserver.Application.DTOs;
using NetworkObserver.Application.Interfaces.Repositories;
using NetworkObserver.Application.Services;
using NetworkObserver.Domain.Entities;
using NetworkObserver.Domain.Enums;
using NetworkObserver.Domain.Exceptions;
using NetworkObserver.Infrastructure.Security;
using Xunit;

namespace NetworkObserver.Tests.Security;

public class UserServiceTests
{
    private readonly InMemoryUserRepository _userRepo;
    private readonly InMemorySiteRepository _siteRepo;
    private readonly Argon2idPasswordHasher _hasher;
    private readonly SessionTokenFactory _tokenFactory;
    private readonly SessionManager _sessionManager;
    private readonly UserService _userService;

    public UserServiceTests()
    {
        _userRepo = new InMemoryUserRepository();
        _siteRepo = new InMemorySiteRepository();
        _hasher = new Argon2idPasswordHasher();
        _tokenFactory = new SessionTokenFactory();
        _sessionManager = new SessionManager(_tokenFactory);
        _userService = new UserService(_userRepo, _siteRepo, _hasher, _sessionManager);

        // Seed site 1
        _siteRepo.AddSync(new Site
        {
            SiteId = 1,
            SiteName = "Primary Site Enclave",
            FacilityCode = "SITE-01"
        });

        // Seed default admin
        var adminHash = _hasher.HashPassword("Admin@NetworkObserver2026!");
        _userRepo.AddSync(new User
        {
            UserId = 1,
            SiteId = 1,
            Username = "admin",
            PasswordHash = adminHash,
            RoleId = 1,
            IsActive = true,
            Role = new Role { RoleId = 1, RoleName = "Network Admin", RoleCode = "NETWORK_ADMIN" },
            Site = new Site { SiteId = 1, SiteName = "Primary Site Enclave" }
        });
    }

    [Fact]
    public async Task GetUsersAsync_ReturnsAllUsersWithRoleAndSiteNames()
    {
        // Act
        var response = await _userService.GetUsersAsync();

        // Assert
        Assert.NotNull(response);
        Assert.Single(response.Users);
        var user = response.Users[0];
        Assert.Equal("admin", user.Username);
        Assert.Equal("Network Admin", user.Role);
        Assert.Equal("Primary Site Enclave", user.SiteName);
    }

    [Fact]
    public async Task GetUserByIdAsync_WithValidId_ReturnsUserDetail()
    {
        // Act
        var user = await _userService.GetUserByIdAsync(1);

        // Assert
        Assert.NotNull(user);
        Assert.Equal(1, user.UserId);
        Assert.Equal("admin", user.Username);
        Assert.Equal(1, user.RoleId);
        Assert.Equal("Network Admin", user.Role);
    }

    [Fact]
    public async Task GetUserByIdAsync_WithInvalidId_ThrowsUserNotFoundException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<UserNotFoundException>(() => _userService.GetUserByIdAsync(999));
    }

    [Fact]
    public async Task CreateUserAsync_WithValidRequest_HashesPasswordAndPersists()
    {
        // Arrange
        var request = new CreateUserRequest(
            Username: "eng_lead",
            Password: "SecureEngineerPass2026!",
            RoleId: 2,
            SiteId: 1
        );

        // Act
        var result = await _userService.CreateUserAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("eng_lead", result.Username);
        Assert.Equal(2, result.RoleId);
        Assert.Equal(1, result.SiteId);
        Assert.True(result.IsActive);

        var persisted = await _userRepo.GetByUsernameAsync("eng_lead");
        Assert.NotNull(persisted);
        Assert.True(_hasher.VerifyPassword("SecureEngineerPass2026!", persisted.PasswordHash));
    }

    [Fact]
    public async Task CreateUserAsync_WithDuplicateUsername_ThrowsDuplicateUsernameException()
    {
        // Arrange - "admin" already exists
        var request = new CreateUserRequest("admin", "AnotherSecurePass2026!", 2, 1);

        // Act & Assert
        await Assert.ThrowsAsync<DuplicateUsernameException>(() => _userService.CreateUserAsync(request));
    }

    [Fact]
    public async Task CreateUserAsync_WithInvalidRole_ThrowsInvalidRoleException()
    {
        // Arrange - role 99 is invalid
        var request = new CreateUserRequest("new_user", "ValidSecurePassword2026!", 99, 1);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidRoleException>(() => _userService.CreateUserAsync(request));
    }

    [Fact]
    public async Task CreateUserAsync_WithInvalidSite_ThrowsInvalidSiteException()
    {
        // Arrange - site 404 does not exist
        var request = new CreateUserRequest("new_user", "ValidSecurePassword2026!", 2, 404);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidSiteException>(() => _userService.CreateUserAsync(request));
    }

    [Fact]
    public async Task CreateUserAsync_WithWeakPassword_ThrowsPasswordComplexityException()
    {
        // Arrange - weak password (too short)
        var request = new CreateUserRequest("new_user", "weakpass", 2, 1);

        // Act & Assert
        await Assert.ThrowsAsync<PasswordComplexityException>(() => _userService.CreateUserAsync(request));
    }

    [Fact]
    public async Task UpdateUserAsync_WithRoleChange_UpdatesRoleAndInvalidatesSessions()
    {
        // Arrange - create user 2 and active session
        var user2 = new User { UserId = 2, Username = "operator2", RoleId = 3, SiteId = 1, PasswordHash = "hash" };
        _userRepo.AddSync(user2);
        var session = _sessionManager.CreateSession(2, "operator2", 3, 1);
        Assert.NotNull(_sessionManager.ValidateSession(session.Token));

        var updateRequest = new UpdateUserRequest(RoleId: 2, SiteId: null, IsActive: null, ResetPassword: null);

        // Act
        var result = await _userService.UpdateUserAsync(2, updateRequest);

        // Assert
        Assert.Equal(2, result.RoleId);
        // Active session must be revoked when role is modified
        Assert.Null(_sessionManager.ValidateSession(session.Token));
    }

    [Fact]
    public async Task UpdateUserAsync_WithPasswordReset_HashesNewPasswordAndInvalidatesSessions()
    {
        // Arrange
        var user3 = new User { UserId = 3, Username = "operator3", RoleId = 3, SiteId = 1, PasswordHash = "oldhash" };
        _userRepo.AddSync(user3);
        var session = _sessionManager.CreateSession(3, "operator3", 3, 1);

        var updateRequest = new UpdateUserRequest(
            RoleId: null,
            SiteId: null,
            IsActive: null,
            ResetPassword: "AdminResetPassphrase2026!"
        );

        // Act
        var result = await _userService.UpdateUserAsync(3, updateRequest);

        // Assert
        Assert.NotNull(result);
        var updated = await _userRepo.GetByIdAsync(3);
        Assert.NotNull(updated);
        Assert.True(_hasher.VerifyPassword("AdminResetPassphrase2026!", updated.PasswordHash));
        Assert.Null(_sessionManager.ValidateSession(session.Token));
    }

    [Fact]
    public async Task DeleteUserAsync_WhenTargetIsCurrentUser_ThrowsSelfDeletionException()
    {
        // Act & Assert - Admin cannot delete self (currentUserId = 1, targetUserId = 1)
        await Assert.ThrowsAsync<SelfDeletionException>(() => _userService.DeleteUserAsync(1, 1));
    }

    [Fact]
    public async Task DeleteUserAsync_WhenTargetIsDifferentUser_DeletesAndInvalidatesSessions()
    {
        // Arrange
        var user4 = new User { UserId = 4, Username = "temp_user", RoleId = 3, SiteId = 1, PasswordHash = "hash" };
        _userRepo.AddSync(user4);
        var session = _sessionManager.CreateSession(4, "temp_user", 3, 1);
        Assert.Equal(1, _sessionManager.ActiveSessionCount);

        // Act - Admin (1) deletes User (4)
        await _userService.DeleteUserAsync(4, 1);

        // Assert
        Assert.Null(await _userRepo.GetByIdAsync(4));
        Assert.Equal(0, _sessionManager.ActiveSessionCount);
        Assert.Null(_sessionManager.ValidateSession(session.Token));
    }
}

/// <summary>
/// Fast in-memory stub of ISiteRepository for testing.
/// </summary>
internal class InMemorySiteRepository : ISiteRepository
{
    private readonly List<Site> _sites = new();

    public void AddSync(Site site) => _sites.Add(site);

    public Task<Site?> GetByIdAsync(int siteId, CancellationToken ct = default)
    {
        return Task.FromResult(_sites.FirstOrDefault(s => s.SiteId == siteId));
    }

    public Task<bool> ExistsAsync(int siteId, CancellationToken ct = default)
    {
        return Task.FromResult(_sites.Any(s => s.SiteId == siteId));
    }
}
