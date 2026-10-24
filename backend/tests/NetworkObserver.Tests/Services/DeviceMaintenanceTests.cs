using NetworkObserver.Application.DTOs;
using NetworkObserver.Application.Interfaces.Repositories;
using NetworkObserver.Application.Services;
using NetworkObserver.Domain.Entities;
using NetworkObserver.Domain.Enums;
using NetworkObserver.Domain.Exceptions;
using Xunit;

namespace NetworkObserver.Tests.Services;

public class DeviceMaintenanceTests
{
    private readonly StubDeviceRepository _deviceRepo;
    private readonly StubGroupRepository _groupRepo;
    private readonly StubReachabilityRepository _reachabilityRepo;
    private readonly StubAuditLogRepository _auditRepo;
    private readonly DeviceService _deviceService;

    public DeviceMaintenanceTests()
    {
        _deviceRepo = new StubDeviceRepository();
        _groupRepo = new StubGroupRepository();
        _reachabilityRepo = new StubReachabilityRepository();
        _auditRepo = new StubAuditLogRepository();

        _deviceService = new DeviceService(_deviceRepo, _groupRepo, _reachabilityRepo, _auditRepo);
    }

    [Fact]
    public async Task ToggleMaintenanceAsync_WhenLeafEndpointByItSupport_TogglesModeAndLogsAudit()
    {
        // Arrange: Workstation PC (type 5)
        _deviceRepo.AddDevice(new Device
        {
            DeviceId = 20,
            DisplayName = "CS-LAB101-PC-15",
            PrimaryIp = "192.168.1.115",
            MacAddress = "00:1A:2B:11:22:33",
            TypeId = (short)DeviceTypeEnum.Pc,
            GroupId = 1,
            IsMonitored = true
        });

        var request = new DeviceMaintenanceRequest(IsMonitored: false, Reason: "OS Re-imaging scheduled");

        // Act - IT Support operator puts PC into maintenance
        var response = await _deviceService.ToggleMaintenanceAsync(
            deviceId: 20,
            request: request,
            userId: 5,
            operatorRole: RoleType.ItSupport,
            ipAddress: "10.0.0.50"
        );

        // Assert
        Assert.NotNull(response);
        Assert.Equal(20, response.DeviceId);
        Assert.False(response.IsMonitored);
        Assert.Equal("UNKNOWN", response.StatusCode);
        Assert.True(response.AuditLogId > 0);

        // Verify device state in repository
        var updatedDevice = await _deviceRepo.GetByIdAsync(20);
        Assert.NotNull(updatedDevice);
        Assert.False(updatedDevice.IsMonitored);

        // Verify audit log entry
        Assert.Single(_auditRepo.Logs);
        var audit = _auditRepo.Logs[0];
        Assert.Equal(5, audit.UserId);
        Assert.Equal("DEVICE_MAINTENANCE", audit.ActionCode);
        Assert.Equal("DEVICE", audit.TargetEntity);
        Assert.Equal("20", audit.TargetId);
        Assert.Equal("10.0.0.50", audit.IpAddress);
        Assert.Contains("OS Re-imaging scheduled", audit.Details);

        // Verify UNKNOWN reachability sample persisted (Strict N/A Rule: LatencyUs = null)
        Assert.Single(_reachabilityRepo.SavedSamples);
        var sample = _reachabilityRepo.SavedSamples[0];
        Assert.Equal(20, sample.DeviceId);
        Assert.Equal(0, sample.StatusId); // UNKNOWN
        Assert.Null(sample.LatencyUs);
    }

    [Fact]
    public async Task ToggleMaintenanceAsync_WhenAccessSwitchByItSupport_ThrowsUnauthorizedRoleException()
    {
        // Arrange: Access Switch (type 3)
        _deviceRepo.AddDevice(new Device
        {
            DeviceId = 30,
            DisplayName = "LAB101-RACK-SW-1",
            PrimaryIp = "192.168.1.2",
            MacAddress = "00:1A:2B:33:44:55",
            TypeId = (short)DeviceTypeEnum.AccessSwitch,
            GroupId = 1,
            IsMonitored = true
        });

        var request = new DeviceMaintenanceRequest(IsMonitored: false, Reason: "Unauthorized firmware test");

        // Act & Assert - IT Support must be rejected with 403 Forbidden
        var ex = await Assert.ThrowsAsync<UnauthorizedRoleException>(() =>
            _deviceService.ToggleMaintenanceAsync(
                deviceId: 30,
                request: request,
                userId: 5,
                operatorRole: RoleType.ItSupport,
                ipAddress: "10.0.0.50"
            ));

        Assert.Equal("AUTH_FORBIDDEN", ex.ErrorCode);
        Assert.Contains("IT Support operators cannot toggle maintenance mode on network switches or routers", ex.Message);
    }

