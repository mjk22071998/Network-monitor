namespace NetworkObserver.Application.DTOs;

/// <summary>
/// DTOs for Asynchronous Subnet Discovery Scans complying with
/// api_contracts_specification.md Section 8.
/// </summary>
public record SubnetScanRequest(
    string? SubnetCidr = null,
    string? Cidr = null,
    string? SnmpCommunity = "public",
    string? SnmpVersion = "v2c",
    int? TimeoutMs = null
)
{
    public string EffectiveCidr => !string.IsNullOrWhiteSpace(SubnetCidr) ? SubnetCidr : Cidr ?? string.Empty;
}

public record SubnetScanInitResponse(
    string ScanId,
    string Status,
    string TargetCidr,
    int TotalIps,
    DateTimeOffset StartedAt
);

public record DiscoveredAssetDto(
    string PrimaryIp,
    string MacAddress,
    string? Vendor,
    string InferredType,
    bool IsAlreadyRegistered,
    double? ResponseTimeMs = null
);

public record SubnetScanStatusResponse(
    string ScanId,
    string Status,
    double ProgressPct,
    IReadOnlyList<DiscoveredAssetDto> DiscoveredAssets,
    string? TargetCidr = null,
    int TotalIps = 0,
    int ScannedIps = 0
);
