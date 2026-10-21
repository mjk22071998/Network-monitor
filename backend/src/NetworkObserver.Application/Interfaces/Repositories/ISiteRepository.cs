using NetworkObserver.Domain.Entities;

namespace NetworkObserver.Application.Interfaces.Repositories;

/// <summary>
/// Repository abstraction for Site entity persistence.
/// Complies with Repository Pattern and DIP. Zero raw SQL.
/// </summary>
public interface ISiteRepository
{
    Task<Site?> GetByIdAsync(int siteId, CancellationToken ct = default);
    Task<Site?> GetActiveSiteAsync(CancellationToken ct = default);
    Task<bool> ExistsAsync(int siteId, CancellationToken ct = default);
    Task UpdateAsync(Site site, CancellationToken ct = default);
}
