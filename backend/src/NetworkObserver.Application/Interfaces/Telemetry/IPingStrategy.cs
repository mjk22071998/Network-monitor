namespace NetworkObserver.Application.Interfaces.Telemetry;

/// <summary>
/// Result of an ICMP network probe.
/// Strict N/A Rule: LatencyUs is strictly null on timeout/unreachable, never 0.
/// </summary>
public record PingProbeResult(
    bool IsSuccess,
    int? LatencyUs,
    float LossPercentage,
    string? ErrorMessage = null
);

/// <summary>
/// Strategy pattern abstraction for raw socket asynchronous ICMP probes.
/// Zero subprocess spawning.
/// </summary>
public interface IPingStrategy
{
    Task<PingProbeResult> PingAsync(string ipAddress, int timeoutMs, CancellationToken ct = default);
}
