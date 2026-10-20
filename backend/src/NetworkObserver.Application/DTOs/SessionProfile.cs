namespace NetworkObserver.Application.DTOs;

/// <summary>
/// Authenticated operator session state cached in memory by the SessionManager Singleton.
/// Enforces an absolute 8-hour shift lifetime and a 30-minute idle inactivity timeout.
/// </summary>
public class SessionProfile
{
    public string Token { get; init; } = string.Empty;
    public int UserId { get; init; }
    public string Username { get; init; } = string.Empty;
    public short RoleId { get; init; }
    public int SiteId { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset LastActivityAt { get; set; }

    public SessionProfile(string token, int userId, string username, short roleId, int siteId, TimeSpan shiftLifetime)
    {
        Token = token;
        UserId = userId;
        Username = username;
        RoleId = roleId;
        SiteId = siteId;
        CreatedAt = DateTimeOffset.UtcNow;
        ExpiresAt = CreatedAt.Add(shiftLifetime);
        LastActivityAt = CreatedAt;
    }

    /// <summary>
    /// Checks whether the session has exceeded either the absolute 8-hour shift duration
    /// or the sliding 30-minute idle inactivity window.
    /// </summary>
    public bool IsExpired(DateTimeOffset now, TimeSpan maxIdleTime)
    {
        if (now >= ExpiresAt)
        {
            return true;
        }

        if (now - LastActivityAt > maxIdleTime)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Updates the sliding idle activity timestamp on each authenticated request.
    /// </summary>
    public void RefreshActivity(DateTimeOffset now)
    {
        LastActivityAt = now;
    }
}
