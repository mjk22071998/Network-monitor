using NetworkObserver.Domain.Entities;

namespace NetworkObserver.Application.Interfaces.Repositories;

/// <summary>
/// Repository abstraction for immutable audit logs.
/// Complies with GoF Repository Pattern and non-repudiation security constraints.
/// 100% EF Core LINQ.
/// </summary>
public interface IAuditLogRepository
{
    Task<AuditLog> AddAsync(AuditLog log, CancellationToken ct = default);
    Task<AuditLog?> GetByIdAsync(long logId, CancellationToken ct = default);
    Task<IReadOnlyList<AuditLog>> GetRecentAsync(int limit = 50, CancellationToken ct = default);
}
