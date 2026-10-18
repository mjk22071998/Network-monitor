namespace NetworkObserver.Domain.Entities;

public class ReachabilityStatusType
{
    public short StatusId { get; set; }
    public string StatusCode { get; set; } = string.Empty;

    // Navigation properties
    public virtual ICollection<ReachabilitySample> Samples { get; set; } = new List<ReachabilitySample>();
}
