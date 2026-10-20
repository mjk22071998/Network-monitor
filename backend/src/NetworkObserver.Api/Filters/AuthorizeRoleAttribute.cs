using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NetworkObserver.Application.DTOs;
using NetworkObserver.Domain.Enums;

namespace NetworkObserver.Api.Filters;

/// <summary>
/// Authoritative Role-Based Access Control filter enforcing Master RBAC Route Authorization Matrix
/// defined in security_and_rbac_specification.md Section 4.
/// Returns 401 Unauthorized with standard error envelope when unauthenticated.
/// Returns 403 Forbidden with standard error envelope when role is insufficient.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public class AuthorizeRoleAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly RoleType[] _allowedRoles;

    public AuthorizeRoleAttribute(params RoleType[] allowedRoles)
    {
        _allowedRoles = allowedRoles;
    }

    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        // 1. Verify authenticated identity
        if (context.HttpContext.User.Identity?.IsAuthenticated != true)
        {
            context.Result = new ObjectResult(new ErrorResponse(
                new ErrorDetail(
                    Code: "AUTH_UNAUTHORIZED",
                    Message: "Authentication required. Active session token missing or expired.",
                    Details: null,
                    Timestamp: DateTimeOffset.UtcNow
                )
            ))
            {
                StatusCode = StatusCodes.Status401Unauthorized
            };
            return Task.CompletedTask;
        }

        // If no specific roles are required, any valid authenticated session is permitted
        if (_allowedRoles.Length == 0)
        {
            return Task.CompletedTask;
        }

        // 2. Verify Role Claim against allowed roles
        var roleClaim = context.HttpContext.User.FindFirst(ClaimTypes.Role)?.Value;
        if (string.IsNullOrWhiteSpace(roleClaim) ||
            !Enum.TryParse<RoleType>(roleClaim, true, out var userRole) ||
            !_allowedRoles.Contains(userRole))
        {
            context.Result = new ObjectResult(new ErrorResponse(
                new ErrorDetail(
                    Code: "AUTH_FORBIDDEN",
                    Message: "User does not have sufficient role permissions for this operational endpoint.",
                    Details: new
                    {
                        required_roles = _allowedRoles.Select(r => r.ToString()).ToArray(),
                        user_role = roleClaim
                    },
                    Timestamp: DateTimeOffset.UtcNow
                )
            ))
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return Task.CompletedTask;
        }

        return Task.CompletedTask;
    }
}
