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

        // Security, Argon2id Cryptography, and Session Management
        services.AddSingleton<IPasswordHasher, Argon2idPasswordHasher>();
        services.AddSingleton<ISessionTokenFactory, SessionTokenFactory>();
        services.AddSingleton<ISessionManager, SessionManager>();

        return services;
    }
}
