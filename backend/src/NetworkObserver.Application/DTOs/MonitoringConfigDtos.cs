namespace NetworkObserver.Application.DTOs;

/// <summary>
/// Authoritative Monitoring Engine Configuration DTOs complying with
/// api_contracts_specification.md Section 7.5 and 7.6.
/// </summary>
public record MonitoringConfigDto(
    int ConfigId,
    int SiteId,
    int ReachabilityIntervalSec,
    int ReachabilityTimeoutMs,
    int HealthIntervalSec,
    int HealthTimeoutMs,
    bool CascadeSuppressionEnabled,
    DateTimeOffset UpdatedAt
);

public record UpdateMonitoringConfigRequest(
    int ReachabilityIntervalSec,
    int ReachabilityTimeoutMs,
    int HealthIntervalSec,
    int HealthTimeoutMs,
    bool CascadeSuppressionEnabled
);
