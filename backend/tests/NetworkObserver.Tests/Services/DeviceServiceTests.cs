using NetworkObserver.Application.DTOs;
using NetworkObserver.Application.Interfaces.Repositories;
using NetworkObserver.Application.Services;
using NetworkObserver.Domain.Entities;
using NetworkObserver.Domain.Enums;
using NetworkObserver.Domain.Exceptions;
using Xunit;

namespace NetworkObserver.Tests.Services;

public class DeviceServiceTests
{
    private readonly StubDeviceRepository _deviceRepo;
    private readonly StubGroupRepository _groupRepo;
    private readonly DeviceService _deviceService;

    public DeviceServiceTests()
    {
        _deviceRepo = new StubDeviceRepository();
        _groupRepo = new StubGroupRepository();
        _groupRepo.AddGroup(new Group { GroupId = 1, GroupName = "Unassigned Group" });
        _groupRepo.AddGroup(new Group { GroupId = 3, GroupName = "Assembly Line A" });

        _deviceService = new DeviceService(_deviceRepo, _groupRepo);
    }

    [Fact]
    public async Task GetDevicesAsync_ReturnsPagedDevices_WithPaginationMetadata()
    {
        // Arrange
        _deviceRepo.AddDevice(new Device { DeviceId = 1, DisplayName = "CORE-R1", PrimaryIp = "192.168.1.1", MacAddress = "00:1A:2B:00:00:01", TypeId = 1, GroupId = 1 });
        _deviceRepo.AddDevice(new Device { DeviceId = 2, DisplayName = "ACCESS-SW-1", PrimaryIp = "192.168.1.2", MacAddress = "00:1A:2B:00:00:02", TypeId = 3, GroupId = 1 });

        var parameters = new DeviceQueryParameters(Page: 1, Limit: 10);

        // Act
        var result = await _deviceService.GetDevicesAsync(parameters);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Data.Count);
        Assert.Equal(2, result.Pagination.TotalRecords);
        Assert.Equal(1, result.Pagination.CurrentPage);
        Assert.False(result.Pagination.HasNext);
    }

    [Fact]
    public async Task GetDeviceByIdAsync_WhenExists_ReturnsDetail()
    {
        // Arrange
        _deviceRepo.AddDevice(new Device { DeviceId = 10, DisplayName = "PLC-01", PrimaryIp = "192.168.1.101", MacAddress = "00:1A:2B:AA:01:05", TypeId = 5, GroupId = 3 });

        // Act
        var result = await _deviceService.GetDeviceByIdAsync(10);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(10, result.DeviceId);
        Assert.Equal("PLC-01", result.DisplayName);
        Assert.Equal("PC", result.TypeCode);
    }

    [Fact]
    public async Task GetDeviceByIdAsync_WhenNotFound_ThrowsDeviceNotFoundException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<DeviceNotFoundException>(() => _deviceService.GetDeviceByIdAsync(999));
    }

    [Fact]
    public async Task CreateDeviceAsync_WhenAdminCreatesCoreRouter_Succeeds()
    {
        // Arrange
        var request = new CreateDeviceRequest(
            DisplayName: "CORE-ROUTER-01",
            PrimaryIp: "10.0.0.1",
            MacAddress: "AA:BB:CC:DD:EE:01",
            TypeId: (short)DeviceTypeEnum.CoreRouter,
            GroupId: 1,
            ParentDeviceId: null,
            Vendor: "Cisco",
            Model: "Catalyst 9500"
        );

        // Act
        var result = await _deviceService.CreateDeviceAsync(request, RoleType.NetworkAdmin);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("CORE-ROUTER-01", result.DisplayName);
        Assert.Equal("AA:BB:CC:DD:EE:01", result.MacAddress);
    }

    [Fact]
    public async Task CreateDeviceAsync_WhenEngineerCreatesCoreRouter_ThrowsUnauthorizedRoleException()
    {
        // Arrange
        var request = new CreateDeviceRequest(
            DisplayName: "CORE-ROUTER-01",
            PrimaryIp: "10.0.0.1",
            MacAddress: "AA:BB:CC:DD:EE:01",
            TypeId: (short)DeviceTypeEnum.CoreRouter, // Restricted to Admin
            GroupId: 1,
            ParentDeviceId: null,
            Vendor: "Cisco",
            Model: "Catalyst 9500"
        );

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedRoleException>(() =>
            _deviceService.CreateDeviceAsync(request, RoleType.NetworkEngineer));
        Assert.Equal("AUTH_FORBIDDEN", ex.ErrorCode);
    }

    [Fact]
    public async Task CreateDeviceAsync_WhenSupportCreatesAccessSwitch_ThrowsUnauthorizedRoleException()
    {
        // Arrange
        var request = new CreateDeviceRequest(
            DisplayName: "ACCESS-SW-01",
            PrimaryIp: "192.168.1.5",
            MacAddress: "AA:BB:CC:DD:EE:02",
            TypeId: (short)DeviceTypeEnum.AccessSwitch, // Restricted to Admin/Engineer
            GroupId: 1,
            ParentDeviceId: null,
            Vendor: "Cisco",
            Model: "Catalyst 2960"
        );

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedRoleException>(() =>
            _deviceService.CreateDeviceAsync(request, RoleType.ItSupport));
        Assert.Equal("AUTH_FORBIDDEN", ex.ErrorCode);
    }

    [Fact]
    public async Task CreateDeviceAsync_WhenSupportCreatesPc_Succeeds()
    {
        // Arrange
        var request = new CreateDeviceRequest(
            DisplayName: "OPERATOR-WORKSTATION-1",
            PrimaryIp: "192.168.1.50",
            MacAddress: "AA:BB:CC:DD:EE:50",
            TypeId: (short)DeviceTypeEnum.Pc, // Permitted for all roles
            GroupId: 3,
            ParentDeviceId: null,
            Vendor: "Dell",
            Model: "OptiPlex 7090"
        );

        // Act
        var result = await _deviceService.CreateDeviceAsync(request, RoleType.ItSupport);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("OPERATOR-WORKSTATION-1", result.DisplayName);
    }

    [Fact]
    public async Task CreateDeviceAsync_WhenDuplicateMac_ThrowsDuplicateMacException()
    {
        // Arrange
        _deviceRepo.AddDevice(new Device { DeviceId = 1, DisplayName = "D1", PrimaryIp = "192.168.1.1", MacAddress = "00:1A:2B:3C:4D:5E", TypeId = 5, GroupId = 1 });

        var request = new CreateDeviceRequest(
            DisplayName: "Duplicate MAC Dev",
            PrimaryIp: "192.168.1.200",
            MacAddress: "00-1A-2B-3C-4D-5E", // Same MAC with hyphens
            TypeId: 5,
            GroupId: 1,
            ParentDeviceId: null
        );

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DuplicateMacException>(() =>
            _deviceService.CreateDeviceAsync(request, RoleType.NetworkAdmin));
        Assert.Equal("DUPLICATE_MAC_ADDRESS", ex.ErrorCode);
    }

    [Fact]
    public async Task CreateDeviceAsync_WhenInvalidIp_ThrowsInvalidDeviceException()
    {
        // Arrange
        var request = new CreateDeviceRequest(
            DisplayName: "Invalid IP Dev",
            PrimaryIp: "999.999.999.999",
            MacAddress: "AA:BB:CC:DD:EE:99",
            TypeId: 5,
            GroupId: 1,
            ParentDeviceId: null
        );

        // Act & Assert
        await Assert.ThrowsAsync<InvalidDeviceException>(() =>
            _deviceService.CreateDeviceAsync(request, RoleType.NetworkAdmin));
    }

    [Fact]
    public async Task CreateDeviceAsync_WhenInvalidMac_ThrowsInvalidDeviceException()
    {
        // Arrange
        var request = new CreateDeviceRequest(
            DisplayName: "Invalid MAC Dev",
            PrimaryIp: "192.168.1.20",
            MacAddress: "NOT-A-VALID-MAC",
            TypeId: 5,
            GroupId: 1,
            ParentDeviceId: null
        );

        // Act & Assert
        await Assert.ThrowsAsync<InvalidDeviceException>(() =>
            _deviceService.CreateDeviceAsync(request, RoleType.NetworkAdmin));
    }

    [Fact]
    public async Task UpdateDeviceAsync_WhenMacAddressChanged_ThrowsMacAddressImmutableException()
    {
        // Arrange
        _deviceRepo.AddDevice(new Device
        {
            DeviceId = 54,
            DisplayName = "PLC-01",
            PrimaryIp = "192.168.1.104",
            MacAddress = "00:1A:2B:AA:01:08",
            TypeId = 5,
            GroupId = 3
        });

        var request = new UpdateDeviceRequest(
            DisplayName: "PLC-01-REV2",
            PrimaryIp: "192.168.1.120",
            TypeId: 5,
            GroupId: 3,
            ParentDeviceId: null,
            Vendor: "Siemens",
            Model: "S7",
            Zone: "Floor 1",
            IsMonitored: true,
            MacAddress: "00:1A:2B:AA:01:99" // Attempted MAC change!
        );

        // Act & Assert
        var ex = await Assert.ThrowsAsync<MacAddressImmutableException>(() =>
            _deviceService.UpdateDeviceAsync(54, request, RoleType.NetworkAdmin));
        Assert.Equal("DEVICE_MAC_IMMUTABLE", ex.ErrorCode);
    }

    [Fact]
    public async Task UpdateDeviceAsync_WhenValidInput_UpdatesDeviceAndReturnsDetail()
    {
        // Arrange
        _deviceRepo.AddDevice(new Device
        {
            DeviceId = 54,
            DisplayName = "PLC-01",
            PrimaryIp = "192.168.1.104",
            MacAddress = "00:1A:2B:AA:01:08",
            TypeId = 5,
            GroupId = 3
        });

        var request = new UpdateDeviceRequest(
            DisplayName: "PLC-LINE-A-04-REV2",
            PrimaryIp: "192.168.1.120",
            TypeId: 5,
            GroupId: 3,
            ParentDeviceId: null,
            Vendor: "Siemens",
            Model: "Simatic S7-1500",
            Zone: "Floor 1 Machine Bay 4",
            IsMonitored: true,
            MacAddress: "00:1A:2B:AA:01:08" // Matching MAC (permitted)
        );

        // Act
        var result = await _deviceService.UpdateDeviceAsync(54, request, RoleType.NetworkEngineer);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("PLC-LINE-A-04-REV2", result.DisplayName);
        Assert.Equal("192.168.1.120", result.PrimaryIp);
        Assert.Equal("00:1A:2B:AA:01:08", result.MacAddress);
    }

    [Fact]
    public async Task DeleteDeviceAsync_WhenUpstreamSwitchDeleted_SafelyOrphansChildrenAndReturnsDetails()
    {
        // Arrange
        var sw = new Device { DeviceId = 3, DisplayName = "ACCESS-SW-A", TypeId = (short)DeviceTypeEnum.AccessSwitch, GroupId = 1, MacAddress = "00:11:22:33:44:55", PrimaryIp = "192.168.1.10" };
        var c1 = new Device { DeviceId = 10, DisplayName = "PLC-1", ParentDeviceId = 3, TypeId = 5, GroupId = 1, MacAddress = "00:11:22:33:44:56", PrimaryIp = "192.168.1.11" };
        var c2 = new Device { DeviceId = 11, DisplayName = "PLC-2", ParentDeviceId = 3, TypeId = 5, GroupId = 1, MacAddress = "00:11:22:33:44:57", PrimaryIp = "192.168.1.12" };

        _deviceRepo.AddDevice(sw);
        _deviceRepo.AddDevice(c1);
        _deviceRepo.AddDevice(c2);

        // Act - Network Engineer deletes Access Switch
        var result = await _deviceService.DeleteDeviceAsync(3, RoleType.NetworkEngineer);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.DeletedDeviceId);
        Assert.Equal("ACCESS-SW-A", result.DeletedDisplayName);
        Assert.Equal(2, result.OrphanedChildrenCount);
        Assert.Contains(10, result.OrphanedChildrenDeviceIds);
        Assert.Contains(11, result.OrphanedChildrenDeviceIds);

        // Ensure child devices are now orphaned (parent = null) and still exist
        var child1 = await _deviceRepo.GetByIdAsync(10);
        Assert.NotNull(child1);
        Assert.Null(child1.ParentDeviceId);
    }

    [Fact]
    public async Task DeleteDeviceAsync_WhenSupportDeletesSwitch_ThrowsUnauthorizedRoleException()
    {
        // Arrange
        _deviceRepo.AddDevice(new Device { DeviceId = 3, DisplayName = "ACCESS-SW-A", TypeId = (short)DeviceTypeEnum.AccessSwitch, GroupId = 1, MacAddress = "00:11:22:33:44:55", PrimaryIp = "192.168.1.10" });

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedRoleException>(() =>
            _deviceService.DeleteDeviceAsync(3, RoleType.ItSupport));
    }
}

