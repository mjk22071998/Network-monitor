using NetworkObserver.Application.Interfaces.Repositories;
using NetworkObserver.Application.Interfaces.Services;

namespace NetworkObserver.Api.HostedServices;

/// <summary>
/// Continuous background worker coordinating independent reachability (5s) and host health (30s)
/// micro-batch sweeps. Respects dynamic monitoring cadences, non-reentrant locks, and graceful shutdown.
/// Complies with FR-RCH-01, FR-HST-01, and 500MB RAM budget.
/// </summary>
public class TelemetryPollingBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TelemetryPollingBackgroundService> _logger;
    private readonly SemaphoreSlim _reachabilityGate = new(1, 1);
    private readonly SemaphoreSlim _healthGate = new(1, 1);

    public TelemetryPollingBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<TelemetryPollingBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Network Observer Telemetry Polling Background Service started.");

        var reachabilityTask = RunReachabilityLoopAsync(stoppingToken);
        var healthTask = RunHealthLoopAsync(stoppingToken);

        await Task.WhenAll(reachabilityTask, healthTask);
    }

    private async Task RunReachabilityLoopAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            int intervalSec = 5;
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var configRepo = scope.ServiceProvider.GetRequiredService<IMonitoringConfigRepository>();
                    var config = await configRepo.GetActiveConfigAsync(stoppingToken);
                    if (config != null && config.ReachabilityIntervalSec >= 1)
                    {
                        intervalSec = config.ReachabilityIntervalSec;
                    }
                }

                if (await _reachabilityGate.WaitAsync(0, stoppingToken))
                {
                    try
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var telemetryService = scope.ServiceProvider.GetRequiredService<ITelemetryService>();
                        var result = await telemetryService.ExecuteReachabilitySweepAsync(stoppingToken);
                        _logger.LogDebug(
                            "Reachability sweep completed: {Polled} polled, {Reachable} reachable, {Unreachable} unreachable, {Suppressed} suppressed.",
                            result.PolledCount, result.ReachableCount, result.UnreachableCount, result.SuppressedCount);
                    }
                    finally
                    {
                        _reachabilityGate.Release();
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error occurred during background reachability sweep.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(intervalSec), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RunHealthLoopAsync(CancellationToken stoppingToken)
    {
        // Initial offset to stagger start
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            int intervalSec = 30;
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var configRepo = scope.ServiceProvider.GetRequiredService<IMonitoringConfigRepository>();
                    var config = await configRepo.GetActiveConfigAsync(stoppingToken);
                    if (config != null && config.HealthIntervalSec >= 10)
                    {
                        intervalSec = config.HealthIntervalSec;
                    }
                }

                if (await _healthGate.WaitAsync(0, stoppingToken))
                {
                    try
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var telemetryService = scope.ServiceProvider.GetRequiredService<ITelemetryService>();
                        var result = await telemetryService.ExecuteHealthSweepAsync(stoppingToken);
                        _logger.LogDebug(
                            "Health sweep completed: {Polled} polled, {Available} available, {Unavailable} unavailable, {Unsupported} unsupported.",
                            result.PolledCount, result.AvailableCount, result.UnavailableCount, result.UnsupportedCount);
                    }
                    finally
                    {
                        _healthGate.Release();
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error occurred during background host health sweep.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(intervalSec), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
