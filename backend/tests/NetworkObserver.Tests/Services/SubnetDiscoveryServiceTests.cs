using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using NetworkObserver.Application.DTOs;
using NetworkObserver.Application.Interfaces.Discovery;
using NetworkObserver.Application.Interfaces.Telemetry;
using NetworkObserver.Application.Services;
using NetworkObserver.Domain.Entities;
using NetworkObserver.Domain.Exceptions;
using Xunit;

namespace NetworkObserver.Tests.Services;

public class SubnetDiscoveryServiceTests
{
    private readonly StubPingProbeFactory _pingProbeFactory;
    private readonly StubArpResolver _arpResolver;
    private readonly StubMacVendorLookup _macVendorLookup;
    private readonly StubDeviceRepository _deviceRepo;
    private readonly SubnetDiscoveryService _discoveryService;

    public SubnetDiscoveryServiceTests()
    {
        _pingProbeFactory = new StubPingProbeFactory();
        _arpResolver = new StubArpResolver();
        _macVendorLookup = new StubMacVendorLookup();
        _deviceRepo = new StubDeviceRepository();

        _discoveryService = new SubnetDiscoveryService(
            _pingProbeFactory,
            _arpResolver,
            _macVendorLookup,
            _deviceRepo,
            NullLogger<SubnetDiscoveryService>.Instance
        );
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task TriggerScanAsync_WhenCidrEmpty_ThrowsSubnetValidationException(string? invalidCidr)
    {
        var request = new SubnetScanRequest(SubnetCidr: invalidCidr!);

        var ex = await Assert.ThrowsAsync<SubnetValidationException>(() =>
            _discoveryService.TriggerScanAsync(request, siteId: 1));

        Assert.Equal("SUBNET_VALIDATION_ERROR", ex.ErrorCode);
    }

    [Theory]
    [InlineData("192.168.1.0")]
    [InlineData("not-a-cidr")]
    [InlineData("256.100.0.1/24")]
    [InlineData("192.168.1.0/abc")]
    public async Task TriggerScanAsync_WhenCidrFormatInvalid_ThrowsSubnetValidationException(string invalidCidr)
    {
        var request = new SubnetScanRequest(SubnetCidr: invalidCidr);

        await Assert.ThrowsAsync<SubnetValidationException>(() =>
            _discoveryService.TriggerScanAsync(request, siteId: 1));
    }

    [Theory]
    [InlineData("192.168.0.0/16")]
    [InlineData("10.0.0.0/8")]
    [InlineData("172.16.0.0/20")]
    [InlineData("192.168.1.0/23")]
    public async Task TriggerScanAsync_WhenCidrTooLarge_ThrowsSubnetValidationException_WithCidrTooLargeCode(string largeCidr)
    {
        var request = new SubnetScanRequest(SubnetCidr: largeCidr);

        var ex = await Assert.ThrowsAsync<SubnetValidationException>(() =>
            _discoveryService.TriggerScanAsync(request, siteId: 1));

        Assert.Equal("CIDR_TOO_LARGE", ex.ErrorCode);
        Assert.Contains("Maximum supported range is /24 (254 hosts)", ex.Message);
    }

    [Fact]
    public async Task TriggerScanAsync_WhenPrefixGreaterThan32_ThrowsSubnetValidationException()
    {
        var request = new SubnetScanRequest(SubnetCidr: "192.168.1.0/33");

        var ex = await Assert.ThrowsAsync<SubnetValidationException>(() =>
            _discoveryService.TriggerScanAsync(request, siteId: 1));

        Assert.Contains("cannot exceed /32", ex.Message);
    }

    [Fact]
    public async Task TriggerScanAsync_WhenValidCidr24_InitializesScanWith254TotalIps_AndReturnsRunning()
    {
        // Use a unique site ID to prevent cross-test lock interference
        int testSiteId = 101;
        var request = new SubnetScanRequest(SubnetCidr: "192.168.10.0/24");

        // Act
        var response = await _discoveryService.TriggerScanAsync(request, siteId: testSiteId);

        // Assert
        Assert.NotNull(response);
        Assert.StartsWith("scan_", response.ScanId);
        Assert.Equal("RUNNING", response.Status);
        Assert.Equal("192.168.10.0/24", response.TargetCidr);
        Assert.Equal(254, response.TotalIps);
    }

    [Fact]
    public async Task TriggerScanAsync_WhenConcurrentScanActiveForSameSite_ThrowsScanConflictException()
    {
        int testSiteId = 202;
        var request1 = new SubnetScanRequest(SubnetCidr: "192.168.20.0/24");
        var request2 = new SubnetScanRequest(SubnetCidr: "192.168.30.0/24");

        // Act 1: Initial scan starts successfully
        var scan1 = await _discoveryService.TriggerScanAsync(request1, siteId: testSiteId);
        Assert.Equal("RUNNING", scan1.Status);

        // Act 2: Second concurrent scan on same site must be rejected with 409 Conflict
        var ex = await Assert.ThrowsAsync<ScanConflictException>(() =>
            _discoveryService.TriggerScanAsync(request2, siteId: testSiteId));

        Assert.Equal("SCAN_ALREADY_IN_PROGRESS", ex.ErrorCode);
        Assert.Contains("already active for this site", ex.Message);
    }

    [Fact]
    public async Task GetScanStatusAsync_WhenScanNotFound_ThrowsScanNotFoundException()
    {
        await Assert.ThrowsAsync<ScanNotFoundException>(() =>
            _discoveryService.GetScanStatusAsync("scan_non_existent"));
    }

    [Fact]
    public async Task CancelScanAsync_WhenScanRunning_CancelsScanAndReleasesSiteLock()
    {
        int testSiteId = 303;
        var request = new SubnetScanRequest(SubnetCidr: "192.168.50.0/24");

        var init = await _discoveryService.TriggerScanAsync(request, siteId: testSiteId);
        Assert.Equal("RUNNING", init.Status);

        // Cancel scan
        var cancelled = await _discoveryService.CancelScanAsync(init.ScanId);
        Assert.True(cancelled);

        var status = await _discoveryService.GetScanStatusAsync(init.ScanId);
        Assert.Equal("CANCELLED", status.Status);

        // Can start another scan now that lock is released
        var newScan = await _discoveryService.TriggerScanAsync(request, siteId: testSiteId);
        Assert.Equal("RUNNING", newScan.Status);
    }

    [Fact]
    public async Task RunSweepAsync_DiscoversActiveDevices_WithVendorLookupAndRegistrationCheck()
    {
        int testSiteId = 404;
        // Pre-register one device in the inventory
        _deviceRepo.AddDevice(new Device
        {
            DeviceId = 5,
            DisplayName = "EXISTING-PC-01",
            PrimaryIp = "192.168.99.10",
            MacAddress = "00:14:22:AA:BB:CC", // Dell MAC
            TypeId = 5,
            GroupId = 1
        });

        // Configure probe responses:
        // 192.168.99.1 is a Cisco gateway router (Ping OK, SendARP OK)
        _pingProbeFactory.ConfigureResult("192.168.99.1", new PingProbeResult(true, 100, 0f));
        _arpResolver.Configure("192.168.99.1", "00:1A:2B:11:22:33");
        _macVendorLookup.Configure("00:1A:2B:11:22:33", "Cisco");

        // 192.168.99.10 is the pre-registered Dell PC
        _pingProbeFactory.ConfigureResult("192.168.99.10", new PingProbeResult(true, 150, 0f));
        _arpResolver.Configure("192.168.99.10", "00:14:22:AA:BB:CC");
        _macVendorLookup.Configure("00:14:22:AA:BB:CC", "Dell");

        // Trigger small /30 subnet scan (192.168.99.8/30 -> host IPs: .9 and .10)
        var request = new SubnetScanRequest(SubnetCidr: "192.168.99.8/30"); // usable: .9, .10

        var init = await _discoveryService.TriggerScanAsync(request, siteId: testSiteId);

        // Await background worker completion with bounded timeout
        SubnetScanStatusResponse? status = null;
        for (int i = 0; i < 40; i++)
        {
            status = await _discoveryService.GetScanStatusAsync(init.ScanId);
            if (status.Status != "RUNNING")
            {
                break;
            }
            await Task.Delay(50);
        }

        Assert.NotNull(status);
        Assert.Equal("COMPLETED", status.Status);
        Assert.Equal(100.0, status.ProgressPct);

        var asset10 = status.DiscoveredAssets.FirstOrDefault(a => a.PrimaryIp == "192.168.99.10");
        Assert.NotNull(asset10);
        Assert.Equal("00:14:22:AA:BB:CC", asset10.MacAddress);
        Assert.Equal("Dell", asset10.Vendor);
        Assert.Equal("PC", asset10.InferredType);
        Assert.True(asset10.IsAlreadyRegistered); // Already in inventory!
    }

    [Fact]
    public async Task GetActiveOrLatestScanAsync_WhenCalled_ReturnsBaselineOrActiveScan()
    {
        var scan = await _discoveryService.GetActiveOrLatestScanAsync(siteId: 1);
        Assert.NotNull(scan);
        Assert.NotEmpty(scan.DiscoveredAssets);
        Assert.True(scan.ProgressPct >= 0);
    }
}

internal class StubArpResolver : IArpResolver
{
    private readonly Dictionary<string, string> _arpTable = new();

    public void Configure(string ip, string mac)
    {
        _arpTable[ip] = mac;
    }

    public Task<string?> ResolveMacAddressAsync(IPAddress ipAddress, CancellationToken ct = default)
    {
        _arpTable.TryGetValue(ipAddress.ToString(), out var mac);
        return Task.FromResult<string?>(mac);
    }
}

internal class StubMacVendorLookup : IMacVendorLookup
{
    private readonly Dictionary<string, string> _vendors = new(StringComparer.OrdinalIgnoreCase);

    public void Configure(string mac, string vendor)
    {
        _vendors[mac] = vendor;
    }

    public string? LookupVendor(string macAddress)
    {
        return _vendors.TryGetValue(macAddress, out var v) ? v : "Unknown Vendor";
    }
}
