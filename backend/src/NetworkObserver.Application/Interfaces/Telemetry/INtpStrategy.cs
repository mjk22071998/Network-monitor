namespace NetworkObserver.Application.Interfaces.Telemetry;

/// <summary>
/// Result of an asynchronous UDP NTP clock drift probe.
/// Strict N/A Rule: OffsetUs is strictly null on timeout or when no NTP daemon is responding (never 0).
/// </summary>
public record NtpProbeResult(
    bool IsSuccess,
    int? OffsetUs,
    string? ErrorMessage = null
);

/// <summary>
/// Strategy pattern abstraction for asynchronous 48-byte UDP NTP client queries (port 123).
/// Zero subprocesses, non-blocking async network I/O.
/// </summary>
public interface INtpStrategy
{
    Task<NtpProbeResult> QueryDriftAsync(string ipAddress, int timeoutMs = 1000, CancellationToken ct = default);
}
