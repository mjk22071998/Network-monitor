using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NetworkObserver.Application.Interfaces;
using NetworkObserver.Application.Interfaces.Repositories;
using NetworkObserver.Application.Interfaces.Security;
using NetworkObserver.Infrastructure.Persistence;
using NetworkObserver.Infrastructure.Persistence.Repositories;
using NetworkObserver.Infrastructure.Security;
using NetworkObserver.Infrastructure.Services;

namespace NetworkObserver.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found in configuration.");

        services.AddDbContext<NetworkObserverDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__ef_migrations_history", "public");
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorCodesToAdd: null);
            }));

        services.AddScoped<IDatabaseHealthService, DatabaseHealthService>();

        // Repositories (Data Access Abstraction)
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ISiteRepository, SiteRepository>();
        services.AddScoped<IGroupRepository, GroupRepository>();
        services.AddScoped<IDeviceRepository, DeviceRepository>();
        services.AddScoped<IMonitoringConfigRepository, MonitoringConfigRepository>();
        services.AddScoped<IReachabilityRepository, ReachabilityRepository>();
        services.AddScoped<IHealthRepository, HealthRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IIngestRepository, IngestRepository>();

        // Discovery Hardware Identification (Strategy & OUI Heuristics)
        services.AddSingleton<NetworkObserver.Application.Interfaces.Discovery.IMacVendorLookup, NetworkObserver.Infrastructure.Discovery.MacVendorLookup>();
        services.AddSingleton<NetworkObserver.Application.Interfaces.Discovery.IArpResolver, NetworkObserver.Infrastructure.Discovery.WindowsNativeArpResolver>();

        // Telemetry Engine & Raw Socket Probe Factory (Strategy & Factory Patterns)
        services.AddSingleton<NetworkObserver.Application.Interfaces.Telemetry.IPingProbeFactory, NetworkObserver.Infrastructure.Telemetry.PingProbeFactory>();
        services.AddSingleton<NetworkObserver.Application.Interfaces.Telemetry.INtpProbeFactory, NetworkObserver.Infrastructure.Telemetry.NtpProbeFactory>();
        services.AddSingleton<NetworkObserver.Application.Interfaces.Telemetry.IHostHealthProbeFactory, NetworkObserver.Infrastructure.Telemetry.HostHealthProbeFactory>();

        // Security, Argon2id Cryptography, and Session Management
        services.AddSingleton<IPasswordHasher, Argon2idPasswordHasher>();
        services.AddSingleton<ISessionTokenFactory, SessionTokenFactory>();
        services.AddSingleton<ISessionManager, SessionManager>();

        return services;
    }
}
