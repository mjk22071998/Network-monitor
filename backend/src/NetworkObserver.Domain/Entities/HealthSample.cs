namespace NetworkObserver.Domain.Entities;

public class HealthSample
{
    public long SampleId { get; set; }
    public int DeviceId { get; set; }
    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;
    public short StatusId { get; set; }
    public float? CpuUtilizationPct { get; set; }
    public float? RamUtilizationPct { get; set; }
    public float? NetRxKbps { get; set; }
    public float? NetTxKbps { get; set; }
    public float? TemperatureCelsius { get; set; }

    // Navigation properties
    public virtual Device? Device { get; set; }
    public virtual HealthStatusType? StatusType { get; set; }
    public virtual ICollection<DiskVolumeSample> DiskVolumeSamples { get; set; } = new List<DiskVolumeSample>();
}
