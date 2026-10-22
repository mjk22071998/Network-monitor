using Microsoft.EntityFrameworkCore;
using NetworkObserver.Application.Interfaces.Repositories;
using NetworkObserver.Domain.Entities;

namespace NetworkObserver.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of IMonitoringConfigRepository.
/// Zero raw SQL queries; 100% type-safe compile-time LINQ.
/// </summary>
public class MonitoringConfigRepository : IMonitoringConfigRepository
{
    private readonly NetworkObserverDbContext _dbContext;

    public MonitoringConfigRepository(NetworkObserverDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<MonitoringConfig?> GetActiveConfigAsync(CancellationToken ct = default)
    {
        // Enclave operates as a singleton configuration (config_id = 1)
        return await _dbContext.MonitoringConfigs.FirstOrDefaultAsync(c => c.ConfigId == 1, ct)
               ?? await _dbContext.MonitoringConfigs.FirstOrDefaultAsync(ct);
    }

    public async Task UpdateAsync(MonitoringConfig config, CancellationToken ct = default)
    {
        _dbContext.MonitoringConfigs.Update(config);
        await _dbContext.SaveChangesAsync(ct);
    }
}
