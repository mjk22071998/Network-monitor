namespace NetworkObserver.Domain.Entities;

public class PhysicalLayout
{
    public int LayoutId { get; set; }
    public int SiteId { get; set; } = 1;
    public string LayoutSlug { get; set; } = string.Empty;
    public string FloorName { get; set; } = string.Empty;
    public string LayoutFilePath { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public virtual Site? Site { get; set; }
}