/// <summary>
/// Fast in-memory stub of IDeviceRepository for unit testing.
/// </summary>
internal class StubDeviceRepository : IDeviceRepository
{
    private readonly List<Device> _devices = new();
    private int _sequence = 100;

    public void AddDevice(Device device)
    {
        if (device.DeviceId == 0)
        {
            device.DeviceId = ++_sequence;
        }
        _devices.Add(device);
    }

    public Task<(IReadOnlyList<Device> Items, int TotalCount)> GetPagedAsync(DeviceQueryParameters parameters, CancellationToken ct = default)
    {
        var items = _devices.Skip((parameters.Page - 1) * parameters.Limit).Take(parameters.Limit).ToList();
        return Task.FromResult<(IReadOnlyList<Device>, int)>((items, _devices.Count));
    }

    public Task<Device?> GetByIdAsync(int deviceId, CancellationToken ct = default)
    {
        return Task.FromResult(_devices.FirstOrDefault(d => d.DeviceId == deviceId));
    }

    public Task<Device?> GetByMacAsync(string macAddress, CancellationToken ct = default)
    {
        return Task.FromResult(_devices.FirstOrDefault(d => string.Equals(d.MacAddress, macAddress, StringComparison.OrdinalIgnoreCase)));
    }

    public Task<bool> ExistsAsync(int deviceId, CancellationToken ct = default)
    {
        return Task.FromResult(_devices.Any(d => d.DeviceId == deviceId));
    }

    public Task<bool> ExistsByMacAsync(string macAddress, CancellationToken ct = default)
    {
        return Task.FromResult(_devices.Any(d => string.Equals(d.MacAddress, macAddress, StringComparison.OrdinalIgnoreCase)));
    }

    public Task AddAsync(Device device, CancellationToken ct = default)
    {
        AddDevice(device);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Device device, CancellationToken ct = default)
    {
        var existing = _devices.FirstOrDefault(d => d.DeviceId == device.DeviceId);
        if (existing != null)
        {
            _devices.Remove(existing);
        }
        _devices.Add(device);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<int>> DeleteAndOrphanChildrenAsync(int deviceId, CancellationToken ct = default)
    {
        var device = _devices.FirstOrDefault(d => d.DeviceId == deviceId);
        if (device == null)
        {
            return Task.FromResult<IReadOnlyList<int>>(Array.Empty<int>());
        }

        var children = _devices.Where(d => d.ParentDeviceId == deviceId).ToList();
        var orphanedIds = children.Select(c => c.DeviceId).ToList();

        foreach (var child in children)
        {
            child.ParentDeviceId = null;
        }

        _devices.Remove(device);
        return Task.FromResult<IReadOnlyList<int>>(orphanedIds);
    }
}
