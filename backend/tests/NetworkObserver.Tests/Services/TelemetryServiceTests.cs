using NetworkObserver.Application.DTOs;
using NetworkObserver.Application.Interfaces.Repositories;
using NetworkObserver.Application.Interfaces.Services;
using NetworkObserver.Application.Interfaces.Telemetry;
using NetworkObserver.Application.Services;
using NetworkObserver.Domain.Entities;
using NetworkObserver.Domain.Exceptions;
using Xunit;

namespace NetworkObserver.Tests.Services;

public class TelemetryServiceTests
{
    private readonly StubDeviceRepository _deviceRepo;
    private readonly StubReachabilityRepository _reachabilityRepo;
    private readonly StubMonitoringConfigRepository _configRepo;
    private readonly StubPingProbeFactory _probeFactory;
    private readonly TelemetryService _telemetryService;

    public TelemetryServiceTests()
    {
        _deviceRepo = new StubDeviceRepository();
        _reachabilityRepo = new StubReachabilityRepository();
        _configRepo = new StubMonitoringConfigRepository();
        _configRepo.SetConfig(new MonitoringConfig
        {
            ConfigId = 1,
            SiteId = 1,
            ReachabilityIntervalSec = 5,
            ReachabilityTimeoutMs = 2000,
            HealthIntervalSec = 30,
            HealthTimeoutMs = 5000,
            CascadeSuppressionEnabled = true
        });
        _probeFactory = new StubPingProbeFactory();

        _telemetryService = new TelemetryService(
            _deviceRepo,
            _reachabilityRepo,
            _configRepo,
            _probeFactory
        );
    }

    [Fact]
    public async Task ExecuteReachabilitySweepAsync_ProbesMonitoredDevices_AndPersistsSamples_StrictNARule()
    {
        // Arrange: Device 1 is reachable, Device 2 is unreachable, Device 3 is unmonitored
        _deviceRepo.AddDevice(new Device { DeviceId = 1, DisplayName = "CORE-R1", PrimaryIp = "10.0.0.1", MacAddress = "00:1A:2B:00:00:01", TypeId = 1, GroupId = 1, IsMonitored = true });
        _deviceRepo.AddDevice(new Device { DeviceId = 2, DisplayName = "LEAF-PLC-1", PrimaryIp = "192.168.1.101", MacAddress = "00:1A:2B:00:00:02", TypeId = 5, GroupId = 1, IsMonitored = true });
        _deviceRepo.AddDevice(new Device { DeviceId = 3, DisplayName = "UNMONITORED-PC", PrimaryIp = "10.0.0.50", MacAddress = "00:1A:2B:00:00:03", TypeId = 8, GroupId = 1, IsMonitored = false });

        // Configure probe factory: 10.0.0.1 succeeds with 450us, 192.168.1.101 times out (Strict N/A LatencyUs = null)
        _probeFactory.ConfigureResult("10.0.0.1", new PingProbeResult(IsSuccess: true, LatencyUs: 450, LossPercentage: 0f));
        _probeFactory.ConfigureResult("192.168.1.101", new PingProbeResult(IsSuccess: false, LatencyUs: null, LossPercentage: 100f, ErrorMessage: "TimedOut"));

        // Act
        var result = await _telemetryService.ExecuteReachabilitySweepAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.PolledCount);     // Only monitored devices polled
        Assert.Equal(1, result.ReachableCount);
        Assert.Equal(1, result.UnreachableCount);

        // Verify persisted samples
        var samples = _reachabilityRepo.SavedSamples;
        Assert.Equal(2, samples.Count);

        var reachableSample = samples.FirstOrDefault(s => s.DeviceId == 1);
        Assert.NotNull(reachableSample);
        Assert.Equal(1, reachableSample.StatusId);
        Assert.Equal(450, reachableSample.LatencyUs);
        Assert.Equal(0f, reachableSample.LossPercentage);

