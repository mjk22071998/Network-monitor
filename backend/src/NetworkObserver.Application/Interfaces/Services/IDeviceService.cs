using NetworkObserver.Application.DTOs;
using NetworkObserver.Application.Interfaces.Repositories;
using NetworkObserver.Domain.Enums;

namespace NetworkObserver.Application.Interfaces.Services;

/// <summary>
/// Application service contract for Device Inventory CRUD and tiered role governance.
/// Complies with SOLID principles (SRP, DIP) and api_contracts_specification.md Section 6.
/// </summary>
public interface IDeviceService
{
    Task<PagedResponse<DeviceListItemDto>> GetDevicesAsync(DeviceQueryParameters parameters, CancellationToken ct = default);
    Task<DeviceDetailDto> GetDeviceByIdAsync(int deviceId, CancellationToken ct = default);
    Task<DeviceDetailDto> GetDeviceByMacAsync(string macAddress, CancellationToken ct = default);
    Task<CreatedDeviceResponse> CreateDeviceAsync(CreateDeviceRequest request, RoleType operatorRole, CancellationToken ct = default);
    Task<UpdatedDeviceResponse> UpdateDeviceAsync(int deviceId, UpdateDeviceRequest request, RoleType operatorRole, CancellationToken ct = default);
    Task<DeleteDeviceResponse> DeleteDeviceAsync(int deviceId, RoleType operatorRole, CancellationToken ct = default);
}
