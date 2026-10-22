using NetworkObserver.Domain.Entities;

namespace NetworkObserver.Application.Interfaces.Repositories;

/// <summary>
/// Repository abstraction for global monitoring configuration persistence.
/// Complies with Repository Pattern and DIP. 100% EF Core LINQ.
/// </summary>
public interface IMonitoringConfigRepository
{
    Task<MonitoringConfig?> GetActiveConfigAsync(CancellationToken ct = default);
    Task UpdateAsync(MonitoringConfig config, CancellationToken ct = default);
}
