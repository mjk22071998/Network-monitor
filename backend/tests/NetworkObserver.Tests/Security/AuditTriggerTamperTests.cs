using Microsoft.EntityFrameworkCore;
using NetworkObserver.Domain.Constants;
using NetworkObserver.Domain.Entities;
using NetworkObserver.Infrastructure.Persistence;
using Npgsql;
using Xunit;

namespace NetworkObserver.Tests.Security;

/// <summary>
/// Non-repudiation integration test suite verifying that PostgreSQL 18 PL/pgSQL triggers
/// (trg_audit_logs_no_update and trg_audit_logs_no_delete) actively abort any attempt
/// to mutate or delete audit log records.
/// </summary>
public class AuditTriggerTamperTests
{
    private const string ConnectionString = "Host=localhost;Port=5432;Database=network_observer;Username=postgres;";

    private NetworkObserverDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<NetworkObserverDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        return new NetworkObserverDbContext(options);
    }

    [Fact]
    public async Task AuditLogs_WhenUpdateAttempted_PostgresTriggerAbortsWithSecurityViolation()
    {
        await using var context = CreateDbContext();

        if (!await context.Database.CanConnectAsync())
        {
            return;
        }

        // Insert a valid audit log
        var log = new AuditLog
        {
            UserId = 1,
            SiteId = 1,
            ActionCode = AuditActionCodes.DeviceCreate,
            TargetEntity = AuditTargetEntities.Device,
            TargetId = "9999",
            Details = "{\"message\":\"Test tamper trigger\"}",
            IpAddress = "127.0.0.1",
            LoggedAt = DateTimeOffset.UtcNow
        };

        context.AuditLogs.Add(log);
        await context.SaveChangesAsync();

        // Attempt to maliciously UPDATE the audit log record
        log.Details = "{\"message\":\"Tampered details\"}";

        var ex = await Assert.ThrowsAsync<DbUpdateException>(async () =>
        {
            await context.SaveChangesAsync();
        });

        // Verify the Postgres PL/pgSQL trigger message
        var pgEx = ex.GetBaseException() as PostgresException;
        Assert.NotNull(pgEx);
        Assert.Contains("SECURITY VIOLATION", pgEx.MessageText);
        Assert.Contains("audit_logs records are immutable and cannot be updated or deleted", pgEx.MessageText);
    }

    [Fact]
    public async Task AuditLogs_WhenDeleteAttempted_PostgresTriggerAbortsWithSecurityViolation()
    {
        await using var context = CreateDbContext();

        if (!await context.Database.CanConnectAsync())
        {
            return;
        }

        // Insert a valid audit log
        var log = new AuditLog
        {
            UserId = 1,
            SiteId = 1,
            ActionCode = AuditActionCodes.DeviceDelete,
            TargetEntity = AuditTargetEntities.Device,
            TargetId = "9998",
            Details = "{\"message\":\"Test tamper delete trigger\"}",
            IpAddress = "127.0.0.1",
            LoggedAt = DateTimeOffset.UtcNow
        };

        context.AuditLogs.Add(log);
        await context.SaveChangesAsync();

        // Attempt to maliciously DELETE the audit log record
        context.AuditLogs.Remove(log);

        var ex = await Assert.ThrowsAsync<DbUpdateException>(async () =>
        {
            await context.SaveChangesAsync();
        });

        var pgEx = ex.GetBaseException() as PostgresException;
        Assert.NotNull(pgEx);
        Assert.Contains("SECURITY VIOLATION", pgEx.MessageText);
        Assert.Contains("audit_logs records are immutable and cannot be updated or deleted", pgEx.MessageText);
    }
}
