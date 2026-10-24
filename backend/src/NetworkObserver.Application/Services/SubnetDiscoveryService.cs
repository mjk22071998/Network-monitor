using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NetworkObserver.Application.DTOs;
using NetworkObserver.Application.Interfaces.Discovery;
using NetworkObserver.Application.Interfaces.Repositories;
using NetworkObserver.Application.Interfaces.Telemetry;
using NetworkObserver.Domain.Constants;
using NetworkObserver.Domain.Entities;
using NetworkObserver.Domain.Exceptions;

namespace NetworkObserver.Application.Services;

/// <summary>
/// Domain application service implementing Asynchronous Subnet Discovery Sweeps,
/// 10ms packet pacing, single concurrent scan locks, and hardware OUI heuristics.
/// Complies with api_contracts_specification.md Section 8.
/// </summary>
public class SubnetDiscoveryService : ISubnetDiscoveryService
{
    private readonly IPingProbeFactory _pingProbeFactory;
    private readonly IArpResolver _arpResolver;
    private readonly IMacVendorLookup _macVendorLookup;
    private readonly IDeviceRepository _deviceRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly ILogger<SubnetDiscoveryService> _logger;

    // Singleton scan state storage across concurrent requests
    private static readonly ConcurrentDictionary<string, DiscoveryScanState> Scans = new();
    private static readonly ConcurrentDictionary<int, string> ActiveScanPerSite = new();

    public SubnetDiscoveryService(
        IPingProbeFactory pingProbeFactory,
        IArpResolver arpResolver,
        IMacVendorLookup macVendorLookup,
        IDeviceRepository deviceRepository,
        ILogger<SubnetDiscoveryService> logger,
        IServiceScopeFactory? scopeFactory = null,
        IAuditLogRepository? auditLogRepository = null)
    {
        _pingProbeFactory = pingProbeFactory;
        _arpResolver = arpResolver;
        _macVendorLookup = macVendorLookup;
        _deviceRepository = deviceRepository;
        _logger = logger;
        _scopeFactory = scopeFactory;
        _auditLogRepository = auditLogRepository ?? new NullAuditLogRepository();
    }

    public async Task<SubnetScanInitResponse> TriggerScanAsync(
        SubnetScanRequest request,
        int siteId,
        int? userId = null,
        string ipAddress = "127.0.0.1",
        CancellationToken ct = default)
    {
        var targetCidr = request.EffectiveCidr;
        if (string.IsNullOrWhiteSpace(targetCidr))
        {
            throw new SubnetValidationException("Subnet CIDR is required.");
        }

        var hostIps = ParseAndValidateCidr(targetCidr);

        // Enforce max 1 concurrent scan per site (api_contracts_specification.md Section 8.1)
        if (ActiveScanPerSite.TryGetValue(siteId, out var existingScanId))
        {
            if (Scans.TryGetValue(existingScanId, out var existingState) && existingState.Status == "RUNNING")
            {
                throw new ScanConflictException("A discovery scan is already active for this site.");
            }
        }

        var scanId = $"scan_{Guid.NewGuid():N}"[..15];
        var state = new DiscoveryScanState(
            scanId: scanId,
            siteId: siteId,
            targetCidr: targetCidr,
            totalIps: hostIps.Count,
            startedAt: DateTimeOffset.UtcNow
        );

        Scans[scanId] = state;
        ActiveScanPerSite[siteId] = scanId;

        // Dispatch background worker asynchronously
        _ = RunSweepAsync(state, hostIps, siteId);

        await _auditLogRepository.AddAsync(new AuditLog
        {
            UserId = userId,
            SiteId = siteId,
            ActionCode = AuditActionCodes.ScanTrigger,
            TargetEntity = AuditTargetEntities.Network,
            TargetId = scanId,
            Details = $"Initiated subnet discovery scan on CIDR {targetCidr} (Host count: {hostIps.Count}).",
            IpAddress = string.IsNullOrWhiteSpace(ipAddress) ? "127.0.0.1" : ipAddress.Trim(),
            LoggedAt = DateTimeOffset.UtcNow
        }, ct);

        var response = new SubnetScanInitResponse(
            ScanId: scanId,
            Status: "RUNNING",
            TargetCidr: targetCidr,
            TotalIps: hostIps.Count,
            StartedAt: state.StartedAt
        );

        return response;
    }

    public Task<SubnetScanStatusResponse> GetScanStatusAsync(
        string scanId,
        CancellationToken ct = default)
    {
        if (!Scans.TryGetValue(scanId, out var state))
        {
            throw new ScanNotFoundException(scanId);
        }

        return Task.FromResult(MapToStatusResponse(state));
    }

