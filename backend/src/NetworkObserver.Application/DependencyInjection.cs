using Microsoft.Extensions.DependencyInjection;
using NetworkObserver.Application.Interfaces.Security;
using NetworkObserver.Application.Services;

namespace NetworkObserver.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        return services;
    }
}
