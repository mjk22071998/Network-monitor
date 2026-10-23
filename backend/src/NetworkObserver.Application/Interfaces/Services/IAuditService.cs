using NetworkObserver.Application.DTOs;
using NetworkObserver.Domain.Entities;

namespace NetworkObserver.Application.Interfaces.Services;

/// <summary>
/// Application service contract for security audit logging and non-repudiation querying.
/// Complies with SOLID principles (SRP, DIP), security_and_rbac_specification.md Section 8,
/// and api_contracts_specification.md Section 11.
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// Writes an immutable audit log entry for an operator action or system security event.
    /// </summary>
    Task<AuditLog> LogAsync(
        int? userId,
        string actionCode,
        string targetEntity,
        string? targetId,
        string? details,
        string ipAddress = "127.0.0.1",
        int siteId = 1,
        CancellationToken ct = default);

    /// <summary>
    /// Queries immutable audit logs with optional filters and tactical pagination.
    /// Accessible exclusively by NETWORK_ADMIN.
    /// </summary>
    Task<AuditLogsPagedResponse> GetAuditLogsPagedAsync(
        AuditLogQueryParameters parameters,
        CancellationToken ct = default);
}
