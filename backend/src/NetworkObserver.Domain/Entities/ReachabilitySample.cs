namespace NetworkObserver.Domain.Entities;

public class ReachabilitySample
{
    public long SampleId { get; set; }
    public int DeviceId { get; set; }
    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;
    public short StatusId { get; set; }
    public int? LatencyUs { get; set; }
    public float LossPercentage { get; set; } = 0.0f;
    public int? NtpOffsetUs { get; set; }
    public bool IsSuppressed { get; set; } = false;

    // Navigation properties
    public virtual Device? Device { get; set; }
    public virtual ReachabilityStatusType? StatusType { get; set; }
}
