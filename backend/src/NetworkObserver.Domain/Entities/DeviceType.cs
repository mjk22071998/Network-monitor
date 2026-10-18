namespace NetworkObserver.Domain.Entities;

public class DeviceType
{
    public short TypeId { get; set; }
    public string TypeCode { get; set; } = string.Empty;

    // Navigation properties
    public virtual ICollection<Device> Devices { get; set; } = new List<Device>();
}
