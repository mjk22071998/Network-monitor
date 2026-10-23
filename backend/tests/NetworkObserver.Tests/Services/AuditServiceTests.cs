using NetworkObserver.Application.DTOs;
using NetworkObserver.Application.Interfaces.Repositories;
using NetworkObserver.Application.Services;
using NetworkObserver.Domain.Constants;
using NetworkObserver.Domain.Entities;
using NetworkObserver.Domain.Enums;
using Xunit;

namespace NetworkObserver.Tests.Services;

public class AuditServiceTests
{
    private readonly StubAuditLogRepository _auditRepo;
    private readonly AuditService _auditService;

    public AuditServiceTests()
    {
        _auditRepo = new StubAuditLogRepository();
        _auditService = new AuditService(_auditRepo);
    }

    [Fact]
    public async Task LogAsync_RecordsAction_WithUtcTimestampAndTargetDetails()
    {
        // Act
        var result = await _auditService.LogAsync(
            userId: 1,
            actionCode: AuditActionCodes.DeviceCreate,
            targetEntity: AuditTargetEntities.Device,
            targetId: "42",
            details: "Created device 'CS-LAB101-PC-42'.",
            ipAddress: "192.168.1.50",
            siteId: 1
        );

        // Assert
        Assert.NotNull(result);
        Assert.True(result.LogId > 0);
        Assert.Equal(1, result.UserId);
        Assert.Equal(AuditActionCodes.DeviceCreate, result.ActionCode);
        Assert.Equal(AuditTargetEntities.Device, result.TargetEntity);
        Assert.Equal("42", result.TargetId);
        Assert.Equal("Created device 'CS-LAB101-PC-42'.", result.Details);
        Assert.Equal("192.168.1.50", result.IpAddress);
        Assert.True(result.LoggedAt <= DateTimeOffset.UtcNow);

        Assert.Single(_auditRepo.Logs);
    }

    [Fact]
    public async Task GetAuditLogsPagedAsync_ReturnsPagedLogs_WithPaginationMetadata()
    {
        // Arrange - seed 15 audit logs
        var baseTime = DateTimeOffset.UtcNow.AddMinutes(-30);
        for (int i = 1; i <= 15; i++)
        {
            await _auditRepo.AddAsync(new AuditLog
            {
                LogId = i,
                UserId = i % 2 == 0 ? 1 : 2,
                ActionCode = i % 2 == 0 ? AuditActionCodes.DeviceCreate : AuditActionCodes.DeviceUpdate,
                TargetEntity = AuditTargetEntities.Device,
                TargetId = i.ToString(),
                Details = $"Test log #{i}",
                IpAddress = "127.0.0.1",
                LoggedAt = baseTime.AddMinutes(i)
            });
        }

        // Act - request page 1 with limit 5
        var response = await _auditService.GetAuditLogsPagedAsync(new AuditLogQueryParameters(Page: 1, Limit: 5));

        // Assert
        Assert.NotNull(response);
        Assert.Equal(5, response.Data.Count);
        Assert.Equal(15, response.Pagination.TotalRecords);
        Assert.Equal(1, response.Pagination.CurrentPage);
        Assert.Equal(5, response.Pagination.PageSize);
        Assert.Equal(3, response.Pagination.TotalPages);
        Assert.True(response.Pagination.HasNext);
        Assert.False(response.Pagination.HasPrev);
    }

    [Fact]
    public async Task GetAuditLogsPagedAsync_FiltersByUserId()
    {
        // Arrange
        await _auditRepo.AddAsync(new AuditLog { UserId = 1, ActionCode = AuditActionCodes.AuthLogin, TargetEntity = AuditTargetEntities.User });
        await _auditRepo.AddAsync(new AuditLog { UserId = 2, ActionCode = AuditActionCodes.DeviceCreate, TargetEntity = AuditTargetEntities.Device });
        await _auditRepo.AddAsync(new AuditLog { UserId = 1, ActionCode = AuditActionCodes.DeviceUpdate, TargetEntity = AuditTargetEntities.Device });

        // Act
        var response = await _auditService.GetAuditLogsPagedAsync(new AuditLogQueryParameters(UserId: 2));

        // Assert
        Assert.Single(response.Data);
        Assert.Equal(2, response.Data[0].UserId);
        Assert.Equal(AuditActionCodes.DeviceCreate, response.Data[0].ActionCode);
    }

    [Fact]
    public async Task GetAuditLogsPagedAsync_FiltersByActionCode()
    {
        // Arrange
        await _auditRepo.AddAsync(new AuditLog { UserId = 1, ActionCode = AuditActionCodes.AuthLogin, TargetEntity = AuditTargetEntities.User });
        await _auditRepo.AddAsync(new AuditLog { UserId = 1, ActionCode = AuditActionCodes.BatchCommit, TargetEntity = AuditTargetEntities.Batch });
        await _auditRepo.AddAsync(new AuditLog { UserId = 2, ActionCode = AuditActionCodes.BatchCommit, TargetEntity = AuditTargetEntities.Batch });

        // Act
        var response = await _auditService.GetAuditLogsPagedAsync(new AuditLogQueryParameters(ActionCode: AuditActionCodes.BatchCommit));

        // Assert
        Assert.Equal(2, response.Data.Count);
        Assert.All(response.Data, log => Assert.Equal(AuditActionCodes.BatchCommit, log.ActionCode));
    }

