using NetworkObserver.Application.DTOs;

namespace NetworkObserver.Application.Interfaces.Security;

/// <summary>
/// Singleton session manager interface for thread-safe in-memory operator session management.
/// Responsibilities: Issue sessions, validate tokens with sliding idle expiration, and revoke sessions.
/// </summary>
public interface ISessionManager
{
    /// <summary>
    /// Issues a new active session token for the authenticated operator.
    /// </summary>
    SessionProfile CreateSession(int userId, string username, short roleId, int siteId);

    /// <summary>
    /// Validates an incoming session token.
    /// Returns the active SessionProfile if valid and not expired, refreshing its sliding idle activity timestamp.
    /// Returns null if missing, invalid, or expired.
    /// </summary>
    SessionProfile? ValidateSession(string token);

    /// <summary>
    /// Revokes an individual session token immediately (e.g. on operator logout).
    /// </summary>
    bool RevokeSession(string token);

    /// <summary>
    /// Revokes all active session tokens belonging to a given user ID
    /// (e.g. on self password update, admin role mutation, or account deactivation).
    /// </summary>
    int RevokeAllUserSessions(int userId);

    /// <summary>
    /// Returns the current count of active cached sessions.
    /// </summary>
    int ActiveSessionCount { get; }
}
