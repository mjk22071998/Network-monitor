using Microsoft.EntityFrameworkCore;
using NetworkObserver.Application.Interfaces.Repositories;
using NetworkObserver.Domain.Entities;

namespace NetworkObserver.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of ISiteRepository.
/// 100% type-safe compile-time LINQ queries with zero raw SQL.
/// </summary>
public class SiteRepository : ISiteRepository
{
    private readonly NetworkObserverDbContext _dbContext;

    public SiteRepository(NetworkObserverDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Site?> GetByIdAsync(int siteId, CancellationToken ct = default)
    {
        return await _dbContext.Sites.FirstOrDefaultAsync(s => s.SiteId == siteId, ct);
    }

    public async Task<Site?> GetActiveSiteAsync(CancellationToken ct = default)
    {
        // Enclave operates as a single-site boundary (site_id = 1)
        return await _dbContext.Sites.FirstOrDefaultAsync(s => s.SiteId == 1, ct)
               ?? await _dbContext.Sites.FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsAsync(int siteId, CancellationToken ct = default)
    {
        return await _dbContext.Sites.AnyAsync(s => s.SiteId == siteId, ct);
    }

    public async Task UpdateAsync(Site site, CancellationToken ct = default)
    {
        _dbContext.Sites.Update(site);
        await _dbContext.SaveChangesAsync(ct);
    }
}
