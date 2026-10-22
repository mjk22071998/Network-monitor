using NetworkObserver.Application.DTOs;
using NetworkObserver.Domain.Entities;

namespace NetworkObserver.Application.Interfaces.Repositories;

/// <summary>
/// Repository abstraction for host health operating telemetry persistence.
/// Complies with Repository Pattern and DIP. 100% EF Core LINQ.
/// </summary>
public interface IHealthRepository
{
    Task AddBatchAsync(IEnumerable<HealthSample> samples, CancellationToken ct = default);
    Task<HealthSample?> GetLatestByDeviceIdAsync(int deviceId, CancellationToken ct = default);
    Task<IReadOnlyDictionary<int, HealthSample>> GetLatestForMonitoredDevicesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<HealthSample>> GetHistoryAsync(int deviceId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct = default);
    Task<HostHealthSummaryDto> GetSummaryAsync(CancellationToken ct = default);
}
