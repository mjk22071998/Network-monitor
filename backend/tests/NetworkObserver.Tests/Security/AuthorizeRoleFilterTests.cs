using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using NetworkObserver.Api.Filters;
using NetworkObserver.Application.DTOs;
using NetworkObserver.Domain.Enums;
using Xunit;

namespace NetworkObserver.Tests.Security;

public class AuthorizeRoleFilterTests
{
    private static AuthorizationFilterContext CreateContext(ClaimsPrincipal user)
    {
        var httpContext = new DefaultHttpContext { User = user };
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());
    }

    [Fact]
    public async Task OnAuthorizationAsync_WhenUnauthenticated_Returns401Unauthorized()
    {
        // Arrange
        var filter = new AuthorizeRoleAttribute(RoleType.NetworkAdmin);
        var unauthenticatedUser = new ClaimsPrincipal(new ClaimsIdentity()); // IsAuthenticated = false
        var context = CreateContext(unauthenticatedUser);

        // Act
        await filter.OnAuthorizationAsync(context);

        // Assert
        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        var error = Assert.IsType<ErrorResponse>(result.Value);
        Assert.Equal("AUTH_UNAUTHORIZED", error.Error.Code);
    }

    [Fact]
    public async Task OnAuthorizationAsync_WhenRoleIsInsufficient_Returns403Forbidden()
    {
        // Arrange - User has IT_SUPPORT role but endpoint requires NETWORK_ADMIN
        var filter = new AuthorizeRoleAttribute(RoleType.NetworkAdmin);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "3"),
            new Claim(ClaimTypes.Name, "technician"),
            new Claim(ClaimTypes.Role, RoleType.ItSupport.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var context = CreateContext(new ClaimsPrincipal(identity));

        // Act
        await filter.OnAuthorizationAsync(context);

        // Assert
        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        var error = Assert.IsType<ErrorResponse>(result.Value);
        Assert.Equal("AUTH_FORBIDDEN", error.Error.Code);
    }

    [Fact]
    public async Task OnAuthorizationAsync_WhenRoleIsAllowed_PermitsExecution()
    {
        // Arrange - User has NETWORK_ADMIN role and endpoint requires NETWORK_ADMIN
        var filter = new AuthorizeRoleAttribute(RoleType.NetworkAdmin, RoleType.NetworkEngineer);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Name, "admin"),
            new Claim(ClaimTypes.Role, RoleType.NetworkAdmin.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var context = CreateContext(new ClaimsPrincipal(identity));

        // Act
        await filter.OnAuthorizationAsync(context);

        // Assert - Null result means execution is permitted to proceed
        Assert.Null(context.Result);
    }

    [Fact]
    public async Task OnAuthorizationAsync_WithNoRolesSpecified_AllowsAnyAuthenticatedUser()
    {
        // Arrange - Endpoint allows any authenticated user
        var filter = new AuthorizeRoleAttribute();
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "2"),
            new Claim(ClaimTypes.Name, "any_user"),
            new Claim(ClaimTypes.Role, RoleType.ItSupport.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var context = CreateContext(new ClaimsPrincipal(identity));

        // Act
        await filter.OnAuthorizationAsync(context);

        // Assert
        Assert.Null(context.Result);
    }
}
