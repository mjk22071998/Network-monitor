using Microsoft.EntityFrameworkCore;
using NetworkObserver.Application.Interfaces.Repositories;
using NetworkObserver.Domain.Entities;

namespace NetworkObserver.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of ISiteRepository.
/// Zero raw SQL queries.
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

    public async Task<bool> ExistsAsync(int siteId, CancellationToken ct = default)
    {
        return await _dbContext.Sites.AnyAsync(s => s.SiteId == siteId, ct);
    }
}
