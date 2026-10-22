using Microsoft.Extensions.DependencyInjection;
using NetworkObserver.Application.Interfaces.Discovery;
using NetworkObserver.Application.Interfaces.Security;
using NetworkObserver.Application.Interfaces.Services;
using NetworkObserver.Application.Interfaces.Telemetry;
using NetworkObserver.Application.Services;

namespace NetworkObserver.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ISiteService, SiteService>();
        services.AddScoped<IGroupService, GroupService>();
        services.AddScoped<IDeviceService, DeviceService>();
        services.AddScoped<IMonitoringConfigService, MonitoringConfigService>();
        services.AddScoped<ICascadeSuppressionCircuit, CascadeSuppressionCircuit>();
        services.AddScoped<ITelemetryService, TelemetryService>();
        services.AddScoped<ISubnetDiscoveryService, SubnetDiscoveryService>();
        return services;
    }
}
