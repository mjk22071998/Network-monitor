namespace NetworkObserver.Domain.Entities;

public class AuditLog
{
    public long LogId { get; set; }
    public int? UserId { get; set; }
    public int SiteId { get; set; } = 1;
    public string ActionCode { get; set; } = string.Empty;
    public string TargetEntity { get; set; } = string.Empty;
    public string? TargetId { get; set; }
    public string? Details { get; set; } // Stored as JSONB in PostgreSQL
    public string IpAddress { get; set; } = string.Empty;
    public DateTimeOffset LoggedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public virtual User? User { get; set; }
    public virtual Site? Site { get; set; }
}
