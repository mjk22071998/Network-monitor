using Microsoft.EntityFrameworkCore;
using NetworkObserver.Application.Interfaces.Repositories;
using NetworkObserver.Domain.Entities;

namespace NetworkObserver.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of IDeviceRepository.
/// Zero raw SQL queries; 100% compile-time type-safe LINQ.
/// </summary>
public class DeviceRepository : IDeviceRepository
{
    private readonly NetworkObserverDbContext _dbContext;

    public DeviceRepository(NetworkObserverDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<(IReadOnlyList<Device> Items, int TotalCount)> GetPagedAsync(
        DeviceQueryParameters parameters,
        CancellationToken ct = default)
    {
        var query = _dbContext.Devices
            .Include(d => d.Group)
            .Include(d => d.ParentDevice)
            .Include(d => d.DeviceType)
            .AsNoTracking();

        if (parameters.GroupId.HasValue)
        {
            query = query.Where(d => d.GroupId == parameters.GroupId.Value);
        }

        if (parameters.TypeId.HasValue)
        {
            query = query.Where(d => d.TypeId == parameters.TypeId.Value);
        }

        if (!string.IsNullOrWhiteSpace(parameters.Zone))
        {
            var zoneFilter = parameters.Zone.Trim().ToLower();
            query = query.Where(d => d.Zone != null && d.Zone.ToLower().Contains(zoneFilter));
        }

        if (!string.IsNullOrWhiteSpace(parameters.Search))
        {
            var search = parameters.Search.Trim().ToLower();
            query = query.Where(d => d.DisplayName.ToLower().Contains(search)
                                  || d.PrimaryIp.ToLower().Contains(search)
                                  || d.MacAddress.ToLower().Contains(search));
        }

        var totalCount = await query.CountAsync(ct);

        var isDesc = string.Equals(parameters.Order, "desc", StringComparison.OrdinalIgnoreCase);
        query = parameters.SortBy.ToLowerInvariant() switch
        {
            "display_name" => isDesc ? query.OrderByDescending(d => d.DisplayName) : query.OrderBy(d => d.DisplayName),
            "mac_address" => isDesc ? query.OrderByDescending(d => d.MacAddress) : query.OrderBy(d => d.MacAddress),
            "device_id" => isDesc ? query.OrderByDescending(d => d.DeviceId) : query.OrderBy(d => d.DeviceId),
            "created_at" => isDesc ? query.OrderByDescending(d => d.CreatedAt) : query.OrderBy(d => d.CreatedAt),
            _ => isDesc ? query.OrderByDescending(d => d.PrimaryIp) : query.OrderBy(d => d.PrimaryIp)
        };

        var safePage = Math.Max(1, parameters.Page);
        var safeLimit = Math.Clamp(parameters.Limit, 1, 500);

        var items = await query
            .Skip((safePage - 1) * safeLimit)
            .Take(safeLimit)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<Device?> GetByIdAsync(int deviceId, CancellationToken ct = default)
    {
        return await _dbContext.Devices
            .Include(d => d.Group)
            .Include(d => d.ParentDevice)
            .Include(d => d.DeviceType)
            .FirstOrDefaultAsync(d => d.DeviceId == deviceId, ct);
    }

    public async Task<Device?> GetByMacAsync(string macAddress, CancellationToken ct = default)
    {
        var cleanMac = macAddress.Trim().ToUpperInvariant();
        return await _dbContext.Devices
            .Include(d => d.Group)
            .Include(d => d.ParentDevice)
            .Include(d => d.DeviceType)
            .FirstOrDefaultAsync(d => d.MacAddress.ToUpper() == cleanMac, ct);
    }

    public async Task<bool> ExistsAsync(int deviceId, CancellationToken ct = default)
    {
        return await _dbContext.Devices.AnyAsync(d => d.DeviceId == deviceId, ct);
    }

    public async Task<bool> ExistsByMacAsync(string macAddress, CancellationToken ct = default)
    {
        var cleanMac = macAddress.Trim().ToUpperInvariant();
        return await _dbContext.Devices.AnyAsync(d => d.MacAddress.ToUpper() == cleanMac, ct);
    }

    public async Task AddAsync(Device device, CancellationToken ct = default)
    {
        await _dbContext.Devices.AddAsync(device, ct);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Device device, CancellationToken ct = default)
    {
        _dbContext.Devices.Update(device);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<int>> DeleteAndOrphanChildrenAsync(int deviceId, CancellationToken ct = default)
    {
        var device = await _dbContext.Devices.FirstOrDefaultAsync(d => d.DeviceId == deviceId, ct);
        if (device == null)
        {
            return Array.Empty<int>();
        }

        var children = await _dbContext.Devices
            .Where(d => d.ParentDeviceId == deviceId)
            .ToListAsync(ct);

        var orphanedIds = children.Select(c => c.DeviceId).ToList();

        foreach (var child in children)
        {
            child.ParentDeviceId = null;
            child.UpdatedAt = DateTimeOffset.UtcNow;
        }

        _dbContext.Devices.Remove(device);
        await _dbContext.SaveChangesAsync(ct);

        return orphanedIds;
    }
}
