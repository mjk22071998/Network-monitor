using NetworkObserver.Domain.Entities;

namespace NetworkObserver.Application.Interfaces.Repositories;

/// <summary>
/// Repository abstraction for Site entity persistence.
/// </summary>
public interface ISiteRepository
{
    Task<Site?> GetByIdAsync(int siteId, CancellationToken ct = default);
    Task<bool> ExistsAsync(int siteId, CancellationToken ct = default);
}
