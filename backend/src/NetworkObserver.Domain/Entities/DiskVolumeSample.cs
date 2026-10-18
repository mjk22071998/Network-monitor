namespace NetworkObserver.Domain.Entities;

public class DiskVolumeSample
{
    public long VolumeSampleId { get; set; }
    public long SampleId { get; set; }
    public string VolumeName { get; set; } = string.Empty;
    public float UsedPercentage { get; set; }
    public float TotalCapacityGb { get; set; }
    public float FreeSpaceGb { get; set; }

    // Navigation properties
    public virtual HealthSample? HealthSample { get; set; }
}
