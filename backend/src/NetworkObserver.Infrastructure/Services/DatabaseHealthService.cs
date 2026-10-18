using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using NetworkObserver.Application.DTOs;
using NetworkObserver.Application.Interfaces;
using NetworkObserver.Infrastructure.Persistence;

namespace NetworkObserver.Infrastructure.Services;

public class DatabaseHealthService : IDatabaseHealthService
{
    private readonly NetworkObserverDbContext _dbContext;

    public DatabaseHealthService(NetworkObserverDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<DatabaseHealthDto> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);
            if (!canConnect)
            {
                sw.Stop();
                return new DatabaseHealthDto(
                    IsConnected: false,
                    DatabaseEngine: "PostgreSQL 18",
                    SiteCount: 0,
                    GroupCount: 0,
                    DeviceCount: 0,
                    UserCount: 0,
                    QueryLatencyMs: sw.ElapsedMilliseconds,
                    ErrorMessage: "Unable to establish connection to PostgreSQL instance."
                );
            }

            // Verify live ORM querying across master tables using LINQ
            var siteCount = await _dbContext.Sites.CountAsync(cancellationToken);
            var groupCount = await _dbContext.Groups.CountAsync(cancellationToken);
            var deviceCount = await _dbContext.Devices.CountAsync(cancellationToken);
            var userCount = await _dbContext.Users.CountAsync(cancellationToken);

            sw.Stop();
            return new DatabaseHealthDto(
                IsConnected: true,
                DatabaseEngine: "PostgreSQL 18",
                SiteCount: siteCount,
                GroupCount: groupCount,
                DeviceCount: deviceCount,
                UserCount: userCount,
                QueryLatencyMs: sw.ElapsedMilliseconds
            );
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new DatabaseHealthDto(
                IsConnected: false,
                DatabaseEngine: "PostgreSQL 18",
                SiteCount: 0,
                GroupCount: 0,
                DeviceCount: 0,
                UserCount: 0,
                QueryLatencyMs: sw.ElapsedMilliseconds,
                ErrorMessage: ex.Message
            );
        }
    }
}
