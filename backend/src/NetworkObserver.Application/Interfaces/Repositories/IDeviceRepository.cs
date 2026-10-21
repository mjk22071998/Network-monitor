using NetworkObserver.Domain.Entities;

namespace NetworkObserver.Application.Interfaces.Repositories;

public record DeviceQueryParameters(
    int Page = 1,
    int Limit = 50,
    int? GroupId = null,
    short? TypeId = null,
    string? Zone = null,
    string? Search = null,
    string SortBy = "primary_ip",
    string Order = "asc"
);

/// <summary>
/// Repository abstraction for Device inventory persistence.
/// Complies with Repository Pattern and DIP. 100% EF Core LINQ.
/// </summary>
public interface IDeviceRepository
{
    Task<(IReadOnlyList<Device> Items, int TotalCount)> GetPagedAsync(DeviceQueryParameters parameters, CancellationToken ct = default);
    Task<Device?> GetByIdAsync(int deviceId, CancellationToken ct = default);
    Task<Device?> GetByMacAsync(string macAddress, CancellationToken ct = default);
    Task<bool> ExistsAsync(int deviceId, CancellationToken ct = default);
    Task<bool> ExistsByMacAsync(string macAddress, CancellationToken ct = default);
    Task AddAsync(Device device, CancellationToken ct = default);
    Task UpdateAsync(Device device, CancellationToken ct = default);
    Task<IReadOnlyList<int>> DeleteAndOrphanChildrenAsync(int deviceId, CancellationToken ct = default);
}
