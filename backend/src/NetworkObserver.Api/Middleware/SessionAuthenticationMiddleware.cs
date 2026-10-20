using System.Security.Claims;
using NetworkObserver.Application.Interfaces.Security;
using NetworkObserver.Domain.Enums;

namespace NetworkObserver.Api.Middleware;

/// <summary>
/// Intercepts inbound HTTP requests and authenticates active sessions from either
/// the 'observer_session' HttpOnly cookie or the 'Authorization: Bearer <token>' header.
/// Attaches authenticated ClaimsPrincipal with role, user ID, and site ID context.
/// </summary>
public class SessionAuthenticationMiddleware
{
    public const string SessionCookieName = "observer_session";
    private readonly RequestDelegate _next;

    public SessionAuthenticationMiddleware(RequestDelegate _next)
    {
        this._next = _next;
    }

    public async Task InvokeAsync(HttpContext context, ISessionManager sessionManager)
    {
        var token = ExtractToken(context);

        if (!string.IsNullOrWhiteSpace(token))
        {
            var session = sessionManager.ValidateSession(token);
            if (session != null)
            {
                var role = Enum.IsDefined(typeof(RoleType), session.RoleId)
                    ? ((RoleType)session.RoleId).ToString()
                    : "UNKNOWN";

                var claims = new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, session.UserId.ToString()),
                    new Claim(ClaimTypes.Name, session.Username),
                    new Claim(ClaimTypes.Role, role),
                    new Claim("role_id", session.RoleId.ToString()),
                    new Claim("site_id", session.SiteId.ToString()),
                    new Claim("session_token", session.Token)
                };

                var identity = new ClaimsIdentity(claims, "ObserverSession");
                context.User = new ClaimsPrincipal(identity);
                context.Items["SessionProfile"] = session;
            }
        }

        await _next(context);
    }

    private static string? ExtractToken(HttpContext context)
    {
        // 1. Primary: HttpOnly secure cookie for web browser SPA
        if (context.Request.Cookies.TryGetValue(SessionCookieName, out var cookieToken) &&
            !string.IsNullOrWhiteSpace(cookieToken))
        {
            return cookieToken.Trim();
        }

        // 2. Secondary: Authorization: Bearer header for headless CLI/automation
        if (context.Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            var headerValue = authHeader.ToString().Trim();
            if (headerValue.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                var bearerToken = headerValue["Bearer ".Length..].Trim();
                if (!string.IsNullOrWhiteSpace(bearerToken))
                {
                    return bearerToken;
                }
            }
        }

        return null;
    }
}
