using Microsoft.EntityFrameworkCore;
using NetworkObserver.Application.Interfaces.Repositories;
using NetworkObserver.Domain.Entities;

namespace NetworkObserver.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of IGroupRepository.
/// Guarantees 100% type-safe compile-time LINQ with zero raw SQL queries.
/// </summary>
public class GroupRepository : IGroupRepository
{
    private readonly NetworkObserverDbContext _dbContext;

    public GroupRepository(NetworkObserverDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Group>> GetAllAsync(CancellationToken ct = default)
    {
        return await _dbContext.Groups
            .AsNoTracking()
            .OrderBy(g => g.GroupId)
            .ToListAsync(ct);
    }

    public async Task<Group?> GetByIdAsync(int groupId, CancellationToken ct = default)
    {
        return await _dbContext.Groups.FirstOrDefaultAsync(g => g.GroupId == groupId, ct);
    }

    public async Task<bool> ExistsAsync(int groupId, CancellationToken ct = default)
    {
        return await _dbContext.Groups.AnyAsync(g => g.GroupId == groupId, ct);
    }

    public async Task<int> GetDeviceCountAsync(int groupId, CancellationToken ct = default)
    {
        return await _dbContext.Devices.CountAsync(d => d.GroupId == groupId, ct);
    }

    public async Task<Dictionary<int, int>> GetDeviceCountsByGroupAsync(CancellationToken ct = default)
    {
        return await _dbContext.Devices
            .GroupBy(d => d.GroupId)
            .Select(g => new { GroupId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.GroupId, x => x.Count, ct);
    }

    public async Task AddAsync(Group group, CancellationToken ct = default)
    {
        await _dbContext.Groups.AddAsync(group, ct);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Group group, CancellationToken ct = default)
    {
        _dbContext.Groups.Update(group);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task<int> DeleteAndReassignAsync(int groupIdToDelete, int targetGroupId, CancellationToken ct = default)
    {
        var group = await _dbContext.Groups.FirstOrDefaultAsync(g => g.GroupId == groupIdToDelete, ct);
        if (group == null)
        {
            return 0;
        }

        // 1. Reparent any child subgroups to the deleted group's parent
        var childSubgroups = await _dbContext.Groups
            .Where(g => g.ParentGroupId == groupIdToDelete)
            .ToListAsync(ct);

        foreach (var child in childSubgroups)
        {
            child.ParentGroupId = group.ParentGroupId;
        }

        // 2. Reassign all member devices to target group (default: Group 1 Unassigned Group)
        var memberDevices = await _dbContext.Devices
            .Where(d => d.GroupId == groupIdToDelete)
            .ToListAsync(ct);

        foreach (var dev in memberDevices)
        {
            dev.GroupId = targetGroupId;
            dev.UpdatedAt = DateTimeOffset.UtcNow;
        }

        var reassignedCount = memberDevices.Count;

        // 3. Remove the group
        _dbContext.Groups.Remove(group);
        await _dbContext.SaveChangesAsync(ct);

        return reassignedCount;
    }
}
