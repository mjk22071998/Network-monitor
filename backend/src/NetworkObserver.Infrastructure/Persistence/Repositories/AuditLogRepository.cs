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
        if (!string.IsNullOrWhiteSpace(log.Details))
        {
            var trimmed = log.Details.Trim();
            // Ensure details is valid JSON for the PostgreSQL JSONB column
            if (!((trimmed.StartsWith("{") && trimmed.EndsWith("}")) ||
                  (trimmed.StartsWith("[") && trimmed.EndsWith("]")) ||
                  (trimmed.StartsWith("\"") && trimmed.EndsWith("\""))))
            {
                log.Details = System.Text.Json.JsonSerializer.Serialize(new { message = trimmed });
            }
        }

        _dbContext.AuditLogs.Add(log);
        await _dbContext.SaveChangesAsync(ct);
        return log;
    }

    public async Task<AuditLog?> GetByIdAsync(long logId, CancellationToken ct = default)
    {
        return await _dbContext.AuditLogs
            .AsNoTracking()
            .Include(l => l.User)
            .FirstOrDefaultAsync(l => l.LogId == logId, ct);
    }

    public async Task<IReadOnlyList<AuditLog>> GetRecentAsync(int limit = 50, CancellationToken ct = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        return await _dbContext.AuditLogs
            .AsNoTracking()
            .Include(l => l.User)
            .OrderByDescending(l => l.LoggedAt)
            .ThenByDescending(l => l.LogId)
            .Take(safeLimit)
            .ToListAsync(ct);
    }

    public async Task<(IReadOnlyList<AuditLog> Items, int TotalCount)> GetLogsPagedAsync(
        int? userId,
        string? actionCode,
        DateTimeOffset? start,
        DateTimeOffset? end,
        int page,
        int limit,
        CancellationToken ct = default)
    {
        var safePage = Math.Max(1, page);
        var safeLimit = Math.Clamp(limit, 1, 100);

        var query = _dbContext.AuditLogs
            .AsNoTracking()
            .Include(l => l.User)
            .AsQueryable();

        if (userId.HasValue)
        {
            query = query.Where(l => l.UserId == userId.Value);
        }

        if (!string.IsNullOrWhiteSpace(actionCode))
        {
            var normalizedAction = actionCode.Trim().ToUpperInvariant();
            query = query.Where(l => l.ActionCode == normalizedAction);
        }

        if (start.HasValue)
        {
            query = query.Where(l => l.LoggedAt >= start.Value);
        }

        if (end.HasValue)
        {
            query = query.Where(l => l.LoggedAt <= end.Value);
        }

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(l => l.LoggedAt)
            .ThenByDescending(l => l.LogId)
            .Skip((safePage - 1) * safeLimit)
            .Take(safeLimit)
            .ToListAsync(ct);

        return (items, totalCount);
    }
}