    [Fact]
    public async Task ToggleMaintenanceAsync_WhenCoreRouterByItSupport_ThrowsUnauthorizedRoleException()
    {
        // Arrange: Core Router (type 1)
        _deviceRepo.AddDevice(new Device
        {
            DeviceId = 1,
            DisplayName = "CAMPUS-CORE-ROUTER",
            PrimaryIp = "10.0.0.1",
            MacAddress = "00:1A:2B:00:00:01",
            TypeId = (short)DeviceTypeEnum.CoreRouter,
            GroupId = 1,
            IsMonitored = true
        });

        var request = new DeviceMaintenanceRequest(IsMonitored: false);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedRoleException>(() =>
            _deviceService.ToggleMaintenanceAsync(1, request, 5, RoleType.ItSupport, "10.0.0.50"));
    }

    [Fact]
    public async Task ToggleMaintenanceAsync_WhenSwitchByNetworkEngineer_SuccessfullyToggles()
    {
        // Arrange: Access Switch (type 3)
        _deviceRepo.AddDevice(new Device
        {
            DeviceId = 30,
            DisplayName = "LAB101-RACK-SW-1",
            PrimaryIp = "192.168.1.2",
            MacAddress = "00:1A:2B:33:44:55",
            TypeId = (short)DeviceTypeEnum.AccessSwitch,
            GroupId = 1,
            IsMonitored = true
        });

        var request = new DeviceMaintenanceRequest(IsMonitored: false, Reason: "VLAN reconfiguration");

        // Act - Network Engineer is authorized
        var response = await _deviceService.ToggleMaintenanceAsync(
            deviceId: 30,
            request: request,
            userId: 2,
            operatorRole: RoleType.NetworkEngineer,
            ipAddress: "10.0.0.12"
        );

        // Assert
        Assert.NotNull(response);
        Assert.False(response.IsMonitored);
        Assert.Equal("UNKNOWN", response.StatusCode);

        var device = await _deviceRepo.GetByIdAsync(30);
        Assert.NotNull(device);
        Assert.False(device.IsMonitored);
    }

    [Fact]
    public async Task ToggleMaintenanceAsync_WhenSwitchByNetworkAdmin_SuccessfullyToggles()
    {
        // Arrange: Core Router (type 1)
        _deviceRepo.AddDevice(new Device
        {
            DeviceId = 1,
            DisplayName = "CAMPUS-CORE-ROUTER",
            PrimaryIp = "10.0.0.1",
            MacAddress = "00:1A:2B:00:00:01",
            TypeId = (short)DeviceTypeEnum.CoreRouter,
            GroupId = 1,
            IsMonitored = true
        });

        var request = new DeviceMaintenanceRequest(IsMonitored: false, Reason: "Core BGP maintenance");

        // Act - Network Admin is authorized
        var response = await _deviceService.ToggleMaintenanceAsync(
            deviceId: 1,
            request: request,
            userId: 1,
            operatorRole: RoleType.NetworkAdmin,
            ipAddress: "127.0.0.1"
        );

        // Assert
        Assert.NotNull(response);
        Assert.False(response.IsMonitored);
    }

    [Fact]
    public async Task ToggleMaintenanceAsync_WhenDeviceNotFound_ThrowsDeviceNotFoundException()
    {
        var request = new DeviceMaintenanceRequest(IsMonitored: false);

        await Assert.ThrowsAsync<DeviceNotFoundException>(() =>
            _deviceService.ToggleMaintenanceAsync(999, request, 1, RoleType.NetworkAdmin, "127.0.0.1"));
    }

    [Fact]
    public async Task ToggleMaintenanceAsync_WhenReenablingMonitoring_SetsIsMonitoredTrue()
    {
        // Arrange: PC previously under maintenance
        _deviceRepo.AddDevice(new Device
        {
            DeviceId = 20,
            DisplayName = "CS-LAB101-PC-15",
            PrimaryIp = "192.168.1.115",
            MacAddress = "00:1A:2B:11:22:33",
            TypeId = (short)DeviceTypeEnum.Pc,
            GroupId = 1,
            IsMonitored = false
        });

        var request = new DeviceMaintenanceRequest(IsMonitored: true, Reason: "Re-imaging completed");

        // Act
        var response = await _deviceService.ToggleMaintenanceAsync(20, request, 3, RoleType.ItSupport, "10.0.0.80");

        // Assert
        Assert.NotNull(response);
        Assert.True(response.IsMonitored);

        var device = await _deviceRepo.GetByIdAsync(20);
        Assert.NotNull(device);
        Assert.True(device.IsMonitored);
    }