    public Task<SubnetScanStatusResponse?> GetActiveOrLatestScanAsync(int siteId = 1, CancellationToken ct = default)
    {
        // 1. Check if there is an active running scan for this site
        if (ActiveScanPerSite.TryGetValue(siteId, out var activeScanId) &&
            Scans.TryGetValue(activeScanId, out var activeState) &&
            activeState.Status == "RUNNING")
        {
            return Task.FromResult<SubnetScanStatusResponse?>(MapToStatusResponse(activeState));
        }

        // 2. Otherwise return the latest scan for this site
        var latestScan = Scans.Values
            .Where(s => s.SiteId == siteId)
            .OrderByDescending(s => s.StartedAt)
            .FirstOrDefault();

        if (latestScan != null)
        {
            return Task.FromResult<SubnetScanStatusResponse?>(MapToStatusResponse(latestScan));
        }

        return Task.FromResult<SubnetScanStatusResponse?>(null);
    }

    private static SubnetScanStatusResponse MapToStatusResponse(DiscoveryScanState state)
    {
        var assets = state.DiscoveredAssets.OrderBy(a => a.PrimaryIp).ToList();
        return new SubnetScanStatusResponse(
            ScanId: state.ScanId,
            Status: state.Status,
            ProgressPct: state.ProgressPct,
            DiscoveredAssets: assets,
            TargetCidr: state.TargetCidr,
            TotalIps: state.TotalIps,
            ScannedIps: state.ScannedIps
        );
    }

    public async Task<bool> CancelScanAsync(string scanId, int? userId = null, string ipAddress = "127.0.0.1", CancellationToken ct = default)
    {
        if (!Scans.TryGetValue(scanId, out var state))
        {
            throw new ScanNotFoundException(scanId);
        }

        if (state.Status == "RUNNING")
        {
            state.Cts.Cancel();
            state.Status = "CANCELLED";
            ActiveScanPerSite.TryRemove(state.SiteId, out _);

            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserId = userId,
                SiteId = state.SiteId,
                ActionCode = AuditActionCodes.ScanCancel,
                TargetEntity = AuditTargetEntities.Network,
                TargetId = scanId,
                Details = $"Cancelled subnet discovery scan {scanId}.",
                IpAddress = string.IsNullOrWhiteSpace(ipAddress) ? "127.0.0.1" : ipAddress.Trim(),
                LoggedAt = DateTimeOffset.UtcNow
            }, ct);

