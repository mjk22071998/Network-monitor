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
    private readonly StubHealthRepository _healthRepo;
    private readonly StubMonitoringConfigRepository _configRepo;
    private readonly StubPingProbeFactory _probeFactory;
    private readonly StubNtpProbeFactory _ntpFactory;
    private readonly StubHostHealthProbeFactory _healthFactory;
    private readonly CascadeSuppressionCircuit _suppressionCircuit;
    private readonly TelemetryService _telemetryService;

    public TelemetryServiceTests()
    {
        _deviceRepo = new StubDeviceRepository();
        _reachabilityRepo = new StubReachabilityRepository();
        _healthRepo = new StubHealthRepository();
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
        _ntpFactory = new StubNtpProbeFactory();
        _healthFactory = new StubHostHealthProbeFactory();
        _suppressionCircuit = new CascadeSuppressionCircuit();

        _telemetryService = new TelemetryService(
            _deviceRepo,
            _reachabilityRepo,
            _healthRepo,
            _configRepo,
            _probeFactory,
            _ntpFactory,
            _healthFactory,
            _suppressionCircuit
        );
    }

    [Fact]
    public async Task ExecuteReachabilitySweepAsync_ProbesMonitoredDevices_AndPersistsSamples_StrictNARule()
    {
        // Arrange: Device 1 is reachable, Device 2 is unreachable, Device 3 is unmonitored
        _deviceRepo.AddDevice(new Device { DeviceId = 1, DisplayName = "CORE-R1", PrimaryIp = "10.0.0.1", MacAddress = "00:1A:2B:00:00:01", TypeId = 1, GroupId = 1, IsMonitored = true });
        _deviceRepo.AddDevice(new Device { DeviceId = 2, DisplayName = "LAB-PC-1", PrimaryIp = "192.168.1.101", MacAddress = "00:1A:2B:00:00:02", TypeId = 5, GroupId = 1, IsMonitored = true });
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
    public async Task ExecuteReachabilitySweepAsync_ProbesMonitoredDevices_AndCalculatesNtpDrift_StrictNARule()
    {
        // Arrange
        // Device 1: reachable with active NTP daemon (offset = -1250 us)
        // Device 2: reachable without NTP daemon / timing out (offset = null)
        // Device 3: unreachable (NTP not queried, offset = null)
        _deviceRepo.AddDevice(new Device { DeviceId = 1, DisplayName = "CORE-R1", PrimaryIp = "10.0.0.1", MacAddress = "00:1A:2B:00:00:01", TypeId = 1, GroupId = 1, IsMonitored = true });
        _deviceRepo.AddDevice(new Device { DeviceId = 2, DisplayName = "LAB-PC-NO-NTP", PrimaryIp = "192.168.1.50", MacAddress = "00:1A:2B:00:00:02", TypeId = 5, GroupId = 1, IsMonitored = true });
        _deviceRepo.AddDevice(new Device { DeviceId = 3, DisplayName = "DOWN-DEVICE", PrimaryIp = "10.0.0.99", MacAddress = "00:1A:2B:00:00:03", TypeId = 1, GroupId = 1, IsMonitored = true });

        _probeFactory.ConfigureResult("10.0.0.1", new PingProbeResult(IsSuccess: true, LatencyUs: 300, LossPercentage: 0f));
        _probeFactory.ConfigureResult("192.168.1.50", new PingProbeResult(IsSuccess: true, LatencyUs: 500, LossPercentage: 0f));
        _probeFactory.ConfigureResult("10.0.0.99", new PingProbeResult(IsSuccess: false, LatencyUs: null, LossPercentage: 100f));

        _ntpFactory.ConfigureResult("10.0.0.1", new NtpProbeResult(IsSuccess: true, OffsetUs: -1250));
        _ntpFactory.ConfigureResult("192.168.1.50", new NtpProbeResult(IsSuccess: false, OffsetUs: null, ErrorMessage: "TimedOut"));

        // Act
        var result = await _telemetryService.ExecuteReachabilitySweepAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.PolledCount);
        Assert.Equal(2, result.ReachableCount);
        Assert.Equal(1, result.UnreachableCount);

        var sample1 = _reachabilityRepo.SavedSamples.FirstOrDefault(s => s.DeviceId == 1);
        Assert.NotNull(sample1);
        Assert.Equal(-1250, sample1.NtpOffsetUs);

        var sample2 = _reachabilityRepo.SavedSamples.FirstOrDefault(s => s.DeviceId == 2);
        Assert.NotNull(sample2);
        Assert.Null(sample2.NtpOffsetUs); // STRICT N/A RULE: Null for missing/timed-out NTP (never 0)

        var sample3 = _reachabilityRepo.SavedSamples.FirstOrDefault(s => s.DeviceId == 3);
        Assert.NotNull(sample3);
        Assert.Null(sample3.NtpOffsetUs); // Unreachable node: strictly null
    }

    [Fact]
    public async Task ExecuteReachabilitySweepAsync_WhenUpstreamSwitchFails_ImmediatelySuppressesDownstreamNodes_WithoutSyntheticPings()
    {
        // Arrange
        // Topology DAG:
        // Device 1: Core Router (Root)
        // Device 2: Access Switch (Parent = 1)
        // Device 3: Lab PC 1 (Parent = 2)
        // Device 4: Leaf PC (Parent = 2)
        _deviceRepo.AddDevice(new Device { DeviceId = 1, DisplayName = "CORE-R1", PrimaryIp = "10.0.0.1", MacAddress = "00:1A:2B:00:00:01", TypeId = 1, GroupId = 1, IsMonitored = true, ParentDeviceId = null });
        _deviceRepo.AddDevice(new Device { DeviceId = 2, DisplayName = "ACCESS-SW-1", PrimaryIp = "192.168.1.2", MacAddress = "00:1A:2B:00:00:02", TypeId = 3, GroupId = 1, IsMonitored = true, ParentDeviceId = 1 });
        _deviceRepo.AddDevice(new Device { DeviceId = 3, DisplayName = "LAB-PC-1", PrimaryIp = "192.168.1.103", MacAddress = "00:1A:2B:00:00:03", TypeId = 5, GroupId = 1, IsMonitored = true, ParentDeviceId = 2 });
        _deviceRepo.AddDevice(new Device { DeviceId = 4, DisplayName = "LEAF-PC-1", PrimaryIp = "192.168.1.104", MacAddress = "00:1A:2B:00:00:04", TypeId = 5, GroupId = 1, IsMonitored = true, ParentDeviceId = 2 });

        // Core Router 1 is UP
        _probeFactory.ConfigureResult("10.0.0.1", new PingProbeResult(IsSuccess: true, LatencyUs: 250, LossPercentage: 0f));
        // Access Switch 2 is DOWN (Outage)
        _probeFactory.ConfigureResult("192.168.1.2", new PingProbeResult(IsSuccess: false, LatencyUs: null, LossPercentage: 100f, ErrorMessage: "TimedOut"));

        // Act
        var result = await _telemetryService.ExecuteReachabilitySweepAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(4, result.PolledCount);
        Assert.Equal(1, result.ReachableCount);   // Core Router
        Assert.Equal(1, result.UnreachableCount); // Access Switch
        Assert.Equal(2, result.SuppressedCount);  // Lab PC 1 + Lab PC 2

        var samples = _reachabilityRepo.SavedSamples;
        Assert.Equal(4, samples.Count);

        var routerSample = samples.First(s => s.DeviceId == 1);
        Assert.Equal(1, routerSample.StatusId); // REACHABLE
        Assert.False(routerSample.IsSuppressed);

        var switchSample = samples.First(s => s.DeviceId == 2);
        Assert.Equal(2, switchSample.StatusId); // UNREACHABLE
        Assert.False(switchSample.IsSuppressed);

        var labPcSample = samples.First(s => s.DeviceId == 3);
        Assert.Equal(3, labPcSample.StatusId); // SUPPRESSED
        Assert.True(labPcSample.IsSuppressed);
        Assert.Null(labPcSample.LatencyUs);     // Strict N/A Rule

        var pcSample = samples.First(s => s.DeviceId == 4);
        Assert.Equal(3, pcSample.StatusId);  // SUPPRESSED
        Assert.True(pcSample.IsSuppressed);
        Assert.Null(pcSample.LatencyUs);     // Strict N/A Rule

        // FR-CAS-02: Zero synthetic pings sent to downstream suppressed devices!
        Assert.Contains("10.0.0.1", _probeFactory.ProbedIps);
        Assert.Contains("192.168.1.2", _probeFactory.ProbedIps);
        Assert.DoesNotContain("192.168.1.103", _probeFactory.ProbedIps);
        Assert.DoesNotContain("192.168.1.104", _probeFactory.ProbedIps);
    }

    [Fact]
    public async Task ExecuteReachabilitySweepAsync_WhenCascadeSuppressionDisabled_PollsAllDevicesDirectly()
    {
        // Arrange
        _configRepo.SetConfig(new MonitoringConfig
        {
            ConfigId = 1,
            SiteId = 1,
            ReachabilityIntervalSec = 5,
            ReachabilityTimeoutMs = 2000,
            HealthIntervalSec = 30,
            HealthTimeoutMs = 5000,
            CascadeSuppressionEnabled = false // DISABLED
        });

        _deviceRepo.AddDevice(new Device { DeviceId = 1, DisplayName = "CORE-R1", PrimaryIp = "10.0.0.1", MacAddress = "00:1A:2B:00:00:01", TypeId = 1, GroupId = 1, IsMonitored = true, ParentDeviceId = null });
        _deviceRepo.AddDevice(new Device { DeviceId = 2, DisplayName = "ACCESS-SW-1", PrimaryIp = "192.168.1.2", MacAddress = "00:1A:2B:00:00:02", TypeId = 3, GroupId = 1, IsMonitored = true, ParentDeviceId = 1 });
        _deviceRepo.AddDevice(new Device { DeviceId = 3, DisplayName = "LAB-PC-1", PrimaryIp = "192.168.1.103", MacAddress = "00:1A:2B:00:00:03", TypeId = 5, GroupId = 1, IsMonitored = true, ParentDeviceId = 2 });

        _probeFactory.ConfigureResult("10.0.0.1", new PingProbeResult(IsSuccess: true, LatencyUs: 250, LossPercentage: 0f));
        _probeFactory.ConfigureResult("192.168.1.2", new PingProbeResult(IsSuccess: false, LatencyUs: null, LossPercentage: 100f));
        _probeFactory.ConfigureResult("192.168.1.103", new PingProbeResult(IsSuccess: false, LatencyUs: null, LossPercentage: 100f));

        // Act
        var result = await _telemetryService.ExecuteReachabilitySweepAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.SuppressedCount);
        Assert.Equal(2, result.UnreachableCount);

        // When disabled, downstream node was directly probed
        Assert.Contains("192.168.1.103", _probeFactory.ProbedIps);
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
        _deviceRepo.AddDevice(new Device { DeviceId = 2, DisplayName = "LAB-PC-1", PrimaryIp = "192.168.1.101", MacAddress = "00:1A:2B:00:00:02", TypeId = 5, GroupId = 1, IsMonitored = true });

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

    [Fact]
    public async Task ExecuteHealthSweepAsync_ProbesMonitoredEndpoints_AndPersistsMultiVolumeDiskStorage()
    {
        // Arrange
        _deviceRepo.AddDevice(new Device { DeviceId = 1, DisplayName = "CORE-R1", PrimaryIp = "10.0.0.1", MacAddress = "00:1A:2B:00:00:01", TypeId = 1, GroupId = 1, IsMonitored = true });
        _deviceRepo.AddDevice(new Device { DeviceId = 2, DisplayName = "LAB-AUTH-SERVER-1", PrimaryIp = "192.168.1.10", MacAddress = "00:1A:2B:00:00:02", TypeId = 4, GroupId = 1, IsMonitored = true });
        _deviceRepo.AddDevice(new Device { DeviceId = 3, DisplayName = "OPERATOR-PC-1", PrimaryIp = "192.168.1.50", MacAddress = "00:1A:2B:00:00:03", TypeId = 5, GroupId = 1, IsMonitored = true });

        // Core Router: Unsupported
        _healthFactory.ConfigureResult("10.0.0.1", new HostHealthProbeResult(
            IsSuccess: false, StatusId: 4, CpuUtilizationPct: null, RamUtilizationPct: null, NetRxKbps: null, NetTxKbps: null, TemperatureCelsius: null, DiskVolumes: Array.Empty<DiskVolumeProbeResult>()));

        // Server: Available with 2 disk partitions
        _healthFactory.ConfigureResult("192.168.1.10", new HostHealthProbeResult(
            IsSuccess: true, StatusId: 1, CpuUtilizationPct: 25.5f, RamUtilizationPct: 50.0f, NetRxKbps: 450.0f, NetTxKbps: 120.0f, TemperatureCelsius: 38.5f,
            DiskVolumes: new List<DiskVolumeProbeResult>
            {
                new("C:", 45.0f, 256.0f, 140.8f),
                new("D:", 80.0f, 1024.0f, 204.8f)
            }));

        // PC: Unavailable (Strict N/A Rule: all metrics null)
        _healthFactory.ConfigureResult("192.168.1.50", new HostHealthProbeResult(
            IsSuccess: false, StatusId: 2, CpuUtilizationPct: null, RamUtilizationPct: null, NetRxKbps: null, NetTxKbps: null, TemperatureCelsius: null, DiskVolumes: Array.Empty<DiskVolumeProbeResult>()));

        // Act
        var result = await _telemetryService.ExecuteHealthSweepAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.PolledCount);
        Assert.Equal(1, result.AvailableCount);
        Assert.Equal(1, result.UnavailableCount);
        Assert.Equal(1, result.UnsupportedCount);

        var saved = _healthRepo.SavedSamples;
        Assert.Equal(3, saved.Count);

        var serverSample = saved.First(s => s.DeviceId == 2);
        Assert.Equal(1, serverSample.StatusId);
        Assert.Equal(25.5f, serverSample.CpuUtilizationPct);
        Assert.Equal(50.0f, serverSample.RamUtilizationPct);
        Assert.Equal(2, serverSample.DiskVolumeSamples.Count);
        Assert.Equal("C:", serverSample.DiskVolumeSamples.First().VolumeName);
        Assert.Equal(45.0f, serverSample.DiskVolumeSamples.First().UsedPercentage);

        var pcSample = saved.First(s => s.DeviceId == 3);
        Assert.Equal(2, pcSample.StatusId);
        Assert.Null(pcSample.CpuUtilizationPct); // STRICT N/A RULE
        Assert.Null(pcSample.RamUtilizationPct);
    }

    [Fact]
    public async Task GetLiveTelemetryAsync_WhenHealthSampleExists_PopulatesHostLayerAndDiskVolumes()
    {
        // Arrange
        _deviceRepo.AddDevice(new Device { DeviceId = 2, DisplayName = "LAB-AUTH-SERVER-1", PrimaryIp = "192.168.1.10", MacAddress = "00:1A:2B:00:00:02", TypeId = 4, GroupId = 1 });
        var now = DateTimeOffset.UtcNow;

        _reachabilityRepo.SetLatestSample(2, new ReachabilitySample
        {
            DeviceId = 2, RecordedAt = now, StatusId = 1, LatencyUs = 200, LossPercentage = 0f
        });

        _healthRepo.SetLatestSample(2, new HealthSample
        {
            DeviceId = 2, RecordedAt = now, StatusId = 1, CpuUtilizationPct = 14.5f, RamUtilizationPct = 42.1f,
            DiskVolumeSamples = new List<DiskVolumeSample>
            {
                new() { VolumeName = "C:", TotalCapacityGb = 256f, FreeSpaceGb = 140f, UsedPercentage = 45f }
            }
        });

        // Act
        var live = await _telemetryService.GetLiveTelemetryAsync(2);

        // Assert
        Assert.NotNull(live);
        Assert.NotNull(live.HostLayer);
        Assert.Equal("AVAILABLE", live.HostLayer.StatusCode);
        Assert.Equal(14.5f, live.HostLayer.CpuUtilizationPct);
        Assert.Equal(42.1f, live.HostLayer.RamUtilizationPct);
        Assert.Single(live.HostLayer.DiskVolumes);
        Assert.Equal("C:", live.HostLayer.DiskVolumes[0].VolumeName);
    }

    [Fact]
    public async Task GetTopologyTreeAsync_ReturnsTwoBranchAccessSwitchHierarchy()
    {
        // Arrange
        // Infrastructure:
        // 1: Core Router (Root)
        // 2: Aggregate Switch (Parent = 1)
        // 3: Access Switch A (Parent = 2)
        // Leaf Endpoints on Access Switch 3:
        // 10: Lab PC 1
        // 11: Lab PC 2
        // 12: PC 1
        // 13: PC 2
        _deviceRepo.AddDevice(new Device { DeviceId = 1, DisplayName = "CORE-R1", PrimaryIp = "10.0.0.1", MacAddress = "00:1A:2B:00:00:01", TypeId = 1, GroupId = 1, ParentDeviceId = null, IsMonitored = true });
        _deviceRepo.AddDevice(new Device { DeviceId = 2, DisplayName = "AGG-SW-1", PrimaryIp = "10.0.0.2", MacAddress = "00:1A:2B:00:00:02", TypeId = 2, GroupId = 1, ParentDeviceId = 1, IsMonitored = true });
        _deviceRepo.AddDevice(new Device { DeviceId = 3, DisplayName = "ACCESS-SW-A", PrimaryIp = "192.168.1.10", MacAddress = "00:1A:2B:00:00:03", TypeId = 3, GroupId = 1, ParentDeviceId = 2, IsMonitored = true });

        _deviceRepo.AddDevice(new Device { DeviceId = 10, DisplayName = "LAB101-PC-1", PrimaryIp = "192.168.1.101", MacAddress = "00:1A:2B:00:00:10", TypeId = 5, GroupId = 1, ParentDeviceId = 3, IsMonitored = true });
        _deviceRepo.AddDevice(new Device { DeviceId = 11, DisplayName = "LAB101-PC-2", PrimaryIp = "192.168.1.102", MacAddress = "00:1A:2B:00:00:11", TypeId = 5, GroupId = 1, ParentDeviceId = 3, IsMonitored = true });
        _deviceRepo.AddDevice(new Device { DeviceId = 12, DisplayName = "PC-1", PrimaryIp = "192.168.1.103", MacAddress = "00:1A:2B:00:00:12", TypeId = 5, GroupId = 1, ParentDeviceId = 3, IsMonitored = true });
        _deviceRepo.AddDevice(new Device { DeviceId = 13, DisplayName = "PC-2", PrimaryIp = "192.168.1.104", MacAddress = "00:1A:2B:00:00:13", TypeId = 5, GroupId = 1, ParentDeviceId = 3, IsMonitored = true });

        var now = DateTimeOffset.UtcNow;
        _reachabilityRepo.SetLatestSample(1, new ReachabilitySample { DeviceId = 1, RecordedAt = now, StatusId = 1, LatencyUs = 200 });
        _reachabilityRepo.SetLatestSample(2, new ReachabilitySample { DeviceId = 2, RecordedAt = now, StatusId = 1, LatencyUs = 400 });
        _reachabilityRepo.SetLatestSample(3, new ReachabilitySample { DeviceId = 3, RecordedAt = now, StatusId = 1, LatencyUs = 600 });
        _reachabilityRepo.SetLatestSample(10, new ReachabilitySample { DeviceId = 10, RecordedAt = now, StatusId = 1, LatencyUs = 1200 });
        _reachabilityRepo.SetLatestSample(11, new ReachabilitySample { DeviceId = 11, RecordedAt = now, StatusId = 1, LatencyUs = 1300 });
        _reachabilityRepo.SetLatestSample(12, new ReachabilitySample { DeviceId = 12, RecordedAt = now, StatusId = 1, LatencyUs = 1400 });
        _reachabilityRepo.SetLatestSample(13, new ReachabilitySample { DeviceId = 13, RecordedAt = now, StatusId = 1, LatencyUs = 1500 });

        // Act
        var topology = await _telemetryService.GetTopologyTreeAsync();

        // Assert
        Assert.NotNull(topology);
        Assert.Single(topology.RootNodes);

        var root = topology.RootNodes[0];
        Assert.Equal(1, root.DeviceId);
        Assert.Equal("CORE_ROUTER", root.TypeCode);
        Assert.Equal("REACHABLE", root.StatusCode);
        Assert.Equal(200, root.LatencyUs);
        Assert.Single(root.Children);

        var aggSw = root.Children[0];
        Assert.Equal(2, aggSw.DeviceId);
        Assert.Equal("AGGREGATE_SWITCH", aggSw.TypeCode);
        Assert.Single(aggSw.Children);

        var accSw = aggSw.Children[0];
        Assert.Equal(3, accSw.DeviceId);
        Assert.Equal("ACCESS_SWITCH", accSw.TypeCode);
        Assert.Empty(accSw.Children); // No sub-switches

        // Verify 2-Branch Layout Guarantee
        Assert.NotNull(accSw.EndpointBranches);
        Assert.Equal(2, accSw.EndpointBranches.BranchLeft.Count);
        Assert.Equal(2, accSw.EndpointBranches.BranchRight.Count);

        // Branch Left: LAB101-PC-1 (index 0) and PC-1 (index 1)
        Assert.Equal(10, accSw.EndpointBranches.BranchLeft[0].DeviceId);
        Assert.Equal(0, accSw.EndpointBranches.BranchLeft[0].BranchIndex);
        Assert.Equal(12, accSw.EndpointBranches.BranchLeft[1].DeviceId);
        Assert.Equal(1, accSw.EndpointBranches.BranchLeft[1].BranchIndex);

        // Branch Right: LAB101-PC-2 (index 0) and PC-2 (index 1)
        Assert.Equal(11, accSw.EndpointBranches.BranchRight[0].DeviceId);
        Assert.Equal(0, accSw.EndpointBranches.BranchRight[0].BranchIndex);
        Assert.Equal(13, accSw.EndpointBranches.BranchRight[1].DeviceId);
        Assert.Equal(1, accSw.EndpointBranches.BranchRight[1].BranchIndex);
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
        => Task.FromResult<IReadOnlyDictionary<int, ReachabilitySample>>(_latest);

    public Task<IReadOnlyDictionary<int, ReachabilitySample>> GetLatestForMonitoredDevicesAsync(IReadOnlyList<int> deviceIds, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyDictionary<int, ReachabilitySample>>(_latest);

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

    public List<string> ProbedIps => _strategy.ProbedIps;

    public void ConfigureResult(string ip, PingProbeResult result) => _strategy.ConfigureResult(ip, result);

    public IPingStrategy CreateStrategy() => _strategy;
}

internal class StubPingStrategy : IPingStrategy
{
    private readonly Dictionary<string, PingProbeResult> _results = new();
    public List<string> ProbedIps { get; } = new();

    public void ConfigureResult(string ip, PingProbeResult result) => _results[ip] = result;

    public Task<PingProbeResult> PingAsync(string destinationIp, int timeoutMs, CancellationToken ct = default)
    {
        lock (ProbedIps)
        {
            ProbedIps.Add(destinationIp);
        }

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

internal class StubNtpProbeFactory : INtpProbeFactory
{
    private readonly StubNtpStrategy _strategy = new();

    public void ConfigureResult(string ip, NtpProbeResult result) => _strategy.ConfigureResult(ip, result);

    public INtpStrategy CreateStrategy() => _strategy;
}

internal class StubNtpStrategy : INtpStrategy
{
    private readonly Dictionary<string, NtpProbeResult> _results = new();

    public void ConfigureResult(string ip, NtpProbeResult result) => _results[ip] = result;

    public Task<NtpProbeResult> QueryDriftAsync(string ipAddress, int timeoutMs = 1000, CancellationToken ct = default)
    {
        if (_results.TryGetValue(ipAddress, out var result))
        {
            return Task.FromResult(result);
        }

        return Task.FromResult(new NtpProbeResult(false, null, "NoConfiguredNtp"));
    }
}

internal class StubHealthRepository : IHealthRepository
{
    public List<HealthSample> SavedSamples { get; } = new();
    private readonly Dictionary<int, HealthSample> _latest = new();
    private readonly Dictionary<int, List<HealthSample>> _history = new();
    private HostHealthSummaryDto _summary = new(0, 0, 0, 0);

    public void SetSummary(HostHealthSummaryDto summary) => _summary = summary;
    public void SetLatestSample(int deviceId, HealthSample sample) => _latest[deviceId] = sample;
    public void SetHistory(int deviceId, List<HealthSample> samples) => _history[deviceId] = samples;

    public Task AddBatchAsync(IEnumerable<HealthSample> samples, CancellationToken ct = default)
    {
        SavedSamples.AddRange(samples);
        return Task.CompletedTask;
    }

    public Task<HealthSample?> GetLatestByDeviceIdAsync(int deviceId, CancellationToken ct = default)
    {
        _latest.TryGetValue(deviceId, out var sample);
        return Task.FromResult(sample);
    }

    public Task<IReadOnlyDictionary<int, HealthSample>> GetLatestForMonitoredDevicesAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyDictionary<int, HealthSample>>(_latest);

    public Task<IReadOnlyDictionary<int, HealthSample>> GetLatestForMonitoredDevicesAsync(IReadOnlyList<int> deviceIds, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyDictionary<int, HealthSample>>(_latest);

    public Task<IReadOnlyList<HealthSample>> GetHistoryAsync(int deviceId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct = default)
    {
        if (_history.TryGetValue(deviceId, out var list))
        {
            var filtered = list.Where(s => s.RecordedAt >= start && s.RecordedAt <= end).ToList();
            return Task.FromResult<IReadOnlyList<HealthSample>>(filtered);
        }

        return Task.FromResult<IReadOnlyList<HealthSample>>(new List<HealthSample>());
    }

    public Task<HostHealthSummaryDto> GetSummaryAsync(CancellationToken ct = default)
    {
        return Task.FromResult(_summary);
    }
}

internal class StubHostHealthProbeFactory : IHostHealthProbeFactory
{
    private readonly StubHostHealthStrategy _strategy = new();

    public void ConfigureResult(string ip, HostHealthProbeResult result) => _strategy.ConfigureResult(ip, result);

    public IHostHealthStrategy CreateStrategy() => _strategy;
}

internal class StubHostHealthStrategy : IHostHealthStrategy
{
    private readonly Dictionary<string, HostHealthProbeResult> _results = new();

    public void ConfigureResult(string ip, HostHealthProbeResult result) => _results[ip] = result;

    public Task<HostHealthProbeResult> QueryHealthAsync(string ipAddress, short deviceTypeId, int timeoutMs = 3000, CancellationToken ct = default)
    {
        if (_results.TryGetValue(ipAddress, out var result))
        {
            return Task.FromResult(result);
        }

        return Task.FromResult(new HostHealthProbeResult(
            IsSuccess: false,
            StatusId: 2,
            CpuUtilizationPct: null,
            RamUtilizationPct: null,
            NetRxKbps: null,
            NetTxKbps: null,
            TemperatureCelsius: null,
            DiskVolumes: Array.Empty<DiskVolumeProbeResult>()
        ));
    }
}
