using NetworkObserver.Application.DTOs;
using NetworkObserver.Application.Interfaces.Repositories;
using NetworkObserver.Application.Interfaces.Services;
using NetworkObserver.Domain.Exceptions;

namespace NetworkObserver.Application.Services;

/// <summary>
/// Domain application service for single-site enclave metadata.
/// Enforces business validation rules and encapsulates site persistence.
/// </summary>
public class SiteService : ISiteService
{
    private readonly ISiteRepository _siteRepository;

    public SiteService(ISiteRepository siteRepository)
    {
        _siteRepository = siteRepository;
    }

    public async Task<SiteDto> GetActiveSiteAsync(CancellationToken ct = default)
    {
        var site = await _siteRepository.GetActiveSiteAsync(ct);
        if (site == null)
        {
            throw new SiteNotFoundException();
        }

        return new SiteDto(
            SiteId: site.SiteId,
            SiteName: site.SiteName,
            FacilityCode: site.FacilityCode,
            Description: site.Description,
            CreatedAt: site.CreatedAt,
            UpdatedAt: site.UpdatedAt
        );
    }

    public async Task<UpdateSiteResponse> UpdateActiveSiteAsync(UpdateSiteRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.SiteName))
        {
            throw new SiteValidationException("Site name is required and cannot be empty.");
        }

        var trimmedName = request.SiteName.Trim();
        if (trimmedName.Length > 100)
        {
            throw new SiteValidationException("Site name cannot exceed 100 characters.");
        }

        var trimmedCode = request.FacilityCode?.Trim();
        if (trimmedCode != null && trimmedCode.Length > 50)
        {
            throw new SiteValidationException("Facility code cannot exceed 50 characters.");
        }

        var trimmedDesc = request.Description?.Trim();
        if (trimmedDesc != null && trimmedDesc.Length > 500)
        {
            throw new SiteValidationException("Description cannot exceed 500 characters.");
        }

        var site = await _siteRepository.GetActiveSiteAsync(ct);
        if (site == null)
        {
            throw new SiteNotFoundException();
        }

        site.SiteName = trimmedName;
        site.FacilityCode = string.IsNullOrEmpty(trimmedCode) ? null : trimmedCode;
        site.Description = string.IsNullOrEmpty(trimmedDesc) ? null : trimmedDesc;
        site.UpdatedAt = DateTimeOffset.UtcNow;

        await _siteRepository.UpdateAsync(site, ct);

        return new UpdateSiteResponse(
            SiteId: site.SiteId,
            SiteName: site.SiteName,
            FacilityCode: site.FacilityCode,
            Description: site.Description,
            UpdatedAt: site.UpdatedAt
        );
    }
}