        var unreachableSample = samples.FirstOrDefault(s => s.DeviceId == 2);
        Assert.NotNull(unreachableSample);
        Assert.Equal(2, unreachableSample.StatusId);
        Assert.Null(unreachableSample.LatencyUs); // STRICT N/A RULE: Latency MUST be null on failure, never 0
        Assert.Equal(100f, unreachableSample.LossPercentage);
    }

    [Fact]
    public async Task ExecuteReachabilitySweepAsync_WhenNoMonitoredDevices_ReturnsZeroCountResult()
    {
        // Arrange
        _deviceRepo.AddDevice(new Device { DeviceId = 1, DisplayName = "SPARE-1", PrimaryIp = "10.0.0.99", MacAddress = "00:1A:2B:99:99:99", TypeId = 1, GroupId = 1, IsMonitored = false });

        // Act
        var result = await _telemetryService.ExecuteReachabilitySweepAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.PolledCount);
        Assert.Equal(0, result.ReachableCount);
        Assert.Equal(0, result.UnreachableCount);
        Assert.Empty(_reachabilityRepo.SavedSamples);
    }

    [Fact]
    public async Task GetSummaryAsync_ReturnsAggregatedMetrics_MatchingContract()
    {
        // Arrange
        _deviceRepo.AddDevice(new Device { DeviceId = 1, DisplayName = "CORE-R1", PrimaryIp = "10.0.0.1", MacAddress = "00:1A:2B:00:00:01", TypeId = 1, GroupId = 1, IsMonitored = true });
        _deviceRepo.AddDevice(new Device { DeviceId = 2, DisplayName = "LEAF-PLC-1", PrimaryIp = "192.168.1.101", MacAddress = "00:1A:2B:00:00:02", TypeId = 5, GroupId = 1, IsMonitored = true });

        _reachabilityRepo.SetSummary(new TelemetrySummaryDto(
            Timestamp: DateTimeOffset.UtcNow,
            Reachability: new ReachabilitySummaryDto(
                TotalMonitored: 2,
                ReachableCount: 1,
                OutageCount: 1,
                SuppressedCount: 0,
                UnknownCount: 0
            ),
            HostHealth: new HostHealthSummaryDto(0, 0, 0, 0)
        ));

        // Act
        var summary = await _telemetryService.GetSummaryAsync();

        // Assert
        Assert.NotNull(summary);
        Assert.Equal(2, summary.Reachability.TotalMonitored);
        Assert.Equal(1, summary.Reachability.ReachableCount);
        Assert.Equal(1, summary.Reachability.OutageCount);
        Assert.Equal(0, summary.Reachability.SuppressedCount);
        Assert.Equal(0, summary.Reachability.UnknownCount);
    }

    [Fact]
    public async Task GetLiveTelemetryAsync_WhenDeviceExists_ReturnsLiveTelemetryWithLatestReachability()
    {
        // Arrange
        _deviceRepo.AddDevice(new Device { DeviceId = 1, DisplayName = "CORE-R1", PrimaryIp = "10.0.0.1", MacAddress = "00:1A:2B:00:00:01", TypeId = 1, GroupId = 1 });
        var now = DateTimeOffset.UtcNow;
        _reachabilityRepo.SetLatestSample(1, new ReachabilitySample
        {
            DeviceId = 1,
            RecordedAt = now,
            StatusId = 1,
            LatencyUs = 320,
            LossPercentage = 0f,
            IsSuppressed = false
        });

        // Act
        var live = await _telemetryService.GetLiveTelemetryAsync(1);

        // Assert
        Assert.NotNull(live);
        Assert.Equal(1, live.DeviceId);
        Assert.Equal("CORE-R1", live.DisplayName);
        Assert.Equal("10.0.0.1", live.PrimaryIp);
        Assert.NotNull(live.LatestReachability);
        Assert.Equal(1, live.LatestReachability.StatusId);
        Assert.Equal("REACHABLE", live.LatestReachability.StatusCode);
        Assert.Equal(320, live.LatestReachability.LatencyUs);
        Assert.Equal(0m, live.LatestReachability.LossPercentage);
        Assert.False(live.LatestReachability.IsSuppressed);
    }

    [Fact]
    public async Task GetLiveTelemetryAsync_WhenDeviceNotFound_ThrowsDeviceNotFoundException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<DeviceNotFoundException>(() =>
            _telemetryService.GetLiveTelemetryAsync(9999));
    }

    [Fact]
    public async Task GetLiveTelemetryAsync_WhenDeviceHasNoSamples_ReturnsNullLatestReachability()
    {
        // Arrange
        _deviceRepo.AddDevice(new Device { DeviceId = 10, DisplayName = "NEW-DEVICE", PrimaryIp = "10.0.0.10", MacAddress = "00:1A:2B:10:10:10", TypeId = 1, GroupId = 1 });

        // Act
        var live = await _telemetryService.GetLiveTelemetryAsync(10);

        // Assert
        Assert.NotNull(live);
        Assert.Equal(10, live.DeviceId);
        Assert.Null(live.LatestReachability);
        Assert.Null(live.LatestHealth);
    }

    [Fact]
    public async Task GetTelemetryHistoryAsync_WhenDeviceExists_ReturnsPointsWithinWindow()
    {
        // Arrange
        _deviceRepo.AddDevice(new Device { DeviceId = 1, DisplayName = "CORE-R1", PrimaryIp = "10.0.0.1", MacAddress = "00:1A:2B:00:00:01", TypeId = 1, GroupId = 1 });
        var now = DateTimeOffset.UtcNow;

        _reachabilityRepo.SetHistory(1, new List<ReachabilitySample>
        {
            new() { DeviceId = 1, RecordedAt = now.AddMinutes(-10), StatusId = 1, LatencyUs = 300, LossPercentage = 0f },
            new() { DeviceId = 1, RecordedAt = now.AddMinutes(-5), StatusId = 1, LatencyUs = 350, LossPercentage = 0f },
            new() { DeviceId = 1, RecordedAt = now, StatusId = 2, LatencyUs = null, LossPercentage = 100f }
        });

        // Act
        var history = await _telemetryService.GetTelemetryHistoryAsync(1, now.AddMinutes(-15), now, "raw");

        // Assert
        Assert.NotNull(history);
        Assert.Equal(1, history.DeviceId);
        Assert.Equal(3, history.Points.Count);
        Assert.Equal("REACHABLE", history.Points[0].StatusCode);
        Assert.Equal(300, history.Points[0].LatencyUs);
        Assert.Equal("UNREACHABLE", history.Points[2].StatusCode);
        Assert.Null(history.Points[2].LatencyUs);
    }

    [Fact]
    public async Task GetTelemetryHistoryAsync_WhenDeviceNotFound_ThrowsDeviceNotFoundException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<DeviceNotFoundException>(() =>
            _telemetryService.GetTelemetryHistoryAsync(9999, DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow, "raw"));
    }
}

