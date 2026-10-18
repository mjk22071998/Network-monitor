namespace NetworkObserver.Domain.Entities;

public class StagedDevice
{
    public long StagedId { get; set; }
    public int BatchId { get; set; }
    public int SourceRowNumber { get; set; }
    public string? RawMacAddress { get; set; }
    public string? RawPrimaryIp { get; set; }
    public string? RawDisplayName { get; set; }
    public string? RawDeviceType { get; set; }
    public string? RawGroupPath { get; set; }
    public string? RawParentIdentifier { get; set; }
    public string? RawZone { get; set; }
    public string? RawVendor { get; set; }
    public string? RawModel { get; set; }
    public bool IsMonitored { get; set; } = true;
    public short? ResolvedTypeId { get; set; }
    public int? ResolvedGroupId { get; set; }
    public int? ResolvedParentDeviceId { get; set; }
    public string ValidationStatus { get; set; } = "INCOMPLETE";
    public string? ValidationErrors { get; set; }
    public bool IsUserModified { get; set; } = false;

    // Navigation properties
    public virtual ImportBatch? Batch { get; set; }
    public virtual DeviceType? ResolvedType { get; set; }
    public virtual Group? ResolvedGroup { get; set; }
    public virtual Device? ResolvedParentDevice { get; set; }
}
