namespace NetworkObserver.Application.DTOs;

/// <summary>
/// Authoritative Telemetry HUD and Time-Series DTOs complying with
/// api_contracts_specification.md Section 7.
/// </summary>
public record ReachabilitySummaryDto(
    int TotalMonitored,
    int ReachableCount,
    int OutageCount,
    int SuppressedCount,
    int UnknownCount
);

public record HostHealthSummaryDto(
    int TotalAgents,
    int HighCpuCount,
    int HighRamCount,
    int StorageWarningCount
);

public record TelemetrySummaryDto(
    DateTimeOffset Timestamp,
    ReachabilitySummaryDto Reachability,
    HostHealthSummaryDto HostHealth
);

public record LiveNetworkLayerDto(
    string StatusCode,
    int? LatencyUs,
    decimal? LatencyMs,
    decimal LossPercentage,
    int? NtpOffsetUs,
    decimal? NtpOffsetMs,
    bool IsSuppressed,
    DateTimeOffset LastCheckedAt
);

public record DeviceLiveTelemetryDto(
    int DeviceId,
    string MacAddress,
    string PrimaryIp,
    string DisplayName,
    LiveNetworkLayerDto? NetworkLayer,
    LatestReachabilityDto? LatestReachability,
    LatestHealthDto? HostLayer,
    LatestHealthDto? LatestHealth
);

public record ReachabilityHistoryPointDto(
    DateTimeOffset Timestamp,
    short StatusId,
    string StatusCode,
    int? LatencyUs,
    decimal LossPercentage,
    int? NtpOffsetUs,
    bool IsSuppressed
);

public record DeviceHistoryTelemetryDto(
    int DeviceId,
    DateTimeOffset Start,
    DateTimeOffset End,
    string Interval,
    IReadOnlyList<ReachabilityHistoryPointDto> Points
);

public record PollSweepResultDto(
    int PolledCount,
    int ReachableCount,
    int UnreachableCount,
    int SuppressedCount,
    DateTimeOffset CompletedAt
);
