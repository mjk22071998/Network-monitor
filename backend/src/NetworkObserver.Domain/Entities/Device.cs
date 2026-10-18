namespace NetworkObserver.Domain.Entities;

public class Device
{
    public int DeviceId { get; set; }
    public string MacAddress { get; set; } = string.Empty;
    public string PrimaryIp { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public short TypeId { get; set; }
    public int GroupId { get; set; } = 1;
    public int? ParentDeviceId { get; set; }
    public string? Zone { get; set; }
    public string? Vendor { get; set; }
    public string? Model { get; set; }
    public bool IsMonitored { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public virtual DeviceType? DeviceType { get; set; }
    public virtual Group? Group { get; set; }
    public virtual Device? ParentDevice { get; set; }
    public virtual ICollection<Device> ChildDevices { get; set; } = new List<Device>();
    public virtual ICollection<ReachabilitySample> ReachabilitySamples { get; set; } = new List<ReachabilitySample>();
    public virtual ICollection<HealthSample> HealthSamples { get; set; } = new List<HealthSample>();
}
