using NetworkObserver.Domain.Entities;

namespace NetworkObserver.Application.Interfaces.Repositories;

/// <summary>
/// Repository abstraction for Group & Subgroup hierarchy persistence.
/// Complies with Repository Pattern and DIP. 100% EF Core LINQ.
/// </summary>
public interface IGroupRepository
{
    Task<IReadOnlyList<Group>> GetAllAsync(CancellationToken ct = default);
    Task<Group?> GetByIdAsync(int groupId, CancellationToken ct = default);
    Task<bool> ExistsAsync(int groupId, CancellationToken ct = default);
    Task<int> GetDeviceCountAsync(int groupId, CancellationToken ct = default);
    Task<Dictionary<int, int>> GetDeviceCountsByGroupAsync(CancellationToken ct = default);
    Task AddAsync(Group group, CancellationToken ct = default);
    Task UpdateAsync(Group group, CancellationToken ct = default);
    Task<int> DeleteAndReassignAsync(int groupIdToDelete, int targetGroupId, CancellationToken ct = default);
}
