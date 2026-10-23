using NetworkObserver.Application.DTOs;

namespace NetworkObserver.Application.Interfaces.Services;

/// <summary>
/// Application service contract for single-site enclave management.
/// Complies with SOLID principles (SRP, DIP) and api_contracts_specification.md Section 4.
/// </summary>
public interface ISiteService
{
    Task<SiteDto> GetActiveSiteAsync(CancellationToken ct = default);
    Task<UpdateSiteResponse> UpdateActiveSiteAsync(UpdateSiteRequest request, int? userId = null, string ipAddress = "127.0.0.1", CancellationToken ct = default);
}
