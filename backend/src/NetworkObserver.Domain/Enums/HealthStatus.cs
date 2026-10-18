namespace NetworkObserver.Domain.Enums;

public enum HealthStatus : short
{
    Available = 1,
    Unavailable = 2,
    Stale = 3,
    Unsupported = 4,
    Error = 5
}
