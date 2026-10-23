using NetworkObserver.Application.DTOs;
using NetworkObserver.Application.Interfaces.Repositories;
using NetworkObserver.Application.Interfaces.Services;
using NetworkObserver.Domain.Entities;

namespace NetworkObserver.Application.Services;

/// <summary>
/// Application service implementing central audit trail logging and query execution.
/// Complies with GoF Service Layer pattern, 100% EF Core LINQ, zero raw SQL queries.
/// </summary>
public class AuditService : IAuditService
{
    private readonly IAuditLogRepository _auditLogRepository;

    public AuditService(IAuditLogRepository auditLogRepository)
    {
        _auditLogRepository = auditLogRepository;
    }

    public async Task<AuditLog> LogAsync(
        int? userId,
        string actionCode,
        string targetEntity,
        string? targetId,
        string? details,
        string ipAddress = "127.0.0.1",
        int siteId = 1,
        CancellationToken ct = default)
    {
        var log = new AuditLog
        {
            UserId = userId,
            SiteId = siteId,
            ActionCode = actionCode.ToUpperInvariant(),
            TargetEntity = targetEntity.ToUpperInvariant(),
            TargetId = targetId,
            Details = details,
            IpAddress = string.IsNullOrWhiteSpace(ipAddress) ? "127.0.0.1" : ipAddress.Trim(),
            LoggedAt = DateTimeOffset.UtcNow
        };

        return await _auditLogRepository.AddAsync(log, ct);
    }

    public async Task<AuditLogsPagedResponse> GetAuditLogsPagedAsync(
        AuditLogQueryParameters parameters,
        CancellationToken ct = default)
    {
        var safePage = Math.Max(1, parameters.Page);
        var safeLimit = Math.Clamp(parameters.Limit, 1, 100);

        var (items, totalCount) = await _auditLogRepository.GetLogsPagedAsync(
            parameters.UserId,
            parameters.ActionCode,
            parameters.Start,
            parameters.End,
            safePage,
            safeLimit,
            ct);

        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / safeLimit);

        var dtos = items.Select(l => new AuditLogDto(
            LogId: l.LogId,
            UserId: l.UserId,
            Username: l.User?.Username,
            ActionCode: l.ActionCode,
            TargetEntity: l.TargetEntity,
            TargetId: l.TargetId,
            Details: FormatDetails(l.Details),
            IpAddress: l.IpAddress,
            LoggedAt: l.LoggedAt
        )).ToList();

        var pagination = new PaginationMetadata(
            TotalRecords: totalCount,
            CurrentPage: safePage,
            PageSize: safeLimit,
            TotalPages: totalPages,
            HasNext: safePage < totalPages,
            HasPrev: safePage > 1
        );

        return new AuditLogsPagedResponse(dtos, pagination);
    }

    private static string? FormatDetails(string? rawDetails)
    {
        if (string.IsNullOrWhiteSpace(rawDetails)) return null;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(rawDetails);
            if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("message", out var msgProp) &&
                doc.RootElement.EnumerateObject().Count() == 1)
            {
                return msgProp.GetString();
            }
        }
        catch
        {
            // If not JSON, return as is
        }
        return rawDetails;
    }
}