    [Fact]
    public async Task GetAuditLogsPagedAsync_FiltersByDateRange()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        await _auditRepo.AddAsync(new AuditLog { LoggedAt = now.AddHours(-10), ActionCode = "OLD" });
        await _auditRepo.AddAsync(new AuditLog { LoggedAt = now.AddHours(-2), ActionCode = "MID" });
        await _auditRepo.AddAsync(new AuditLog { LoggedAt = now.AddMinutes(-5), ActionCode = "NEW" });

        // Act - filter between -3 hours and -1 hour
        var response = await _auditService.GetAuditLogsPagedAsync(new AuditLogQueryParameters(
            Start: now.AddHours(-3),
            End: now.AddHours(-1)
        ));

        // Assert
        Assert.Single(response.Data);
        Assert.Equal("MID", response.Data[0].ActionCode);
    }

    [Fact]
    public async Task GetAuditLogsPagedAsync_ClampsLimitTo100()
    {
        // Act - request page with excessive limit (500)
        var response = await _auditService.GetAuditLogsPagedAsync(new AuditLogQueryParameters(Page: 1, Limit: 500));

        // Assert
        Assert.Equal(100, response.Pagination.PageSize);
    }

    [Fact]
    public async Task DeviceService_CreateDevice_EmitsDeviceCreateAuditLog()
    {
        // Arrange
        var deviceRepo = new StubDeviceRepository();
        var groupRepo = new StubGroupRepository();
        groupRepo.AddGroup(new Group { GroupId = 1, GroupName = "Unassigned Group" });
        var deviceService = new DeviceService(deviceRepo, groupRepo, auditLogRepository: _auditRepo);

        // Act
        await deviceService.CreateDeviceAsync(
            new CreateDeviceRequest("CS-LAB101-PC-99", "192.168.1.99", "00:11:22:33:44:99", 5, 1),
            RoleType.NetworkAdmin,
            userId: 1,
            ipAddress: "192.168.1.200"
        );

        // Assert
        var log = Assert.Single(_auditRepo.Logs);
        Assert.Equal(AuditActionCodes.DeviceCreate, log.ActionCode);
        Assert.Equal(AuditTargetEntities.Device, log.TargetEntity);
        Assert.Equal(1, log.UserId);
        Assert.Equal("192.168.1.200", log.IpAddress);
    }

    [Fact]
    public async Task GroupService_CreateGroup_EmitsGroupCreateAuditLog()
    {
        // Arrange
        var groupRepo = new StubGroupRepository();
        groupRepo.AddGroup(new Group { GroupId = 1, GroupName = "Unassigned Group" });
        var groupService = new GroupService(groupRepo, _auditRepo);

        // Act
        await groupService.CreateGroupAsync(
            new CreateGroupRequest("CS Lab 105", null, "New lab room"),
            userId: 2,
            ipAddress: "10.0.0.5"
        );

        // Assert
        var log = Assert.Single(_auditRepo.Logs);
        Assert.Equal(AuditActionCodes.GroupCreate, log.ActionCode);
        Assert.Equal(AuditTargetEntities.Group, log.TargetEntity);
        Assert.Equal(2, log.UserId);
    }

    [Fact]
    public async Task SiteService_UpdateSite_EmitsSiteUpdateAuditLog()
    {
        // Arrange
        var siteRepo = new StubSiteRepository();
        siteRepo.SetSite(new Site
        {
            SiteId = 1,
            SiteName = "Initial Site",
            FacilityCode = "SITE-01",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        var siteService = new SiteService(siteRepo, _auditRepo);

        // Act
        await siteService.UpdateActiveSiteAsync(
            new UpdateSiteRequest("Engineering Campus Lab", "SITE-ENG", "Updated Description"),
            userId: 1,
            ipAddress: "127.0.0.1"
        );

        // Assert
        var log = Assert.Single(_auditRepo.Logs);
        Assert.Equal(AuditActionCodes.SiteUpdate, log.ActionCode);
        Assert.Equal(AuditTargetEntities.Site, log.TargetEntity);
        Assert.Equal(1, log.UserId);
    }

    [Fact]
    public async Task MonitoringConfigService_UpdateConfig_EmitsConfigUpdateAuditLog()
    {
        // Arrange
        var configRepo = new StubMonitoringConfigRepository();
        configRepo.SetConfig(new MonitoringConfig
        {
            ConfigId = 1,
            SiteId = 1,
            ReachabilityIntervalSec = 5,
            ReachabilityTimeoutMs = 2000,
            HealthIntervalSec = 30,
            HealthTimeoutMs = 5000,
            CascadeSuppressionEnabled = true,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        var configService = new MonitoringConfigService(configRepo, _auditRepo);

        // Act
        await configService.UpdateConfigAsync(
            new UpdateMonitoringConfigRequest(10, 2000, 60, 5000, true),
            userId: 1,
            ipAddress: "127.0.0.1"
        );

        // Assert
        var log = Assert.Single(_auditRepo.Logs);
        Assert.Equal(AuditActionCodes.ConfigUpdate, log.ActionCode);
        Assert.Equal(AuditTargetEntities.Site, log.TargetEntity);
        Assert.Equal(1, log.UserId);
    }
}
