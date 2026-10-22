using Microsoft.EntityFrameworkCore;
using NetworkObserver.Application.DTOs;
using NetworkObserver.Application.Interfaces.Repositories;
using NetworkObserver.Domain.Entities;

namespace NetworkObserver.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of IReachabilityRepository.
/// Interacts directly with PostgreSQL 18 partitioned reachability_samples table.
/// 100% type-safe compile-time LINQ with zero raw SQL queries.
/// </summary>
public class ReachabilityRepository : IReachabilityRepository
{
    private readonly NetworkObserverDbContext _dbContext;

    public ReachabilityRepository(NetworkObserverDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddBatchAsync(IEnumerable<ReachabilitySample> samples, CancellationToken ct = default)
    {
        await _dbContext.ReachabilitySamples.AddRangeAsync(samples, ct);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task<ReachabilitySample?> GetLatestByDeviceIdAsync(int deviceId, CancellationToken ct = default)
    {
        return await _dbContext.ReachabilitySamples
            .AsNoTracking()
            .Where(s => s.DeviceId == deviceId)
            .OrderByDescending(s => s.RecordedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyDictionary<int, ReachabilitySample>> GetLatestForMonitoredDevicesAsync(CancellationToken ct = default)
    {
        // Get the latest sample for each device using an efficient grouping query
        var latestSamples = await _dbContext.ReachabilitySamples
            .AsNoTracking()
            .GroupBy(s => s.DeviceId)
            .Select(g => g.OrderByDescending(x => x.RecordedAt).First())
            .ToDictionaryAsync(s => s.DeviceId, ct);

        return latestSamples;
    }

    public async Task<IReadOnlyList<ReachabilitySample>> GetHistoryAsync(
        int deviceId,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken ct = default)
    {
        return await _dbContext.ReachabilitySamples
            .AsNoTracking()
            .Where(s => s.DeviceId == deviceId && s.RecordedAt >= start && s.RecordedAt <= end)
            .OrderBy(s => s.RecordedAt)
            .ToListAsync(ct);
    }

    public async Task<TelemetrySummaryDto> GetSummaryAsync(int totalMonitoredCount, CancellationToken ct = default)
    {
        // Compute summary counts from latest sample of each monitored device
        var latestByDevice = await _dbContext.ReachabilitySamples
            .AsNoTracking()
            .GroupBy(s => s.DeviceId)
            .Select(g => new { DeviceId = g.Key, StatusId = g.OrderByDescending(x => x.RecordedAt).First().StatusId })
            .ToListAsync(ct);

        int reachable = 0;
        int outage = 0;
        int suppressed = 0;
        int unknown = 0;

        foreach (var sample in latestByDevice)
        {
            switch (sample.StatusId)
            {
                case 1:
                    reachable++;
                    break;
                case 2:
                    outage++;
                    break;
                case 3:
                    suppressed++;
                    break;
                default:
                    unknown++;
                    break;
            }
        }

        // Any monitored device with no sample recorded yet is counted as UNKNOWN
        var devicesWithSamples = latestByDevice.Count;
        if (totalMonitoredCount > devicesWithSamples)
        {
            unknown += (totalMonitoredCount - devicesWithSamples);
        }

        var reachabilitySummary = new ReachabilitySummaryDto(
            TotalMonitored: totalMonitoredCount,
            ReachableCount: reachable,
            OutageCount: outage,
            SuppressedCount: suppressed,
            UnknownCount: unknown
        );

        // Host health summary placeholder for Step 4.4
        var hostHealthSummary = new HostHealthSummaryDto(
            TotalAgents: 0,
            HighCpuCount: 0,
            HighRamCount: 0,
            StorageWarningCount: 0
        );

        return new TelemetrySummaryDto(
            Timestamp: DateTimeOffset.UtcNow,
            Reachability: reachabilitySummary,
            HostHealth: hostHealthSummary
        );
    }
}
