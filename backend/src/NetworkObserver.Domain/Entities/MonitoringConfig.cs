namespace NetworkObserver.Domain.Entities;

public class MonitoringConfig
{
    public int ConfigId { get; set; } = 1;
    public int SiteId { get; set; } = 1;
    public int ReachabilityIntervalSec { get; set; } = 5;
    public int ReachabilityTimeoutMs { get; set; } = 2000;
    public int HealthIntervalSec { get; set; } = 30;
    public int HealthTimeoutMs { get; set; } = 5000;
    public bool CascadeSuppressionEnabled { get; set; } = true;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public virtual Site? Site { get; set; }
}
