using NetworkObserver.Application.DTOs;
using NetworkObserver.Application.Interfaces.Repositories;
using NetworkObserver.Application.Services;
using NetworkObserver.Domain.Entities;
using NetworkObserver.Domain.Exceptions;
using Xunit;

namespace NetworkObserver.Tests.Services;

public class MonitoringConfigServiceTests
{
    private readonly StubMonitoringConfigRepository _configRepo;
    private readonly MonitoringConfigService _configService;

    public MonitoringConfigServiceTests()
    {
        _configRepo = new StubMonitoringConfigRepository();
        _configService = new MonitoringConfigService(_configRepo);
    }

    [Fact]
    public async Task GetConfigAsync_WhenExists_ReturnsExpectedDto()
    {
        // Arrange
        var config = new MonitoringConfig
        {
            ConfigId = 1,
            SiteId = 1,
            ReachabilityIntervalSec = 5,
            ReachabilityTimeoutMs = 2000,
            HealthIntervalSec = 30,
            HealthTimeoutMs = 5000,
            CascadeSuppressionEnabled = true,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _configRepo.SetConfig(config);

        // Act
        var result = await _configService.GetConfigAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.ConfigId);
        Assert.Equal(5, result.ReachabilityIntervalSec);
        Assert.Equal(2000, result.ReachabilityTimeoutMs);
        Assert.Equal(30, result.HealthIntervalSec);
        Assert.Equal(5000, result.HealthTimeoutMs);
        Assert.True(result.CascadeSuppressionEnabled);
    }

    [Fact]
    public async Task GetConfigAsync_WhenNotFound_ThrowsMonitoringConfigNotFoundException()
    {
        // Arrange
        _configRepo.SetConfig(null);

        // Act & Assert
        await Assert.ThrowsAsync<MonitoringConfigNotFoundException>(() => _configService.GetConfigAsync());
    }

    [Fact]
    public async Task UpdateConfigAsync_WhenValidInput_UpdatesAndReturnsExpectedDto()
    {
        // Arrange
        var initial = new MonitoringConfig
        {
            ConfigId = 1,
            SiteId = 1,
            ReachabilityIntervalSec = 5,
            ReachabilityTimeoutMs = 2000,
            HealthIntervalSec = 30,
            HealthTimeoutMs = 5000,
            CascadeSuppressionEnabled = true
        };
        _configRepo.SetConfig(initial);

        var request = new UpdateMonitoringConfigRequest(
            ReachabilityIntervalSec: 10,
            ReachabilityTimeoutMs: 3000,
            HealthIntervalSec: 60,
            HealthTimeoutMs: 10000,
            CascadeSuppressionEnabled: false
        );

        // Act
        var result = await _configService.UpdateConfigAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(10, result.ReachabilityIntervalSec);
        Assert.Equal(3000, result.ReachabilityTimeoutMs);
        Assert.Equal(60, result.HealthIntervalSec);
        Assert.Equal(10000, result.HealthTimeoutMs);
        Assert.False(result.CascadeSuppressionEnabled);

        var stored = await _configRepo.GetActiveConfigAsync();
        Assert.NotNull(stored);
        Assert.Equal(10, stored.ReachabilityIntervalSec);
        Assert.False(stored.CascadeSuppressionEnabled);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(61)]
    [InlineData(100)]
    public async Task UpdateConfigAsync_WhenReachabilityIntervalOutOfRange_ThrowsMonitoringConfigValidationException(int invalidSec)
    {
        // Arrange
        var request = new UpdateMonitoringConfigRequest(
            ReachabilityIntervalSec: invalidSec,
            ReachabilityTimeoutMs: 1000,
            HealthIntervalSec: 30,
            HealthTimeoutMs: 5000,
            CascadeSuppressionEnabled: true
        );

        // Act & Assert
        var ex = await Assert.ThrowsAsync<MonitoringConfigValidationException>(() =>
            _configService.UpdateConfigAsync(request));
        Assert.Equal("INVALID_CONFIG_PARAMETERS", ex.ErrorCode);
    }

    [Fact]
    public async Task UpdateConfigAsync_WhenReachabilityTimeoutExceedsInterval_ThrowsMonitoringConfigValidationException()
    {
        // Arrange: 5s interval = 5000ms max timeout. Given: 6000ms
        var request = new UpdateMonitoringConfigRequest(
            ReachabilityIntervalSec: 5,
            ReachabilityTimeoutMs: 6000,
            HealthIntervalSec: 30,
            HealthTimeoutMs: 5000,
            CascadeSuppressionEnabled: true
        );

        // Act & Assert
        await Assert.ThrowsAsync<MonitoringConfigValidationException>(() =>
            _configService.UpdateConfigAsync(request));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(9)]
    [InlineData(301)]
    [InlineData(500)]
    public async Task UpdateConfigAsync_WhenHealthIntervalOutOfRange_ThrowsMonitoringConfigValidationException(int invalidSec)
    {
        // Arrange
        var request = new UpdateMonitoringConfigRequest(
            ReachabilityIntervalSec: 5,
            ReachabilityTimeoutMs: 2000,
            HealthIntervalSec: invalidSec,
            HealthTimeoutMs: 5000,
            CascadeSuppressionEnabled: true
        );

        // Act & Assert
        await Assert.ThrowsAsync<MonitoringConfigValidationException>(() =>
            _configService.UpdateConfigAsync(request));
    }

    [Fact]
    public async Task UpdateConfigAsync_WhenHealthTimeoutExceedsInterval_ThrowsMonitoringConfigValidationException()
    {
        // Arrange: 30s interval = 30000ms max timeout. Given: 35000ms
        var request = new UpdateMonitoringConfigRequest(
            ReachabilityIntervalSec: 5,
            ReachabilityTimeoutMs: 2000,
            HealthIntervalSec: 30,
            HealthTimeoutMs: 35000,
            CascadeSuppressionEnabled: true
        );

        // Act & Assert
        await Assert.ThrowsAsync<MonitoringConfigValidationException>(() =>
            _configService.UpdateConfigAsync(request));
    }

    [Fact]
    public async Task UpdateConfigAsync_WhenConfigNotFound_ThrowsMonitoringConfigNotFoundException()
    {
        // Arrange
        _configRepo.SetConfig(null);

        var request = new UpdateMonitoringConfigRequest(
            ReachabilityIntervalSec: 5,
            ReachabilityTimeoutMs: 2000,
            HealthIntervalSec: 30,
            HealthTimeoutMs: 5000,
            CascadeSuppressionEnabled: true
        );

        // Act & Assert
        await Assert.ThrowsAsync<MonitoringConfigNotFoundException>(() =>
            _configService.UpdateConfigAsync(request));
    }
}

/// <summary>
/// Fast in-memory stub of IMonitoringConfigRepository for unit testing.
/// </summary>
internal class StubMonitoringConfigRepository : IMonitoringConfigRepository
{
    private MonitoringConfig? _config;

    public void SetConfig(MonitoringConfig? config) => _config = config;

    public Task<MonitoringConfig?> GetActiveConfigAsync(CancellationToken ct = default)
    {
        return Task.FromResult(_config);
    }

    public Task UpdateAsync(MonitoringConfig config, CancellationToken ct = default)
    {
        _config = config;
        return Task.CompletedTask;
    }
}
