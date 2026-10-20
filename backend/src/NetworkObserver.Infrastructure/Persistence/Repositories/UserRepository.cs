using Microsoft.EntityFrameworkCore;
using NetworkObserver.Application.Interfaces.Repositories;
using NetworkObserver.Domain.Entities;

namespace NetworkObserver.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of IUserRepository using type-safe compile-time LINQ expressions.
/// Zero raw SQL queries.
/// </summary>
public class UserRepository : IUserRepository
{
    private readonly NetworkObserverDbContext _dbContext;

    public UserRepository(NetworkObserverDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<User?> GetByIdAsync(int userId, CancellationToken ct = default)
    {
        return await _dbContext.Users
            .Include(u => u.Role)
            .Include(u => u.Site)
            .FirstOrDefaultAsync(u => u.UserId == userId, ct);
    }

    public async Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default)
    {
        return await _dbContext.Users
            .Include(u => u.Role)
            .Include(u => u.Site)
            .FirstOrDefaultAsync(u => u.Username == username, ct);
    }

    public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken ct = default)
    {
        return await _dbContext.Users
            .Include(u => u.Role)
            .Include(u => u.Site)
            .OrderBy(u => u.UserId)
            .ToListAsync(ct);
    }

    public async Task AddAsync(User user, CancellationToken ct = default)
    {
        await _dbContext.Users.AddAsync(user, ct);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(User user, CancellationToken ct = default)
    {
        _dbContext.Users.Update(user);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(User user, CancellationToken ct = default)
    {
        _dbContext.Users.Remove(user);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task<bool> ExistsByUsernameAsync(string username, CancellationToken ct = default)
    {
        return await _dbContext.Users.AnyAsync(u => u.Username == username, ct);
    }
}
