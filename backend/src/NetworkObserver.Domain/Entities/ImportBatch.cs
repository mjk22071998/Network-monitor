namespace NetworkObserver.Domain.Entities;

public class ImportBatch
{
    public int BatchId { get; set; }
    public int UploadedByUserId { get; set; }
    public string SourceFileType { get; set; } = string.Empty;
    public string OriginalFilename { get; set; } = string.Empty;
    public int TotalRows { get; set; } = 0;
    public string Status { get; set; } = "STAGED";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CommittedAt { get; set; }

    // Navigation properties
    public virtual User? UploadedByUser { get; set; }
    public virtual ICollection<StagedDevice> StagedDevices { get; set; } = new List<StagedDevice>();
}
