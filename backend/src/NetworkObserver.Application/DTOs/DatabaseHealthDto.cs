namespace NetworkObserver.Application.DTOs;

public record DatabaseHealthDto(
    bool IsConnected,
    string DatabaseEngine,
    int SiteCount,
    int GroupCount,
    int DeviceCount,
    int UserCount,
    long QueryLatencyMs,
    string? ErrorMessage = null
);
