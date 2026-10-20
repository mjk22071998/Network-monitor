using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using NetworkObserver.Api.Filters;
using NetworkObserver.Api.Middleware;
using NetworkObserver.Application.DTOs;
using NetworkObserver.Application.Interfaces.Security;

namespace NetworkObserver.Api.Controllers.Api.v1;

/// <summary>
/// Authentication and session lifecycle management endpoints.
/// Strictly implements api_contracts_specification.md Section 2 and security_and_rbac_specification.md.
/// Inviolable Boundary Rule: Controllers never inject DbContext; all operations route through IAuthService.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// POST /api/v1/auth/login: Authenticate operator credentials and issue session.
    /// Public endpoint (accessible without active session on enclave network).
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var response = await _authService.LoginAsync(request, ct);

        // Set HttpOnly, Secure, SameSite=Strict cookie for browser SPA clients
        Response.Cookies.Append(SessionAuthenticationMiddleware.SessionCookieName, response.Token, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Expires = response.ExpiresAt,
            Path = "/"
        });

        return Ok(response);
    }

    /// <summary>
    /// POST /api/v1/auth/logout: Revoke active operator session token immediately.
    /// Accessible by all authenticated roles.
    /// </summary>
    [HttpPost("logout")]
    [AuthorizeRole]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var token = User.FindFirst("session_token")?.Value;
        if (!string.IsNullOrWhiteSpace(token))
        {
            await _authService.LogoutAsync(token, ct);
        }

        // Clear session cookie
        Response.Cookies.Delete(SessionAuthenticationMiddleware.SessionCookieName, new CookieOptions
        {
            Path = "/"
        });

        return NoContent();
    }

    /// <summary>
    /// GET /api/v1/auth/me: Retrieve caller identity context and profile.
    /// Accessible by all authenticated roles.
    /// </summary>
    [HttpGet("me")]
    [AuthorizeRole]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCurrentUser(CancellationToken ct)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdStr, out var userId))
        {
            return Unauthorized();
        }

        var profile = await _authService.GetCurrentUserAsync(userId, ct);
        return Ok(profile);
    }

    /// <summary>
    /// PUT /api/v1/auth/password: Self-service passphrase update with verification of current passphrase.
    /// Accessible by all authenticated roles.
    /// </summary>
    [HttpPut("password")]
    [AuthorizeRole]
    [ProducesResponseType(typeof(ChangePasswordResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdStr, out var userId))
        {
            return Unauthorized();
        }

        var result = await _authService.ChangePasswordAsync(userId, request, ct);

        // Password change revokes all active sessions; clear current session cookie
        Response.Cookies.Delete(SessionAuthenticationMiddleware.SessionCookieName, new CookieOptions
        {
            Path = "/"
        });

        return Ok(result);
    }
}
