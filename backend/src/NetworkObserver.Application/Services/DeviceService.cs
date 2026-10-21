using System.Net;
using System.Text.RegularExpressions;
using NetworkObserver.Application.DTOs;
using NetworkObserver.Application.Interfaces.Repositories;
using NetworkObserver.Application.Interfaces.Services;
using NetworkObserver.Domain.Entities;
using NetworkObserver.Domain.Enums;
using NetworkObserver.Domain.Exceptions;

namespace NetworkObserver.Application.Services;

/// <summary>
/// Domain application service implementing Device Inventory CRUD, tiered role guards,
/// and hardware MAC immutability guarantees.
/// </summary>
public class DeviceService : IDeviceService
{
    private static readonly Regex MacRegex = new(
        @"^([0-9A-Fa-f]{2}[:-]){5}([0-9A-Fa-f]{2})$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly IDeviceRepository _deviceRepository;
    private readonly IGroupRepository _groupRepository;

    public DeviceService(IDeviceRepository deviceRepository, IGroupRepository groupRepository)
    {
        _deviceRepository = deviceRepository;
        _groupRepository = groupRepository;
    }

    public async Task<PagedResponse<DeviceListItemDto>> GetDevicesAsync(
        DeviceQueryParameters parameters,
        CancellationToken ct = default)
    {
        var (devices, totalCount) = await _deviceRepository.GetPagedAsync(parameters, ct);

        var safePage = Math.Max(1, parameters.Page);
        var safeLimit = Math.Clamp(parameters.Limit, 1, 500);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / safeLimit);

        var dtos = devices.Select(MapToListItemDto).ToList();

        var pagination = new PaginationMetadata(
            TotalRecords: totalCount,
            CurrentPage: safePage,
            PageSize: safeLimit,
            TotalPages: totalPages,
            HasNext: safePage < totalPages,
            HasPrev: safePage > 1
        );

        return new PagedResponse<DeviceListItemDto>(dtos, pagination);
    }

    public async Task<DeviceDetailDto> GetDeviceByIdAsync(int deviceId, CancellationToken ct = default)
    {
        var device = await _deviceRepository.GetByIdAsync(deviceId, ct);
        if (device == null)
        {
            throw new DeviceNotFoundException(deviceId);
        }

        return MapToDetailDto(device);
    }

    public async Task<DeviceDetailDto> GetDeviceByMacAsync(string macAddress, CancellationToken ct = default)
    {
        var normalizedMac = NormalizeMacAddress(macAddress);
        var device = await _deviceRepository.GetByMacAsync(normalizedMac, ct);
        if (device == null)
        {
            throw new DeviceNotFoundException(macAddress);
        }

        return MapToDetailDto(device);
    }

