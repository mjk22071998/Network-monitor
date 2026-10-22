namespace NetworkObserver.Application.Interfaces.Telemetry;

public record DiskVolumeProbeResult(
    string VolumeName,
    float UsedPercentage,
    float TotalCapacityGb,
    float FreeSpaceGb
);

public record HostHealthProbeResult(
    bool IsSuccess,
    short StatusId, // 1 = AVAILABLE, 2 = UNAVAILABLE, 4 = UNSUPPORTED, 5 = ERROR
    float? CpuUtilizationPct,
    float? RamUtilizationPct,
    float? NetRxKbps,
    float? NetTxKbps,
    float? TemperatureCelsius,
    IReadOnlyList<DiskVolumeProbeResult> DiskVolumes,
    string? ErrorMessage = null
);

/// <summary>
/// Strategy pattern abstraction for collecting host operating health metrics
/// (CPU, RAM, network throughput, multi-volume disk storage) via port 9182 / SNMP.
/// Strict N/A Rule: Metrics are strictly null when agent is unavailable or unsupported (never 0).
/// </summary>
public interface IHostHealthStrategy
{
    Task<HostHealthProbeResult> QueryHealthAsync(string ipAddress, short deviceTypeId, int timeoutMs = 3000, CancellationToken ct = default);
}
