using Microsoft.EntityFrameworkCore;
using NetworkObserver.Application.Interfaces.Repositories;
using NetworkObserver.Domain.Entities;

namespace NetworkObserver.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core 10 repository for immutable append-only audit trail logging.
/// Complies with GoF Repository Pattern, 100% type-safe compile-time LINQ, zero raw SQL.
/// </summary>
public class AuditLogRepository : IAuditLogRepository
{
    private readonly NetworkObserverDbContext _dbContext;

    public AuditLogRepository(NetworkObserverDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AuditLog> AddAsync(AuditLog log, CancellationToken ct = default)
    {
        _dbContext.AuditLogs.Add(log);
        await _dbContext.SaveChangesAsync(ct);
        return log;
    }

    public async Task<AuditLog?> GetByIdAsync(long logId, CancellationToken ct = default)
    {
        return await _dbContext.AuditLogs
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.LogId == logId, ct);
    }

    public async Task<IReadOnlyList<AuditLog>> GetRecentAsync(int limit = 50, CancellationToken ct = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        return await _dbContext.AuditLogs
            .AsNoTracking()
            .OrderByDescending(l => l.LoggedAt)
            .Take(safeLimit)
            .ToListAsync(ct);
    }
}