// ==========================================
// Test Stubs adhering to Clean Architecture
// ==========================================

internal class StubReachabilityRepository : IReachabilityRepository
{
    public List<ReachabilitySample> SavedSamples { get; } = new();
    private readonly Dictionary<int, ReachabilitySample> _latest = new();
    private readonly Dictionary<int, List<ReachabilitySample>> _history = new();
    private TelemetrySummaryDto? _summary;

    public void SetLatestSample(int deviceId, ReachabilitySample sample) => _latest[deviceId] = sample;
    public void SetHistory(int deviceId, List<ReachabilitySample> samples) => _history[deviceId] = samples;
    public void SetSummary(TelemetrySummaryDto summary) => _summary = summary;

    public Task AddBatchAsync(IEnumerable<ReachabilitySample> samples, CancellationToken ct = default)
    {
        SavedSamples.AddRange(samples);
        return Task.CompletedTask;
    }

    public Task<ReachabilitySample?> GetLatestByDeviceIdAsync(int deviceId, CancellationToken ct = default)
    {
        _latest.TryGetValue(deviceId, out var sample);
        return Task.FromResult(sample);
    }

    public Task<IReadOnlyDictionary<int, ReachabilitySample>> GetLatestForMonitoredDevicesAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyDictionary<int, ReachabilitySample>>(_latest);
    }

    public Task<IReadOnlyList<ReachabilitySample>> GetHistoryAsync(int deviceId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct = default)
    {
        if (_history.TryGetValue(deviceId, out var list))
        {
            var filtered = list.Where(s => s.RecordedAt >= start && s.RecordedAt <= end).ToList();
            return Task.FromResult<IReadOnlyList<ReachabilitySample>>(filtered);
        }

        return Task.FromResult<IReadOnlyList<ReachabilitySample>>(new List<ReachabilitySample>());
    }

    public Task<TelemetrySummaryDto> GetSummaryAsync(int totalMonitoredCount, CancellationToken ct = default)
    {
        if (_summary != null)
        {
            return Task.FromResult(_summary);
        }

        return Task.FromResult(new TelemetrySummaryDto(
            Timestamp: DateTimeOffset.UtcNow,
            Reachability: new ReachabilitySummaryDto(totalMonitoredCount, 0, 0, 0, totalMonitoredCount),
            HostHealth: new HostHealthSummaryDto(0, 0, 0, 0)
        ));
    }
}

internal class StubPingProbeFactory : IPingProbeFactory
{
    private readonly StubPingStrategy _strategy = new();

    public void ConfigureResult(string ip, PingProbeResult result) => _strategy.ConfigureResult(ip, result);

    public IPingStrategy CreateStrategy() => _strategy;
}

internal class StubPingStrategy : IPingStrategy
{
    private readonly Dictionary<string, PingProbeResult> _results = new();

    public void ConfigureResult(string ip, PingProbeResult result) => _results[ip] = result;

    public Task<PingProbeResult> PingAsync(string destinationIp, int timeoutMs, CancellationToken ct = default)
    {
        if (_results.TryGetValue(destinationIp, out var result))
        {
            return Task.FromResult(result);
        }

        return Task.FromResult(new PingProbeResult(
            IsSuccess: true,
            LatencyUs: 150,
            LossPercentage: 0f
        ));
    }
}
