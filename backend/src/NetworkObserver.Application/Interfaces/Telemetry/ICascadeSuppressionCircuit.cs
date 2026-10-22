using NetworkObserver.Application.Interfaces.Telemetry;
using NetworkObserver.Domain.Entities;

namespace NetworkObserver.Application.Interfaces.Telemetry;

/// <summary>
/// Domain interface for the Cascading Failure Suppression Circuit.
/// Evaluates in-memory switch-device DAG dependencies and halts synthetic
/// ICMP & NTP packet emissions to downstream devices during upstream switch outages.
/// Complies with FR-CAS-01 through FR-CAS-04.
/// </summary>
public interface ICascadeSuppressionCircuit
{
    Task<IReadOnlyList<ReachabilitySample>> ExecuteSweepAsync(
        IReadOnlyList<Device> monitoredDevices,
        int timeoutMs,
        bool enableSuppression,
        IPingStrategy pingStrategy,
        INtpStrategy ntpStrategy,
        DateTimeOffset timestamp,
        CancellationToken ct = default);
}
