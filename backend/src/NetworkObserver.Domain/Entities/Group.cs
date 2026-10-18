namespace NetworkObserver.Domain.Entities;

public class Group
{
    public int GroupId { get; set; }
    public int SiteId { get; set; } = 1;
    public int? ParentGroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public virtual Site? Site { get; set; }
    public virtual Group? ParentGroup { get; set; }
    public virtual ICollection<Group> Subgroups { get; set; } = new List<Group>();
    public virtual ICollection<Device> Devices { get; set; } = new List<Device>();
}
