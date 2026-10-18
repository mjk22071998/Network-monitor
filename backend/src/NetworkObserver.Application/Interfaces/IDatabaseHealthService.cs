using NetworkObserver.Application.DTOs;

namespace NetworkObserver.Application.Interfaces;

public interface IDatabaseHealthService
{
    Task<DatabaseHealthDto> CheckHealthAsync(CancellationToken cancellationToken = default);
}
