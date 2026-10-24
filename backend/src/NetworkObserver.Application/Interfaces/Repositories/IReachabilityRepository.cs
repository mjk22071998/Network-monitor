using NetworkObserver.Application.DTOs;
using NetworkObserver.Domain.Entities;

namespace NetworkObserver.Application.Interfaces.Repositories;

/// <summary>
/// Repository abstraction for Reachability telemetry samples and range partition persistence.
/// Complies with Repository Pattern and DIP. 100% EF Core LINQ.
/// </summary>
public interface IReachabilityRepository
{
    Task AddBatchAsync(IEnumerable<ReachabilitySample> samples, CancellationToken ct = default);
    Task<ReachabilitySample?> GetLatestByDeviceIdAsync(int deviceId, CancellationToken ct = default);
    Task<IReadOnlyDictionary<int, ReachabilitySample>> GetLatestForMonitoredDevicesAsync(CancellationToken ct = default);
    Task<IReadOnlyDictionary<int, ReachabilitySample>> GetLatestForMonitoredDevicesAsync(IReadOnlyList<int> deviceIds, CancellationToken ct = default);
    Task<IReadOnlyList<ReachabilitySample>> GetHistoryAsync(int deviceId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct = default);
    Task<TelemetrySummaryDto> GetSummaryAsync(int totalMonitoredCount, CancellationToken ct = default);
}
