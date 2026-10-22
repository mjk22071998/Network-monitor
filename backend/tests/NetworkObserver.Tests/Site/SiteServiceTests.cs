using NetworkObserver.Application.DTOs;
using NetworkObserver.Application.Interfaces.Repositories;
using NetworkObserver.Application.Services;
using NetworkObserver.Domain.Entities;
using NetworkObserver.Domain.Exceptions;
using Xunit;

namespace NetworkObserver.Tests.Services;

public class SiteServiceTests
{
    private readonly StubSiteRepository _siteRepo;
    private readonly SiteService _siteService;

    public SiteServiceTests()
    {
        _siteRepo = new StubSiteRepository();
        _siteService = new SiteService(_siteRepo);
    }

    [Fact]
    public async Task GetActiveSiteAsync_WhenSiteExists_ReturnsExpectedDto()
    {
        // Arrange
        var site = new Site
        {
            SiteId = 1,
            SiteName = "Primary Site Enclave",
            FacilityCode = "SITE-01",
            Description = "Initial Air-Gapped NOC",
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-10),
            UpdatedAt = DateTimeOffset.UtcNow.AddDays(-1)
        };
        _siteRepo.SetSite(site);

        // Act
        var result = await _siteService.GetActiveSiteAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.SiteId);
        Assert.Equal("Primary Site Enclave", result.SiteName);
        Assert.Equal("SITE-01", result.FacilityCode);
        Assert.Equal("Initial Air-Gapped NOC", result.Description);
    }

    [Fact]
    public async Task GetActiveSiteAsync_WhenSiteNotFound_ThrowsSiteNotFoundException()
    {
        // Arrange (no site configured)
        _siteRepo.SetSite(null);

        // Act & Assert
        await Assert.ThrowsAsync<SiteNotFoundException>(() => _siteService.GetActiveSiteAsync());
    }

    [Fact]
    public async Task UpdateActiveSiteAsync_WhenValidInput_UpdatesSiteAndReturnsResponse()
    {
        // Arrange
        var existingSite = new Site
        {
            SiteId = 1,
            SiteName = "Old Enclave Name",
            FacilityCode = "OLD-01",
            Description = "Old desc",
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-5),
            UpdatedAt = DateTimeOffset.UtcNow.AddDays(-5)
        };
        _siteRepo.SetSite(existingSite);

        var request = new UpdateSiteRequest(
            SiteName: "University Central Campus",
            FacilityCode: "CAMPUS-MAIN-01",
            Description: "Main Academic Computing & Computer Labs Enclave"
        );

        // Act
        var result = await _siteService.UpdateActiveSiteAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.SiteId);
        Assert.Equal("University Central Campus", result.SiteName);
        Assert.Equal("CAMPUS-MAIN-01", result.FacilityCode);
        Assert.Equal("Main Academic Computing & Computer Labs Enclave", result.Description);

        var stored = await _siteRepo.GetActiveSiteAsync();
        Assert.NotNull(stored);
        Assert.Equal("University Central Campus", stored.SiteName);
        Assert.Equal("CAMPUS-MAIN-01", stored.FacilityCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task UpdateActiveSiteAsync_WhenSiteNameMissing_ThrowsSiteValidationException(string? invalidName)
    {
        // Arrange
        var request = new UpdateSiteRequest(
            SiteName: invalidName!,
            FacilityCode: "FAC-01",
            Description: "Valid desc"
        );

        // Act & Assert
        var ex = await Assert.ThrowsAsync<SiteValidationException>(() => _siteService.UpdateActiveSiteAsync(request));
        Assert.Equal("SITE_VALIDATION_ERROR", ex.ErrorCode);
    }

    [Fact]
    public async Task UpdateActiveSiteAsync_WhenSiteNameTooLong_ThrowsSiteValidationException()
    {
        // Arrange
        var request = new UpdateSiteRequest(
            SiteName: new string('A', 101),
            FacilityCode: "FAC-01",
            Description: "Valid desc"
        );

        // Act & Assert
        await Assert.ThrowsAsync<SiteValidationException>(() => _siteService.UpdateActiveSiteAsync(request));
    }

    [Fact]
    public async Task UpdateActiveSiteAsync_WhenFacilityCodeTooLong_ThrowsSiteValidationException()
    {
        // Arrange
        var request = new UpdateSiteRequest(
            SiteName: "Valid Site Name",
            FacilityCode: new string('F', 51),
            Description: "Valid desc"
        );

        // Act & Assert
        await Assert.ThrowsAsync<SiteValidationException>(() => _siteService.UpdateActiveSiteAsync(request));
    }

    [Fact]
    public async Task UpdateActiveSiteAsync_WhenDescriptionTooLong_ThrowsSiteValidationException()
    {
        // Arrange
        var request = new UpdateSiteRequest(
            SiteName: "Valid Site Name",
            FacilityCode: "FAC-01",
            Description: new string('D', 501)
        );

        // Act & Assert
        await Assert.ThrowsAsync<SiteValidationException>(() => _siteService.UpdateActiveSiteAsync(request));
    }

    [Fact]
    public async Task UpdateActiveSiteAsync_WhenSiteNotFound_ThrowsSiteNotFoundException()
    {
        // Arrange
        _siteRepo.SetSite(null);

        var request = new UpdateSiteRequest(
            SiteName: "Valid Site Name",
            FacilityCode: "FAC-01",
            Description: "Valid desc"
        );

        // Act & Assert
        await Assert.ThrowsAsync<SiteNotFoundException>(() => _siteService.UpdateActiveSiteAsync(request));
    }
}

/// <summary>
/// Fast in-memory stub of ISiteRepository.
/// </summary>
internal class StubSiteRepository : ISiteRepository
{
    private Site? _site;

    public void SetSite(Site? site) => _site = site;

    public Task<Site?> GetByIdAsync(int siteId, CancellationToken ct = default)
    {
        return Task.FromResult(_site?.SiteId == siteId ? _site : null);
    }

    public Task<Site?> GetActiveSiteAsync(CancellationToken ct = default)
    {
        return Task.FromResult(_site);
    }

    public Task<bool> ExistsAsync(int siteId, CancellationToken ct = default)
    {
        return Task.FromResult(_site != null && _site.SiteId == siteId);
    }

    public Task UpdateAsync(Site site, CancellationToken ct = default)
    {
        _site = site;
        return Task.CompletedTask;
    }
}
