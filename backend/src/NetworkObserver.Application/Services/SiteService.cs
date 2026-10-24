using NetworkObserver.Application.DTOs;
using NetworkObserver.Application.Interfaces.Repositories;
using NetworkObserver.Application.Interfaces.Services;
using NetworkObserver.Domain.Constants;
using NetworkObserver.Domain.Entities;
using NetworkObserver.Domain.Exceptions;

namespace NetworkObserver.Application.Services;

/// <summary>
/// Domain application service for single-site enclave metadata.
/// Enforces business validation rules and encapsulates site persistence.
/// </summary>
public class SiteService : ISiteService
{
    private readonly ISiteRepository _siteRepository;
    private readonly IAuditLogRepository _auditLogRepository;

    public SiteService(
        ISiteRepository siteRepository,
        IAuditLogRepository? auditLogRepository = null)
    {
        _siteRepository = siteRepository;
        _auditLogRepository = auditLogRepository ?? new NullAuditLogRepository();
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

    public async Task<UpdateSiteResponse> UpdateActiveSiteAsync(UpdateSiteRequest request, int? userId = null, string ipAddress = "127.0.0.1", CancellationToken ct = default)
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

        var trimmedCode = request.EffectiveFacilityCode?.Trim();
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

        await _auditLogRepository.AddAsync(new AuditLog
        {
            UserId = userId,
            SiteId = site.SiteId,
            ActionCode = AuditActionCodes.SiteUpdate,
            TargetEntity = AuditTargetEntities.Site,
            TargetId = site.SiteId.ToString(),
            Details = $"Updated site metadata: Name='{site.SiteName}', Code='{site.FacilityCode}'.",
            IpAddress = string.IsNullOrWhiteSpace(ipAddress) ? "127.0.0.1" : ipAddress.Trim(),
            LoggedAt = DateTimeOffset.UtcNow
        }, ct);

        return new UpdateSiteResponse(
            SiteId: site.SiteId,
            SiteName: site.SiteName,
            FacilityCode: site.FacilityCode,
            Description: site.Description,
            UpdatedAt: site.UpdatedAt
        );
    }
}
