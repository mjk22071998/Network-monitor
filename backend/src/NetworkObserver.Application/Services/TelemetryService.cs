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
    private readonly INtpProbeFactory _ntpProbeFactory;
    private readonly ICascadeSuppressionCircuit _suppressionCircuit;

    public TelemetryService(
        IDeviceRepository deviceRepository,
        IReachabilityRepository reachabilityRepository,
        IMonitoringConfigRepository configRepository,
        IPingProbeFactory probeFactory,
        INtpProbeFactory ntpProbeFactory,
        ICascadeSuppressionCircuit suppressionCircuit)
    {
        _deviceRepository = deviceRepository;
        _reachabilityRepository = reachabilityRepository;
        _configRepository = configRepository;
        _probeFactory = probeFactory;
        _ntpProbeFactory = ntpProbeFactory;
        _suppressionCircuit = suppressionCircuit;
    }

    public async Task<PollSweepResultDto> ExecuteReachabilitySweepAsync(CancellationToken ct = default)
    {
        var config = await _configRepository.GetActiveConfigAsync(ct);
        var timeoutMs = config?.ReachabilityTimeoutMs ?? 2000;
        var enableSuppression = config?.CascadeSuppressionEnabled ?? true;

        // Fetch all monitored devices in enclave
        var (devices, _) = await _deviceRepository.GetPagedAsync(new DeviceQueryParameters(Limit: 1000), ct);
        var monitoredDevices = devices.Where(d => d.IsMonitored).ToList();

        if (monitoredDevices.Count == 0)
        {
            return new PollSweepResultDto(0, 0, 0, 0, DateTimeOffset.UtcNow);
        }

        var pingStrategy = _probeFactory.CreateStrategy();
        var ntpStrategy = _ntpProbeFactory.CreateStrategy();
        var now = DateTimeOffset.UtcNow;

        var sampleList = await _suppressionCircuit.ExecuteSweepAsync(
            monitoredDevices,
            timeoutMs,
            enableSuppression,
            pingStrategy,
            ntpStrategy,
            now,
            ct
        );

        await _reachabilityRepository.AddBatchAsync(sampleList, ct);

        var reachableCount = sampleList.Count(s => s.StatusId == 1);
        var unreachableCount = sampleList.Count(s => s.StatusId == 2);
        var suppressedCount = sampleList.Count(s => s.StatusId == 3);

        return new PollSweepResultDto(
            PolledCount: sampleList.Count,
            ReachableCount: reachableCount,
            UnreachableCount: unreachableCount,
            SuppressedCount: suppressedCount,
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

        LiveNetworkLayerDto? networkLayerDto = null;
        LatestReachabilityDto? reachabilityDto = null;

        if (latestSample != null)
        {
            var statusCode = GetStatusCode(latestSample.StatusId);
            decimal? latencyMs = latestSample.LatencyUs.HasValue
                ? Math.Round(latestSample.LatencyUs.Value / 1000m, 2)
                : null;
            decimal? ntpOffsetMs = latestSample.NtpOffsetUs.HasValue
                ? Math.Round(latestSample.NtpOffsetUs.Value / 1000m, 2)
                : null;

            networkLayerDto = new LiveNetworkLayerDto(
                StatusCode: statusCode,
                LatencyUs: latestSample.LatencyUs,
                LatencyMs: latencyMs,
                LossPercentage: (decimal)latestSample.LossPercentage,
                NtpOffsetUs: latestSample.NtpOffsetUs,
                NtpOffsetMs: ntpOffsetMs,
                IsSuppressed: latestSample.IsSuppressed,
                LastCheckedAt: latestSample.RecordedAt
            );

            reachabilityDto = new LatestReachabilityDto(
                StatusId: latestSample.StatusId,
                StatusCode: statusCode,
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
            NetworkLayer: networkLayerDto,
            LatestReachability: reachabilityDto,
            HostLayer: null,
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
