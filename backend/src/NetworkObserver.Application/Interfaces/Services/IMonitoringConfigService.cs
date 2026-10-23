using NetworkObserver.Application.DTOs;

namespace NetworkObserver.Application.Interfaces.Services;

/// <summary>
/// Application service contract for global monitoring configuration management.
/// Complies with SOLID principles (SRP, DIP) and api_contracts_specification.md Section 7.
/// </summary>
public interface IMonitoringConfigService
{
    Task<MonitoringConfigDto> GetConfigAsync(CancellationToken ct = default);
    Task<MonitoringConfigDto> UpdateConfigAsync(UpdateMonitoringConfigRequest request, int? userId = null, string ipAddress = "127.0.0.1", CancellationToken ct = default);
}
