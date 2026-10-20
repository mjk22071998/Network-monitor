using System.Collections.Concurrent;
using NetworkObserver.Application.DTOs;
using NetworkObserver.Application.Interfaces.Security;

namespace NetworkObserver.Infrastructure.Security;

/// <summary>
/// Singleton Pattern implementation of ISessionManager.
/// Provides thread-safe in-memory caching of active operator sessions with sliding idle timeouts
/// and absolute shift expirations, avoiding database queries on hot API request paths.
/// </summary>
public class SessionManager : ISessionManager
{
    private readonly ISessionTokenFactory _tokenFactory;
    private readonly ConcurrentDictionary<string, SessionProfile> _sessions = new();
    private readonly TimeSpan _shiftLifetime;
    private readonly TimeSpan _idleTimeout;

    public SessionManager(
        ISessionTokenFactory tokenFactory,
        TimeSpan? shiftLifetime = null,
        TimeSpan? idleTimeout = null)
    {
        _tokenFactory = tokenFactory;
        _shiftLifetime = shiftLifetime ?? TimeSpan.FromHours(8);
        _idleTimeout = idleTimeout ?? TimeSpan.FromMinutes(30);
    }

    public int ActiveSessionCount => _sessions.Count;

    public SessionProfile CreateSession(int userId, string username, short roleId, int siteId)
    {
        // Periodic lightweight cleanup if session count exceeds modest threshold
        if (_sessions.Count > 1000)
        {
            PruneExpiredSessions();
        }

        var token = _tokenFactory.GenerateToken();
        var session = new SessionProfile(token, userId, username, roleId, siteId, _shiftLifetime);
        _sessions[token] = session;
        return session;
    }

    public SessionProfile? ValidateSession(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        if (!_sessions.TryGetValue(token, out var session))
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        if (session.IsExpired(now, _idleTimeout))
        {
            _sessions.TryRemove(token, out _);
            return null;
        }

        // Sliding idle window update
        session.RefreshActivity(now);
        return session;
    }

    public bool RevokeSession(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        return _sessions.TryRemove(token, out _);
    }

    public int RevokeAllUserSessions(int userId)
    {
        var revokedCount = 0;
        foreach (var kvp in _sessions)
        {
            if (kvp.Value.UserId == userId)
            {
                if (_sessions.TryRemove(kvp.Key, out _))
                {
                    revokedCount++;
                }
            }
        }
        return revokedCount;
    }

    private void PruneExpiredSessions()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var kvp in _sessions)
        {
            if (kvp.Value.IsExpired(now, _idleTimeout))
            {
                _sessions.TryRemove(kvp.Key, out _);
            }
        }
    }
}
