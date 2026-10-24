namespace NetworkObserver.Application.DTOs;

/// <summary>
/// Authoritative Site Enclave DTOs complying with api_contracts_specification.md Section 4.
/// </summary>
public record SiteDto(
    int SiteId,
    string SiteName,
    string? FacilityCode,
    string? Description,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

public record UpdateSiteRequest(
    string SiteName,
    string? FacilityCode,
    string? Description,
    string? SiteCode = null
)
{
    public string? EffectiveFacilityCode => !string.IsNullOrWhiteSpace(FacilityCode) ? FacilityCode : SiteCode;
}

public record UpdateSiteResponse(
    int SiteId,
    string SiteName,
    string? FacilityCode,
    string? Description,
    DateTimeOffset UpdatedAt
);
