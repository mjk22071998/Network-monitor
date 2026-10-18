namespace NetworkObserver.Application.DTOs;

public record HealthCheckResponse(
    string Status,
    string Version,
    DateTimeOffset Timestamp,
    DatabaseHealthDto Database
);
