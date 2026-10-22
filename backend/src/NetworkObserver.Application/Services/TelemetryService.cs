using System.Collections.Concurrent;
using NetworkObserver.Application.DTOs;
using NetworkObserver.Application.Interfaces.Repositories;
using NetworkObserver.Application.Interfaces.Services;
using NetworkObserver.Application.Interfaces.Telemetry;
using NetworkObserver.Domain.Entities;
using NetworkObserver.Domain.Exceptions;

namespace NetworkObserver.Application.Services;

/// <summary>
/// Domain application service coordinating high-performance multithreaded network telemetry,
/// native raw socket ICMP polling sweeps, and time-series aggregation.
/// Complies with GoF Strategy & Factory patterns and the 500MB bounded memory budget.
/// </summary>
public class TelemetryService : ITelemetryService
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly IReachabilityRepository _reachabilityRepository;
    private readonly IMonitoringConfigRepository _configRepository;
    private readonly IPingProbeFactory _probeFactory;

    public TelemetryService(
        IDeviceRepository deviceRepository,
        IReachabilityRepository reachabilityRepository,
        IMonitoringConfigRepository configRepository,
        IPingProbeFactory probeFactory)
    {
        _deviceRepository = deviceRepository;
        _reachabilityRepository = reachabilityRepository;
        _configRepository = configRepository;
        _probeFactory = probeFactory;
    }

    public async Task<PollSweepResultDto> ExecuteReachabilitySweepAsync(CancellationToken ct = default)
    {
        var config = await _configRepository.GetActiveConfigAsync(ct);
        var timeoutMs = config?.ReachabilityTimeoutMs ?? 2000;

        // Fetch all monitored devices in enclave
        var (devices, _) = await _deviceRepository.GetPagedAsync(new DeviceQueryParameters(Limit: 1000), ct);
        var monitoredDevices = devices.Where(d => d.IsMonitored).ToList();

        if (monitoredDevices.Count == 0)
        {
            return new PollSweepResultDto(0, 0, 0, DateTimeOffset.UtcNow);
        }

        var samples = new ConcurrentBag<ReachabilitySample>();
        var pingStrategy = _probeFactory.CreateStrategy();

        // Parallel non-blocking execution bounded to 50 concurrent sockets (bounded memory budget)
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = 50,
            CancellationToken = ct
        };

        var now = DateTimeOffset.UtcNow;

        await Parallel.ForEachAsync(monitoredDevices, parallelOptions, async (device, token) =>
        {
            var result = await pingStrategy.PingAsync(device.PrimaryIp, timeoutMs, token);

            var sample = new ReachabilitySample
            {
                DeviceId = device.DeviceId,
                RecordedAt = now,
                StatusId = result.IsSuccess ? (short)1 : (short)2, // 1 = REACHABLE, 2 = UNREACHABLE
                LatencyUs = result.LatencyUs,                      // Strictly NULL on failure (never 0)
                LossPercentage = result.LossPercentage,
                NtpOffsetUs = null,
                IsSuppressed = false
            };

            samples.Add(sample);
        });

        var sampleList = samples.ToList();
        await _reachabilityRepository.AddBatchAsync(sampleList, ct);

        var reachableCount = sampleList.Count(s => s.StatusId == 1);
        var unreachableCount = sampleList.Count(s => s.StatusId == 2);

        return new PollSweepResultDto(
            PolledCount: sampleList.Count,
            ReachableCount: reachableCount,
            UnreachableCount: unreachableCount,
            CompletedAt: DateTimeOffset.UtcNow
        );
    }

    public async Task<TelemetrySummaryDto> GetSummaryAsync(CancellationToken ct = default)
    {
        var (devices, totalMonitored) = await _deviceRepository.GetPagedAsync(new DeviceQueryParameters(Limit: 1), ct);
        return await _reachabilityRepository.GetSummaryAsync(totalMonitored, ct);
    }

    public async Task<DeviceLiveTelemetryDto> GetLiveTelemetryAsync(int deviceId, CancellationToken ct = default)
    {
        var device = await _deviceRepository.GetByIdAsync(deviceId, ct);
        if (device == null)
        {
            throw new DeviceNotFoundException(deviceId);
        }

        var latestSample = await _reachabilityRepository.GetLatestByDeviceIdAsync(deviceId, ct);

        LatestReachabilityDto? reachabilityDto = null;
        if (latestSample != null)
        {
            reachabilityDto = new LatestReachabilityDto(
                StatusId: latestSample.StatusId,
                StatusCode: GetStatusCode(latestSample.StatusId),
                LatencyUs: latestSample.LatencyUs,
                LossPercentage: (decimal)latestSample.LossPercentage,
                NtpOffsetUs: latestSample.NtpOffsetUs,
                IsSuppressed: latestSample.IsSuppressed,
                RecordedAt: latestSample.RecordedAt
            );
        }

        return new DeviceLiveTelemetryDto(
            DeviceId: device.DeviceId,
            MacAddress: device.MacAddress,
            PrimaryIp: device.PrimaryIp,
            DisplayName: device.DisplayName,
            LatestReachability: reachabilityDto,
            LatestHealth: null
        );
    }

    public async Task<DeviceHistoryTelemetryDto> GetTelemetryHistoryAsync(
        int deviceId,
        DateTimeOffset start,
        DateTimeOffset end,
        string interval,
        CancellationToken ct = default)
    {
        var device = await _deviceRepository.GetByIdAsync(deviceId, ct);
        if (device == null)
        {
            throw new DeviceNotFoundException(deviceId);
        }

        var history = await _reachabilityRepository.GetHistoryAsync(deviceId, start, end, ct);

        var points = history.Select(s => new ReachabilityHistoryPointDto(
            Timestamp: s.RecordedAt,
            StatusId: s.StatusId,
            StatusCode: GetStatusCode(s.StatusId),
            LatencyUs: s.LatencyUs,
            LossPercentage: (decimal)s.LossPercentage,
            NtpOffsetUs: s.NtpOffsetUs,
            IsSuppressed: s.IsSuppressed
        )).ToList();

        return new DeviceHistoryTelemetryDto(
            DeviceId: device.DeviceId,
            Start: start,
            End: end,
            Interval: string.IsNullOrWhiteSpace(interval) ? "raw" : interval,
            Points: points
        );
    }

    private static string GetStatusCode(short statusId) => statusId switch
    {
        1 => "REACHABLE",
        2 => "UNREACHABLE",
        3 => "SUPPRESSED",
        _ => "UNKNOWN"
    };
}
