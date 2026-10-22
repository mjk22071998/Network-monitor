using NetworkObserver.Application.DTOs;

namespace NetworkObserver.Application.Interfaces.Services;

/// <summary>
/// Application service contract for telemetry sweeps, live metrics, and time-series history.
/// Complies with SOLID principles (SRP, DIP) and api_contracts_specification.md Section 7.
/// </summary>
public interface ITelemetryService
{
    Task<PollSweepResultDto> ExecuteReachabilitySweepAsync(CancellationToken ct = default);
    Task<HealthSweepResultDto> ExecuteHealthSweepAsync(CancellationToken ct = default);
    Task<TelemetrySummaryDto> GetSummaryAsync(CancellationToken ct = default);
    Task<DeviceLiveTelemetryDto> GetLiveTelemetryAsync(int deviceId, CancellationToken ct = default);
    Task<DeviceHistoryTelemetryDto> GetTelemetryHistoryAsync(int deviceId, DateTimeOffset start, DateTimeOffset end, string interval, CancellationToken ct = default);
}
