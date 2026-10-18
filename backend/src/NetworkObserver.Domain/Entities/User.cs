namespace NetworkObserver.Domain.Entities;

public class User
{
    public int UserId { get; set; }
    public int SiteId { get; set; } = 1;
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public short RoleId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public virtual Site? Site { get; set; }
    public virtual Role? Role { get; set; }
    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    public virtual ICollection<ImportBatch> ImportBatches { get; set; } = new List<ImportBatch>();
}
