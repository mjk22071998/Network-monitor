using NetworkObserver.Domain.Entities;

namespace NetworkObserver.Application.Interfaces.Repositories;

/// <summary>
/// Repository abstraction for User entity persistence.
/// Encapsulates all EF Core queries, keeping business services decoupled from database mechanics.
/// </summary>
public interface IUserRepository
{
    Task<User?> GetByIdAsync(int userId, CancellationToken ct = default);
    Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default);
    Task<IReadOnlyList<User>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
    Task UpdateAsync(User user, CancellationToken ct = default);
    Task DeleteAsync(User user, CancellationToken ct = default);
    Task<bool> ExistsByUsernameAsync(string username, CancellationToken ct = default);
}
