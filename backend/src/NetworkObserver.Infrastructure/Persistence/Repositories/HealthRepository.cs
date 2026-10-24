using Microsoft.EntityFrameworkCore;
using NetworkObserver.Application.DTOs;
using NetworkObserver.Application.Interfaces.Repositories;
using NetworkObserver.Domain.Entities;

namespace NetworkObserver.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of IHealthRepository.
/// Interacts directly with PostgreSQL 18 partitioned health_samples and disk_volume_samples tables.
/// 100% type-safe compile-time LINQ with zero raw SQL queries.
/// </summary>
public class HealthRepository : IHealthRepository
{
    private readonly NetworkObserverDbContext _dbContext;

    public HealthRepository(NetworkObserverDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddBatchAsync(IEnumerable<HealthSample> samples, CancellationToken ct = default)
    {
        var sampleList = samples.ToList();
        await _dbContext.HealthSamples.AddRangeAsync(sampleList, ct);
        await _dbContext.SaveChangesAsync(ct);

        // If any samples have child disk volume records, link and persist them
        var diskVolumes = new List<DiskVolumeSample>();
        foreach (var sample in sampleList)
        {
            if (sample.DiskVolumeSamples.Count > 0)
            {
                foreach (var volume in sample.DiskVolumeSamples)
                {
                    volume.SampleId = sample.SampleId;
                    diskVolumes.Add(volume);
                }
            }
        }

        if (diskVolumes.Count > 0)
        {
            await _dbContext.DiskVolumeSamples.AddRangeAsync(diskVolumes, ct);
            await _dbContext.SaveChangesAsync(ct);
        }
    }

    public async Task<HealthSample?> GetLatestByDeviceIdAsync(int deviceId, CancellationToken ct = default)
    {
        var sample = await _dbContext.HealthSamples
            .AsNoTracking()
            .Where(hs => hs.DeviceId == deviceId)
            .OrderByDescending(hs => hs.RecordedAt)
            .FirstOrDefaultAsync(ct);

        if (sample != null)
        {
            var volumes = await _dbContext.DiskVolumeSamples
                .AsNoTracking()
                .Where(dvs => dvs.SampleId == sample.SampleId)
                .ToListAsync(ct);
            sample.DiskVolumeSamples = volumes;
        }

        return sample;
    }

    public Task<IReadOnlyDictionary<int, HealthSample>> GetLatestForMonitoredDevicesAsync(CancellationToken ct = default)
        => GetLatestForMonitoredDevicesAsync(null, ct);

    public async Task<IReadOnlyDictionary<int, HealthSample>> GetLatestForMonitoredDevicesAsync(
        IReadOnlyList<int>? deviceIds,
        CancellationToken ct = default)
    {
        var query = _dbContext.HealthSamples.AsNoTracking();
        if (deviceIds != null && deviceIds.Count > 0)
        {
            query = query.Where(hs => deviceIds.Contains(hs.DeviceId));
        }

        var latestSamples = await query
            .GroupBy(hs => hs.DeviceId)
            .Select(g => g.OrderByDescending(x => x.RecordedAt).First())
            .ToListAsync(ct);

        var sampleIds = latestSamples.Select(s => s.SampleId).ToList();
        var allVolumes = await _dbContext.DiskVolumeSamples
            .AsNoTracking()
            .Where(d => sampleIds.Contains(d.SampleId))
            .ToListAsync(ct);

        var volumesBySample = allVolumes.GroupBy(v => v.SampleId).ToDictionary(g => g.Key, g => g.ToList());
        foreach (var sample in latestSamples)
        {
            if (volumesBySample.TryGetValue(sample.SampleId, out var vols))
            {
                sample.DiskVolumeSamples = vols;
            }
        }

        return latestSamples.ToDictionary(hs => hs.DeviceId);
    }

    public async Task<IReadOnlyList<HealthSample>> GetHistoryAsync(
        int deviceId,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken ct = default)
    {
        var samples = await _dbContext.HealthSamples
            .AsNoTracking()
            .Where(hs => hs.DeviceId == deviceId && hs.RecordedAt >= start && hs.RecordedAt <= end)
            .OrderBy(hs => hs.RecordedAt)
            .ToListAsync(ct);

        var sampleIds = samples.Select(s => s.SampleId).ToList();
        var allVolumes = await _dbContext.DiskVolumeSamples
            .AsNoTracking()
            .Where(d => sampleIds.Contains(d.SampleId))
            .ToListAsync(ct);

        var volumesBySample = allVolumes.GroupBy(v => v.SampleId).ToDictionary(g => g.Key, g => g.ToList());
        foreach (var sample in samples)
        {
            if (volumesBySample.TryGetValue(sample.SampleId, out var vols))
            {
                sample.DiskVolumeSamples = vols;
            }
        }

        return samples;
    }

    public async Task<HostHealthSummaryDto> GetSummaryAsync(CancellationToken ct = default)
    {
        var latestSamples = await _dbContext.HealthSamples
            .AsNoTracking()
            .GroupBy(hs => hs.DeviceId)
            .Select(g => new
            {
                SampleId = g.OrderByDescending(x => x.RecordedAt).First().SampleId,
                StatusId = g.OrderByDescending(x => x.RecordedAt).First().StatusId,
                CpuUtilizationPct = g.OrderByDescending(x => x.RecordedAt).First().CpuUtilizationPct,
                RamUtilizationPct = g.OrderByDescending(x => x.RecordedAt).First().RamUtilizationPct
            })
            .ToListAsync(ct);

        var availableSampleIds = latestSamples
            .Where(s => s.StatusId == 1)
            .Select(s => s.SampleId)
            .ToList();

        var diskWarnings = await _dbContext.DiskVolumeSamples
            .AsNoTracking()
            .Where(d => availableSampleIds.Contains(d.SampleId) && d.UsedPercentage >= 90.0f)
            .Select(d => d.SampleId)
            .Distinct()
            .CountAsync(ct);

        int totalAgents = 0;
        int highCpu = 0;
        int highRam = 0;

        foreach (var sample in latestSamples)
        {
            if (sample.StatusId == 1) // AVAILABLE
            {
                totalAgents++;

                if (sample.CpuUtilizationPct >= 90.0f)
                {
                    highCpu++;
                }

                if (sample.RamUtilizationPct >= 90.0f)
                {
                    highRam++;
                }
            }
        }

        return new HostHealthSummaryDto(
            TotalAgents: totalAgents,
            HighCpuCount: highCpu,
            HighRamCount: highRam,
            StorageWarningCount: diskWarnings
        );
    }
}
