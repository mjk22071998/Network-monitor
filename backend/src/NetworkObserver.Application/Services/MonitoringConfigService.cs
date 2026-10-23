using NetworkObserver.Application.DTOs;
using NetworkObserver.Application.Interfaces.Repositories;
using NetworkObserver.Application.Interfaces.Services;
using NetworkObserver.Domain.Constants;
using NetworkObserver.Domain.Entities;
using NetworkObserver.Domain.Exceptions;

namespace NetworkObserver.Application.Services;

/// <summary>
/// Domain application service implementing global monitoring configuration validation
/// and cadence updates per api_contracts_specification.md Section 7.
/// </summary>
public class MonitoringConfigService : IMonitoringConfigService
{
    private readonly IMonitoringConfigRepository _configRepository;
    private readonly IAuditLogRepository _auditLogRepository;

    public MonitoringConfigService(
        IMonitoringConfigRepository configRepository,
        IAuditLogRepository? auditLogRepository = null)
    {
        _configRepository = configRepository;
        _auditLogRepository = auditLogRepository ?? new NullAuditLogRepository();
    }

    public async Task<MonitoringConfigDto> GetConfigAsync(CancellationToken ct = default)
    {
        var config = await _configRepository.GetActiveConfigAsync(ct);
        if (config == null)
        {
            throw new MonitoringConfigNotFoundException();
        }

        return new MonitoringConfigDto(
            ConfigId: config.ConfigId,
            SiteId: config.SiteId,
            ReachabilityIntervalSec: config.ReachabilityIntervalSec,
            ReachabilityTimeoutMs: config.ReachabilityTimeoutMs,
            HealthIntervalSec: config.HealthIntervalSec,
            HealthTimeoutMs: config.HealthTimeoutMs,
            CascadeSuppressionEnabled: config.CascadeSuppressionEnabled,
            UpdatedAt: config.UpdatedAt
        );
    }

    public async Task<MonitoringConfigDto> UpdateConfigAsync(UpdateMonitoringConfigRequest request, int? userId = null, string ipAddress = "127.0.0.1", CancellationToken ct = default)
    {
        ValidateCadenceParameters(request);

        var config = await _configRepository.GetActiveConfigAsync(ct);
        if (config == null)
        {
            throw new MonitoringConfigNotFoundException();
        }

        config.ReachabilityIntervalSec = request.ReachabilityIntervalSec;
        config.ReachabilityTimeoutMs = request.ReachabilityTimeoutMs;
        config.HealthIntervalSec = request.HealthIntervalSec;
        config.HealthTimeoutMs = request.HealthTimeoutMs;
        config.CascadeSuppressionEnabled = request.CascadeSuppressionEnabled;
        config.UpdatedAt = DateTimeOffset.UtcNow;

        await _configRepository.UpdateAsync(config, ct);

        await _auditLogRepository.AddAsync(new AuditLog
        {
            UserId = userId,
            SiteId = config.SiteId,
            ActionCode = AuditActionCodes.ConfigUpdate,
            TargetEntity = AuditTargetEntities.Site,
            TargetId = config.ConfigId.ToString(),
            Details = $"Updated monitoring cadences: Reachability={config.ReachabilityIntervalSec}s (timeout {config.ReachabilityTimeoutMs}ms), Health={config.HealthIntervalSec}s (timeout {config.HealthTimeoutMs}ms), CascadeSuppression={config.CascadeSuppressionEnabled}.",
            IpAddress = string.IsNullOrWhiteSpace(ipAddress) ? "127.0.0.1" : ipAddress.Trim(),
            LoggedAt = DateTimeOffset.UtcNow
        }, ct);

        return new MonitoringConfigDto(
            ConfigId: config.ConfigId,
            SiteId: config.SiteId,
            ReachabilityIntervalSec: config.ReachabilityIntervalSec,
            ReachabilityTimeoutMs: config.ReachabilityTimeoutMs,
            HealthIntervalSec: config.HealthIntervalSec,
            HealthTimeoutMs: config.HealthTimeoutMs,
            CascadeSuppressionEnabled: config.CascadeSuppressionEnabled,
            UpdatedAt: config.UpdatedAt
        );
    }

    private static void ValidateCadenceParameters(UpdateMonitoringConfigRequest r)
    {
        // 1. Reachability Interval (1s - 60s)
        if (r.ReachabilityIntervalSec < 1 || r.ReachabilityIntervalSec > 60)
        {
            throw new MonitoringConfigValidationException(
                $"reachability_interval_sec must be between 1 and 60 seconds. Received: {r.ReachabilityIntervalSec}.");
        }

        // 2. Reachability Timeout (100ms - 10,000ms) and <= interval
        if (r.ReachabilityTimeoutMs < 100 || r.ReachabilityTimeoutMs > 10000)
        {
            throw new MonitoringConfigValidationException(
                $"reachability_timeout_ms must be between 100ms and 10,000ms. Received: {r.ReachabilityTimeoutMs}.");
        }

        if (r.ReachabilityTimeoutMs > r.ReachabilityIntervalSec * 1000)
        {
            throw new MonitoringConfigValidationException(
                $"reachability_timeout_ms ({r.ReachabilityTimeoutMs}ms) cannot exceed reachability_interval_sec ({r.ReachabilityIntervalSec * 1000}ms).");
        }

        // 3. Health Interval (10s - 300s)
        if (r.HealthIntervalSec < 10 || r.HealthIntervalSec > 300)
        {
            throw new MonitoringConfigValidationException(
                $"health_interval_sec must be between 10 and 300 seconds. Received: {r.HealthIntervalSec}.");
        }

        // 4. Health Timeout (500ms - 30,000ms) and <= interval
        if (r.HealthTimeoutMs < 500 || r.HealthTimeoutMs > 30000)
        {
            throw new MonitoringConfigValidationException(
                $"health_timeout_ms must be between 500ms and 30,000ms. Received: {r.HealthTimeoutMs}.");
        }

        if (r.HealthTimeoutMs > r.HealthIntervalSec * 1000)
        {
            throw new MonitoringConfigValidationException(
                $"health_timeout_ms ({r.HealthTimeoutMs}ms) cannot exceed health_interval_sec ({r.HealthIntervalSec * 1000}ms).");
        }
    }
}
