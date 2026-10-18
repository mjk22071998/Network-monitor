namespace NetworkObserver.Domain.Entities;

public class Site
{
    public int SiteId { get; set; }
    public string SiteName { get; set; } = string.Empty;
    public string? FacilityCode { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public virtual ICollection<Group> Groups { get; set; } = new List<Group>();
    public virtual ICollection<User> Users { get; set; } = new List<User>();
    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    public virtual MonitoringConfig? MonitoringConfig { get; set; }
    public virtual ICollection<PhysicalLayout> PhysicalLayouts { get; set; } = new List<PhysicalLayout>();
}