    [Fact]
    public async Task GetDevicesAsync_WhenDeviceUnreachable_PopulatesLatestReachabilityWithUnreachableStatus()
    {
        // Arrange
        _deviceRepo.AddDevice(new Device
        {
            DeviceId = 101,
            DisplayName = "Test-Switch-01",
            PrimaryIp = "192.168.1.50",
            MacAddress = "00:1A:2B:50:50:50",
            TypeId = (short)DeviceTypeEnum.AccessSwitch,
            GroupId = 1,
            IsMonitored = true
        });

        _reachabilityRepo.SetLatestSample(101, new ReachabilitySample
        {
            DeviceId = 101,
            StatusId = 2, // UNREACHABLE
            LatencyUs = null,
            LossPercentage = 100f,
            IsSuppressed = false,
            RecordedAt = DateTimeOffset.UtcNow
        });

        // Act
        var result = await _deviceService.GetDevicesAsync(new DeviceQueryParameters());

        // Assert
        Assert.NotNull(result);
        var item = Assert.Single(result.Data, d => d.DeviceId == 101);
        Assert.NotNull(item.LatestReachability);
        Assert.Equal("UNREACHABLE", item.LatestReachability.StatusCode);
        Assert.Equal(2, item.LatestReachability.StatusId);
        Assert.Null(item.LatestReachability.LatencyUs);
        Assert.Equal(100m, item.LatestReachability.LossPercentage);
    }

    [Fact]
    public async Task GetDevicesAsync_WhenDeviceReachable_PopulatesLatestReachabilityWithLatency()
    {
        // Arrange
        _deviceRepo.AddDevice(new Device
        {
            DeviceId = 102,
            DisplayName = "Test-Router-01",
            PrimaryIp = "192.168.1.1",
            MacAddress = "00:1A:2B:01:01:01",
            TypeId = (short)DeviceTypeEnum.CoreRouter,
            GroupId = 1,
            IsMonitored = true
        });

        _reachabilityRepo.SetLatestSample(102, new ReachabilitySample
        {
            DeviceId = 102,
            StatusId = 1, // REACHABLE
            LatencyUs = 1250,
            LossPercentage = 0f,
            IsSuppressed = false,
            RecordedAt = DateTimeOffset.UtcNow
        });

        // Act
        var result = await _deviceService.GetDevicesAsync(new DeviceQueryParameters());

        // Assert
        Assert.NotNull(result);
        var item = Assert.Single(result.Data, d => d.DeviceId == 102);
        Assert.NotNull(item.LatestReachability);
        Assert.Equal("REACHABLE", item.LatestReachability.StatusCode);
        Assert.Equal(1, item.LatestReachability.StatusId);
        Assert.Equal(1250, item.LatestReachability.LatencyUs);
    }
}

internal class StubAuditLogRepository : IAuditLogRepository
{
    public List<AuditLog> Logs { get; } = new();
    private long _seq = 500;

    public Task<AuditLog> AddAsync(AuditLog log, CancellationToken ct = default)
    {
        if (log.LogId == 0)
        {
            log.LogId = ++_seq;
        }
        Logs.Add(log);
        return Task.FromResult(log);
    }

    public Task<AuditLog?> GetByIdAsync(long logId, CancellationToken ct = default)
    {
        return Task.FromResult(Logs.FirstOrDefault(l => l.LogId == logId));
    }

    public Task<IReadOnlyList<AuditLog>> GetRecentAsync(int limit = 50, CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<AuditLog>>(Logs.Take(limit).ToList());
    }

    public Task<(IReadOnlyList<AuditLog> Items, int TotalCount)> GetLogsPagedAsync(
        int? userId, string? actionCode, DateTimeOffset? start, DateTimeOffset? end, int page, int limit, CancellationToken ct = default)
    {
        var query = Logs.AsEnumerable();
        if (userId.HasValue) query = query.Where(l => l.UserId == userId.Value);
        if (!string.IsNullOrWhiteSpace(actionCode)) query = query.Where(l => l.ActionCode == actionCode.ToUpperInvariant());
        if (start.HasValue) query = query.Where(l => l.LoggedAt >= start.Value);
        if (end.HasValue) query = query.Where(l => l.LoggedAt <= end.Value);
        var list = query.OrderByDescending(l => l.LoggedAt).ToList();
        var items = list.Skip((page - 1) * limit).Take(limit).ToList();
        return Task.FromResult<(IReadOnlyList<AuditLog>, int)>((items, list.Count));
    }
}
