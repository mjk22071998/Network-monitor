using NetworkObserver.Infrastructure.Security;
using Xunit;

namespace NetworkObserver.Tests.Security;

public class SessionManagementTests
{
    private readonly SessionTokenFactory _tokenFactory = new();

    [Fact]
    public void SessionTokenFactory_ShouldProduce256BitHexTokenWithObsPrefix()
    {
        // Act
        var token = _tokenFactory.GenerateToken();

        // Assert
        Assert.NotNull(token);
        Assert.StartsWith("obs_tok_", token);
        // "obs_tok_" (8 chars) + 64 hex characters (32 bytes = 256 bits) = 72 characters
        Assert.Equal(72, token.Length);
    }

    [Fact]
    public void SessionTokenFactory_ShouldGenerateUniqueTokens()
    {
        // Act
        var token1 = _tokenFactory.GenerateToken();
        var token2 = _tokenFactory.GenerateToken();

        // Assert
        Assert.NotEqual(token1, token2);
    }

    [Fact]
    public void CreateSession_ShouldReturnActiveSessionProfileWith8HourExpiry()
    {
        // Arrange
        var sessionManager = new SessionManager(_tokenFactory);

        // Act
        var session = sessionManager.CreateSession(userId: 1, username: "admin", roleId: 1, siteId: 1);

        // Assert
        Assert.NotNull(session);
        Assert.Equal(1, session.UserId);
        Assert.Equal("admin", session.Username);
        Assert.Equal(1, session.RoleId);
        Assert.Equal(1, session.SiteId);
        Assert.True(session.ExpiresAt > session.CreatedAt);
        Assert.True(session.ExpiresAt <= session.CreatedAt.AddHours(8).AddSeconds(2));
        Assert.Equal(1, sessionManager.ActiveSessionCount);
    }

    [Fact]
    public void ValidateSession_WithValidToken_ShouldReturnSessionAndRefreshActivity()
    {
        // Arrange
        var sessionManager = new SessionManager(_tokenFactory);
        var created = sessionManager.CreateSession(userId: 2, username: "engineer", roleId: 2, siteId: 1);
        var initialActivity = created.LastActivityAt;

        // Act
        var validated = sessionManager.ValidateSession(created.Token);

        // Assert
        Assert.NotNull(validated);
        Assert.Equal(created.Token, validated.Token);
        Assert.Equal("engineer", validated.Username);
        Assert.True(validated.LastActivityAt >= initialActivity);
    }

    [Fact]
    public void ValidateSession_WithInvalidToken_ShouldReturnNull()
    {
        // Arrange
        var sessionManager = new SessionManager(_tokenFactory);

        // Act
        var validated = sessionManager.ValidateSession("obs_tok_invalidnonexistenttoken12345678901234567890");

        // Assert
        Assert.Null(validated);
    }

    [Fact]
    public void RevokeSession_ShouldRemoveSessionFromActiveRegistry()
    {
        // Arrange
        var sessionManager = new SessionManager(_tokenFactory);
        var session = sessionManager.CreateSession(userId: 3, username: "tech", roleId: 3, siteId: 1);

        // Act
        var revoked = sessionManager.RevokeSession(session.Token);
        var validatedAfterRevoke = sessionManager.ValidateSession(session.Token);

        // Assert
        Assert.True(revoked);
        Assert.Null(validatedAfterRevoke);
        Assert.Equal(0, sessionManager.ActiveSessionCount);
    }

    [Fact]
    public void RevokeAllUserSessions_ShouldRemoveAllSessionsForTargetUser()
    {
        // Arrange
        var sessionManager = new SessionManager(_tokenFactory);
        var session1 = sessionManager.CreateSession(userId: 10, username: "userA", roleId: 2, siteId: 1);
        var session2 = sessionManager.CreateSession(userId: 10, username: "userA", roleId: 2, siteId: 1);
        var session3 = sessionManager.CreateSession(userId: 20, username: "userB", roleId: 3, siteId: 1);

        Assert.Equal(3, sessionManager.ActiveSessionCount);

        // Act
        var revokedCount = sessionManager.RevokeAllUserSessions(userId: 10);

        // Assert
        Assert.Equal(2, revokedCount);
        Assert.Null(sessionManager.ValidateSession(session1.Token));
        Assert.Null(sessionManager.ValidateSession(session2.Token));
        Assert.NotNull(sessionManager.ValidateSession(session3.Token));
        Assert.Equal(1, sessionManager.ActiveSessionCount);
    }

    [Fact]
    public void ValidateSession_WithIdleTimeout_ShouldReturnNullAndPrune()
    {
        // Arrange - custom session manager with 10 millisecond idle timeout
        var shortIdleTimeout = TimeSpan.FromMilliseconds(10);
        var sessionManager = new SessionManager(_tokenFactory, idleTimeout: shortIdleTimeout);
        var session = sessionManager.CreateSession(userId: 5, username: "idleUser", roleId: 3, siteId: 1);

        // Wait past idle timeout
        Thread.Sleep(30);

        // Act
        var validated = sessionManager.ValidateSession(session.Token);

        // Assert
        Assert.Null(validated);
        Assert.Equal(0, sessionManager.ActiveSessionCount);
    }
}
