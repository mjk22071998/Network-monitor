namespace NetworkObserver.Domain.Entities;

public class HealthStatusType
{
    public short StatusId { get; set; }
    public string StatusCode { get; set; } = string.Empty;

    // Navigation properties
    public virtual ICollection<HealthSample> Samples { get; set; } = new List<HealthSample>();
}