            return true;
        }

        return false;
    }

    private async Task RunSweepAsync(DiscoveryScanState state, IReadOnlyList<string> hostIps, int siteId)
    {
        var pingStrategy = _pingProbeFactory.CreateStrategy();
        var ct = state.Cts.Token;

        using var scope = _scopeFactory?.CreateScope();
        var deviceRepo = scope != null
            ? scope.ServiceProvider.GetRequiredService<IDeviceRepository>()
            : _deviceRepository;

        try
        {
            int scannedCount = 0;

            foreach (var ip in hostIps)
            {
                if (ct.IsCancellationRequested)
                {
                    state.Status = "CANCELLED";
                    break;
                }

                // 10ms packet pacing between probes to prevent network congestion (FR-DISC-02)
                try
                {
                    await Task.Delay(10, ct);
                }
                catch (OperationCanceledException)
                {
                    state.Status = "CANCELLED";
                    break;
                }

                try
                {
                    var pingResult = await pingStrategy.PingAsync(ip, timeoutMs: 500, ct);
                    var ipObj = IPAddress.Parse(ip);
                    var mac = await _arpResolver.ResolveMacAddressAsync(ipObj, ct);

                    if (pingResult.IsSuccess || !string.IsNullOrWhiteSpace(mac))
                    {
                        var resolvedMac = !string.IsNullOrWhiteSpace(mac)
                            ? mac
                            : GenerateDeterministicSyntheticMac(ip);

                        var vendor = _macVendorLookup.LookupVendor(resolvedMac);
                        var isRegistered = await deviceRepo.ExistsByMacAsync(resolvedMac, ct);
                        var inferredType = InferDeviceType(ip, vendor);

                        var asset = new DiscoveredAssetDto(
                            PrimaryIp: ip,
                            MacAddress: resolvedMac,
                            Vendor: vendor,
                            InferredType: inferredType,
                            IsAlreadyRegistered: isRegistered,
                            ResponseTimeMs: pingResult.IsSuccess && pingResult.LatencyUs.HasValue
                                ? Math.Round(pingResult.LatencyUs.Value / 1000.0, 2)
                                : null
                        );

                        state.DiscoveredAssets.Add(asset);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogDebug(ex, "Discovery probe failed for IP {Ip}: {Message}", ip, ex.Message);
                }

                scannedCount++;
                state.ScannedIps = scannedCount;
                state.ProgressPct = Math.Round((double)scannedCount / hostIps.Count * 100.0, 1);
            }

            if (state.Status == "RUNNING")
            {
                state.Status = "COMPLETED";
                state.ProgressPct = 100.0;
                state.ScannedIps = hostIps.Count;
            }
        }
        catch (OperationCanceledException)
        {
            state.Status = "CANCELLED";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in discovery scan {ScanId}: {Message}", state.ScanId, ex.Message);
            state.Status = "FAILED";
        }
        finally
        {
            ActiveScanPerSite.TryRemove(siteId, out _);
        }
    }

    private static IReadOnlyList<string> ParseAndValidateCidr(string cidr)
    {
        var parts = cidr.Trim().Split('/');
        if (parts.Length != 2)
        {
            throw new SubnetValidationException("Invalid CIDR format. Expected IPv4 subnet notation, e.g. 192.168.1.0/24.");
        }

        if (!IPAddress.TryParse(parts[0], out var ip) || ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            throw new SubnetValidationException("Invalid IPv4 address in CIDR.");
        }

        if (!int.TryParse(parts[1], out var prefix))
        {
            throw new SubnetValidationException("Invalid subnet mask prefix.");
        }

        if (prefix < 24)
        {
            throw new SubnetValidationException(
                "Subnet mask too large. Maximum supported range is /24 (254 hosts) to prevent network congestion.",
                "CIDR_TOO_LARGE");
        }

        if (prefix > 32)
        {
            throw new SubnetValidationException("Subnet mask prefix cannot exceed /32.");
        }

        var ipBytes = ip.GetAddressBytes();
        uint ipUint = (uint)((ipBytes[0] << 24) | (ipBytes[1] << 16) | (ipBytes[2] << 8) | ipBytes[3]);
        uint mask = prefix == 0 ? 0 : 0xFFFFFFFF << (32 - prefix);
        uint networkUint = ipUint & mask;
        uint broadcastUint = networkUint | ~mask;

        var hostIps = new List<string>();

        if (prefix == 32)
        {
            hostIps.Add(UintToIp(networkUint));
        }
        else if (prefix == 31)
        {
            hostIps.Add(UintToIp(networkUint));
            hostIps.Add(UintToIp(broadcastUint));
        }
        else
        {
            // Standard /24 through /30 ranges: omit network and broadcast addresses
            for (uint current = networkUint + 1; current < broadcastUint; current++)
            {
                hostIps.Add(UintToIp(current));
            }
        }

        return hostIps;
    }

    private static string UintToIp(uint value)
    {
        return $"{(value >> 24) & 0xFF}.{(value >> 16) & 0xFF}.{(value >> 8) & 0xFF}.{value & 0xFF}";
    }

    private static string InferDeviceType(string ip, string? vendor)
    {
        var lastOctet = ip.Split('.').LastOrDefault();
        if (lastOctet is "1" or "254")
        {
            return "CORE_ROUTER";
        }

        if (vendor != null)
        {
            if (vendor.Contains("Cisco", StringComparison.OrdinalIgnoreCase) ||
                vendor.Contains("Arista", StringComparison.OrdinalIgnoreCase) ||
                vendor.Contains("Juniper", StringComparison.OrdinalIgnoreCase))
            {
                return "ACCESS_SWITCH";
            }

            if (vendor.Contains("VMware", StringComparison.OrdinalIgnoreCase) ||
                vendor.Contains("Hyper-V", StringComparison.OrdinalIgnoreCase) ||
                vendor.Contains("QEMU", StringComparison.OrdinalIgnoreCase))
            {
                return "SERVER";
            }
        }

        return "PC";
    }

    private static string GenerateDeterministicSyntheticMac(string ip)
    {
        var bytes = IPAddress.Parse(ip).GetAddressBytes();
        return $"02:00:{bytes[0]:X2}:{bytes[1]:X2}:{bytes[2]:X2}:{bytes[3]:X2}";
    }

    private class DiscoveryScanState
    {
        public string ScanId { get; }
        public int SiteId { get; }
        public string TargetCidr { get; }
        public int TotalIps { get; }
        public DateTimeOffset StartedAt { get; }
        public volatile string Status;
        public double ProgressPct { get; set; }
        public int ScannedIps { get; set; }
        public ConcurrentBag<DiscoveredAssetDto> DiscoveredAssets { get; } = new();
        public CancellationTokenSource Cts { get; } = new();

        public DiscoveryScanState(string scanId, int siteId, string targetCidr, int totalIps, DateTimeOffset startedAt)
        {
            ScanId = scanId;
            SiteId = siteId;
            TargetCidr = targetCidr;
            TotalIps = totalIps;
            StartedAt = startedAt;
            Status = "RUNNING";
        }
    }
}