    public async Task<CreatedDeviceResponse> CreateDeviceAsync(
        CreateDeviceRequest request,
        RoleType operatorRole,
        CancellationToken ct = default)
    {
        ValidateDisplayName(request.DisplayName);
        ValidateIpAddress(request.PrimaryIp);
        var normalizedMac = NormalizeMacAddress(request.MacAddress);
        ValidateDeviceType(request.TypeId);
        ValidateRoleForEquipmentTier(request.TypeId, operatorRole, "provision");

        var groupExists = await _groupRepository.ExistsAsync(request.GroupId, ct);
        if (!groupExists)
        {
            throw new InvalidParentGroupException(request.GroupId);
        }

        if (request.ParentDeviceId.HasValue)
        {
            var parentExists = await _deviceRepository.ExistsAsync(request.ParentDeviceId.Value, ct);
            if (!parentExists)
            {
                throw new InvalidParentDeviceException(request.ParentDeviceId.Value);
            }
        }

        var macExists = await _deviceRepository.ExistsByMacAsync(normalizedMac, ct);
        if (macExists)
        {
            throw new DuplicateMacException(normalizedMac);
        }

        var device = new Device
        {
            DisplayName = request.DisplayName.Trim(),
            PrimaryIp = request.PrimaryIp.Trim(),
            MacAddress = normalizedMac,
            TypeId = request.TypeId,
            GroupId = request.GroupId,
            ParentDeviceId = request.ParentDeviceId,
            Vendor = request.Vendor?.Trim(),
            Model = request.Model?.Trim(),
            Zone = request.Zone?.Trim(),
            IsMonitored = request.IsMonitored,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await _deviceRepository.AddAsync(device, ct);

        return new CreatedDeviceResponse(
            DeviceId: device.DeviceId,
            MacAddress: device.MacAddress,
            PrimaryIp: device.PrimaryIp,
            DisplayName: device.DisplayName,
            TypeId: device.TypeId,
            GroupId: device.GroupId,
            ParentDeviceId: device.ParentDeviceId,
            Vendor: device.Vendor,
            Model: device.Model,
            Zone: device.Zone,
            IsMonitored: device.IsMonitored,
            CreatedAt: device.CreatedAt
        );
    }

    public async Task<UpdatedDeviceResponse> UpdateDeviceAsync(
        int deviceId,
        UpdateDeviceRequest request,
        RoleType operatorRole,
        CancellationToken ct = default)
    {
        var device = await _deviceRepository.GetByIdAsync(deviceId, ct);
        if (device == null)
        {
            throw new DeviceNotFoundException(deviceId);
        }

        // Validate tiered role permissions for existing device type AND target device type
        ValidateRoleForEquipmentTier(device.TypeId, operatorRole, "modify");
        ValidateRoleForEquipmentTier(request.TypeId, operatorRole, "modify");

        // Hardware Immutability Check: MAC Address cannot be changed
        if (!string.IsNullOrWhiteSpace(request.MacAddress))
        {
            var requestedMac = NormalizeMacAddress(request.MacAddress);
            if (!string.Equals(device.MacAddress, requestedMac, StringComparison.OrdinalIgnoreCase))
            {
                throw new MacAddressImmutableException(device.MacAddress, request.MacAddress);
            }
        }

        ValidateDisplayName(request.DisplayName);
        ValidateIpAddress(request.PrimaryIp);
        ValidateDeviceType(request.TypeId);

        var groupExists = await _groupRepository.ExistsAsync(request.GroupId, ct);
        if (!groupExists)
        {
            throw new InvalidParentGroupException(request.GroupId);
        }

        if (request.ParentDeviceId.HasValue)
        {
            if (request.ParentDeviceId.Value == deviceId)
            {
                throw new InvalidParentDeviceException(deviceId);
            }

            var parentExists = await _deviceRepository.ExistsAsync(request.ParentDeviceId.Value, ct);
            if (!parentExists)
            {
                throw new InvalidParentDeviceException(request.ParentDeviceId.Value);
            }
        }

        device.DisplayName = request.DisplayName.Trim();
        device.PrimaryIp = request.PrimaryIp.Trim();
        device.TypeId = request.TypeId;
        device.GroupId = request.GroupId;
        device.ParentDeviceId = request.ParentDeviceId;
        device.Vendor = request.Vendor?.Trim();
        device.Model = request.Model?.Trim();
        device.Zone = request.Zone?.Trim();
        device.IsMonitored = request.IsMonitored;
        device.UpdatedAt = DateTimeOffset.UtcNow;

        await _deviceRepository.UpdateAsync(device, ct);

        return new UpdatedDeviceResponse(
            DeviceId: device.DeviceId,
            MacAddress: device.MacAddress,
            PrimaryIp: device.PrimaryIp,
            DisplayName: device.DisplayName,
            TypeId: device.TypeId,
            GroupId: device.GroupId,
            ParentDeviceId: device.ParentDeviceId,
            Vendor: device.Vendor,
            Model: device.Model,
            Zone: device.Zone,
            IsMonitored: device.IsMonitored,
            UpdatedAt: device.UpdatedAt
        );
    }

    public async Task<DeleteDeviceResponse> DeleteDeviceAsync(
        int deviceId,
        RoleType operatorRole,
        CancellationToken ct = default)
    {
        var device = await _deviceRepository.GetByIdAsync(deviceId, ct);
        if (device == null)
        {
            throw new DeviceNotFoundException(deviceId);
        }

        ValidateRoleForEquipmentTier(device.TypeId, operatorRole, "delete");

        var orphanedChildrenIds = await _deviceRepository.DeleteAndOrphanChildrenAsync(deviceId, ct);

        return new DeleteDeviceResponse(
            DeletedDeviceId: deviceId,
            DeletedDisplayName: device.DisplayName,
            OrphanedChildrenCount: orphanedChildrenIds.Count,
            OrphanedChildrenDeviceIds: orphanedChildrenIds
        );
    }

    private static void ValidateRoleForEquipmentTier(short typeId, RoleType operatorRole, string action)
    {
        switch ((DeviceTypeEnum)typeId)
        {
            case DeviceTypeEnum.CoreRouter:
            case DeviceTypeEnum.AggregateSwitch:
                if (operatorRole != RoleType.NetworkAdmin)
                {
                    throw new UnauthorizedRoleException($"Only Network Administrators are authorized to {action} Backbone Core Routers and Aggregate Switches.");
                }
                break;

            case DeviceTypeEnum.AccessSwitch:
                if (operatorRole != RoleType.NetworkAdmin && operatorRole != RoleType.NetworkEngineer)
                {
                    throw new UnauthorizedRoleException($"IT Support operators are not authorized to {action} Access Switches. Network Engineer or Admin role required.");
                }
                break;

            case DeviceTypeEnum.Server:
            case DeviceTypeEnum.Pc:
                // Permitted across all operational roles (Admin, Engineer, IT Support)
                break;

            default:
                throw new InvalidDeviceTypeException(typeId);
        }
    }

    private static void ValidateDisplayName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidDeviceException("Device display name is required and cannot be empty.");
        }
        if (name.Trim().Length > 100)
        {
            throw new InvalidDeviceException("Device display name cannot exceed 100 characters.");
        }
    }

    private static void ValidateIpAddress(string ip)
    {
        if (string.IsNullOrWhiteSpace(ip) || !IPAddress.TryParse(ip.Trim(), out _))
        {
            throw new InvalidDeviceException($"Invalid IP address '{ip}'. Must be a valid IPv4 or IPv6 address.");
        }
    }

    private static string NormalizeMacAddress(string mac)
    {
        if (string.IsNullOrWhiteSpace(mac) || !MacRegex.IsMatch(mac.Trim()))
        {
            throw new InvalidDeviceException($"Invalid MAC address '{mac}'. Expected standard format (e.g. 00:1A:2B:AA:01:05).");
        }

        // Replace hyphens with colons and uppercase
        return mac.Trim().Replace('-', ':').ToUpperInvariant();
    }

    private static void ValidateDeviceType(short typeId)
    {
        if (!Enum.IsDefined(typeof(DeviceTypeEnum), typeId))
        {
            throw new InvalidDeviceTypeException(typeId);
        }
    }

    private static DeviceListItemDto MapToListItemDto(Device d) => new(
        DeviceId: d.DeviceId,
        MacAddress: d.MacAddress,
        PrimaryIp: d.PrimaryIp,
        DisplayName: d.DisplayName,
        TypeId: d.TypeId,
        TypeCode: GetDeviceTypeCode(d.TypeId),
        GroupId: d.GroupId,
        GroupName: d.Group?.GroupName ?? "Unassigned Group",
        ParentDeviceId: d.ParentDeviceId,
        ParentDeviceName: d.ParentDevice?.DisplayName,
        Vendor: d.Vendor,
        Model: d.Model,
        Zone: d.Zone,
        IsMonitored: d.IsMonitored,
        LatestReachability: null,
        LatestHealth: null
    );

    private static DeviceDetailDto MapToDetailDto(Device d) => new(
        DeviceId: d.DeviceId,
        MacAddress: d.MacAddress,
        PrimaryIp: d.PrimaryIp,
        DisplayName: d.DisplayName,
        TypeId: d.TypeId,
        TypeCode: GetDeviceTypeCode(d.TypeId),
        GroupId: d.GroupId,
        GroupName: d.Group?.GroupName ?? "Unassigned Group",
        ParentDeviceId: d.ParentDeviceId,
        ParentDeviceName: d.ParentDevice?.DisplayName,
        Vendor: d.Vendor,
        Model: d.Model,
        Zone: d.Zone,
        IsMonitored: d.IsMonitored,
        CreatedAt: d.CreatedAt,
        UpdatedAt: d.UpdatedAt,
        LatestReachability: null,
        LatestHealth: null
    );

    private static string GetDeviceTypeCode(short typeId) => (DeviceTypeEnum)typeId switch
    {
        DeviceTypeEnum.CoreRouter => "CORE_ROUTER",
        DeviceTypeEnum.AggregateSwitch => "AGGREGATE_SWITCH",
        DeviceTypeEnum.AccessSwitch => "ACCESS_SWITCH",
        DeviceTypeEnum.Server => "SERVER",
        DeviceTypeEnum.Pc => "PC",
        _ => "UNKNOWN"
    };
}
