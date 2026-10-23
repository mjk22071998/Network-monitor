using NetworkObserver.Application.DTOs;

namespace NetworkObserver.Application.Interfaces.Discovery;

/// <summary>
/// Service contract for Asynchronous Subnet Discovery Scans.
/// Complies with api_contracts_specification.md Section 8.
/// </summary>
public interface ISubnetDiscoveryService
{
    Task<SubnetScanInitResponse> TriggerScanAsync(SubnetScanRequest request, int siteId, int? userId = null, string ipAddress = "127.0.0.1", CancellationToken ct = default);
    Task<SubnetScanStatusResponse> GetScanStatusAsync(string scanId, CancellationToken ct = default);
    Task<SubnetScanStatusResponse?> GetActiveOrLatestScanAsync(int siteId = 1, CancellationToken ct = default);
    Task<bool> CancelScanAsync(string scanId, int? userId = null, string ipAddress = "127.0.0.1", CancellationToken ct = default);
}
