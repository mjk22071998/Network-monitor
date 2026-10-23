using NetworkObserver.Application.DTOs;

namespace NetworkObserver.Application.Interfaces.Services;

/// <summary>
/// Application service contract for Group & Subgroup hierarchy management.
/// Complies with SOLID principles (SRP, DIP) and api_contracts_specification.md Section 5.
/// </summary>
public interface IGroupService
{
    Task<GroupTreeResponse> GetGroupTreeAsync(int? parentId = null, CancellationToken ct = default);
    Task<GroupDetailDto> CreateGroupAsync(CreateGroupRequest request, int? userId = null, string ipAddress = "127.0.0.1", CancellationToken ct = default);
    Task<GroupDetailDto> UpdateGroupAsync(int groupId, UpdateGroupRequest request, int? userId = null, string ipAddress = "127.0.0.1", CancellationToken ct = default);
    Task<DeleteGroupResponse> DeleteGroupAsync(int groupId, int? userId = null, string ipAddress = "127.0.0.1", CancellationToken ct = default);
}
