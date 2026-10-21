using NetworkObserver.Application.DTOs;
using NetworkObserver.Application.Interfaces.Repositories;
using NetworkObserver.Application.Services;
using NetworkObserver.Domain.Entities;
using NetworkObserver.Domain.Exceptions;
using Xunit;

namespace NetworkObserver.Tests.Services;

public class GroupServiceTests
{
    private readonly StubGroupRepository _groupRepo;
    private readonly GroupService _groupService;

    public GroupServiceTests()
    {
        _groupRepo = new StubGroupRepository();
        _groupService = new GroupService(_groupRepo);
    }

    [Fact]
    public async Task GetGroupTreeAsync_ReturnsHierarchicalTree_WithDirectDeviceCounts()
    {
        // Arrange
        var g1 = new Group { GroupId = 1, GroupName = "Unassigned Group", ParentGroupId = null };
        var g2 = new Group { GroupId = 2, GroupName = "Operations", ParentGroupId = null };
        var g3 = new Group { GroupId = 3, GroupName = "Assembly Line A", ParentGroupId = 2 };
        var g4 = new Group { GroupId = 4, GroupName = "Assembly Line B", ParentGroupId = 2 };

        _groupRepo.AddGroup(g1);
        _groupRepo.AddGroup(g2);
        _groupRepo.AddGroup(g3);
        _groupRepo.AddGroup(g4);

        _groupRepo.SetDeviceCount(1, 5);
        _groupRepo.SetDeviceCount(2, 0);
        _groupRepo.SetDeviceCount(3, 28);
        _groupRepo.SetDeviceCount(4, 24);

        // Act
        var result = await _groupService.GetGroupTreeAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Groups.Count); // G1 and G2 are root

        var rootG1 = result.Groups.First(g => g.GroupId == 1);
        Assert.Equal("Unassigned Group", rootG1.GroupName);
        Assert.Equal(5, rootG1.DeviceCount);
        Assert.Empty(rootG1.Children);

        var rootG2 = result.Groups.First(g => g.GroupId == 2);
        Assert.Equal("Operations", rootG2.GroupName);
        Assert.Equal(0, rootG2.DeviceCount);
        Assert.Equal(2, rootG2.Children.Count);

        var childG3 = rootG2.Children.First(c => c.GroupId == 3);
        Assert.Equal("Assembly Line A", childG3.GroupName);
        Assert.Equal(28, childG3.DeviceCount);
    }

    [Fact]
    public async Task CreateGroupAsync_WhenValidInput_AddsGroupAndReturnsDetail()
    {
        // Arrange
        _groupRepo.AddGroup(new Group { GroupId = 2, GroupName = "Operations" });

        var request = new CreateGroupRequest(
            GroupName: "Quality Control Testing",
            ParentGroupId: 2,
            Description: "Inspection cameras and diagnostic sensors"
        );

        // Act
        var result = await _groupService.CreateGroupAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Quality Control Testing", result.GroupName);
        Assert.Equal(2, result.ParentGroupId);
        Assert.Equal("Inspection cameras and diagnostic sensors", result.Description);
    }

    [Fact]
    public async Task CreateGroupAsync_WhenParentNotFound_ThrowsInvalidParentGroupException()
    {
        // Arrange
        var request = new CreateGroupRequest(
            GroupName: "Subgroup",
            ParentGroupId: 999,
            Description: null
        );

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidParentGroupException>(() => _groupService.CreateGroupAsync(request));
        Assert.Equal("INVALID_PARENT_GROUP", ex.ErrorCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task CreateGroupAsync_WhenNameEmpty_ThrowsGroupValidationException(string? invalidName)
    {
        // Arrange
        var request = new CreateGroupRequest(
            GroupName: invalidName!,
            ParentGroupId: null,
            Description: null
        );

        // Act & Assert
        await Assert.ThrowsAsync<GroupValidationException>(() => _groupService.CreateGroupAsync(request));
    }

    [Fact]
    public async Task UpdateGroupAsync_WhenRootGroupReparented_ThrowsProtectedGroupException()
    {
        // Arrange
        _groupRepo.AddGroup(new Group { GroupId = 1, GroupName = "Unassigned Group", ParentGroupId = null });
        _groupRepo.AddGroup(new Group { GroupId = 2, GroupName = "Operations", ParentGroupId = null });

        var request = new UpdateGroupRequest(
            GroupName: "Unassigned Group",
            ParentGroupId: 2,
            Description: null
        );

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ProtectedGroupException>(() => _groupService.UpdateGroupAsync(1, request));
        Assert.Equal("CANNOT_MODIFY_ROOT_GROUP", ex.ErrorCode);
    }

    [Fact]
    public async Task UpdateGroupAsync_WhenParentIsSelf_ThrowsCircularGroupHierarchyException()
    {
        // Arrange
        _groupRepo.AddGroup(new Group { GroupId = 2, GroupName = "Operations", ParentGroupId = null });

        var request = new UpdateGroupRequest(
            GroupName: "Operations",
            ParentGroupId: 2, // Self-parenting
            Description: null
        );

        // Act & Assert
        var ex = await Assert.ThrowsAsync<CircularGroupHierarchyException>(() => _groupService.UpdateGroupAsync(2, request));
        Assert.Equal("CIRCULAR_HIERARCHY", ex.ErrorCode);
    }

    [Fact]
    public async Task UpdateGroupAsync_WhenCycleDetected_ThrowsCircularGroupHierarchyException()
    {
        // Arrange: 2 is parent of 3; try to make 3 parent of 2
        var g2 = new Group { GroupId = 2, GroupName = "Operations", ParentGroupId = null };
        var g3 = new Group { GroupId = 3, GroupName = "Assembly Line A", ParentGroupId = 2 };

        _groupRepo.AddGroup(g2);
        _groupRepo.AddGroup(g3);

        var request = new UpdateGroupRequest(
            GroupName: "Operations",
            ParentGroupId: 3, // Cycle!
            Description: null
        );

        // Act & Assert
        var ex = await Assert.ThrowsAsync<CircularGroupHierarchyException>(() => _groupService.UpdateGroupAsync(2, request));
        Assert.Equal("CIRCULAR_HIERARCHY", ex.ErrorCode);
    }

    [Fact]
    public async Task DeleteGroupAsync_WhenRootGroup1_ThrowsProtectedGroupException()
    {
        // Arrange
        _groupRepo.AddGroup(new Group { GroupId = 1, GroupName = "Unassigned Group" });

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ProtectedGroupException>(() => _groupService.DeleteGroupAsync(1));
        Assert.Equal("CANNOT_MODIFY_ROOT_GROUP", ex.ErrorCode);
    }

    [Fact]
    public async Task DeleteGroupAsync_WhenGroupNotFound_ThrowsGroupNotFoundException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<GroupNotFoundException>(() => _groupService.DeleteGroupAsync(999));
    }

    [Fact]
    public async Task DeleteGroupAsync_WhenValid_ReassignsDevicesAndDeletesGroup()
    {
        // Arrange
        var g5 = new Group { GroupId = 5, GroupName = "To Delete Group", ParentGroupId = null };
        _groupRepo.AddGroup(g5);
        _groupRepo.SetDeviceCount(5, 8);

        // Act
        var result = await _groupService.DeleteGroupAsync(5);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(5, result.DeletedGroupId);
        Assert.Equal(8, result.ReassignedDevicesCount);
        Assert.Equal(1, result.TargetGroupId);
        Assert.Equal("Unassigned Group", result.TargetGroupName);

        Assert.False(await _groupRepo.ExistsAsync(5));
    }
}

/// <summary>
/// In-memory stub of IGroupRepository for fast deterministic unit tests.
/// </summary>
internal class StubGroupRepository : IGroupRepository
{
    private readonly List<Group> _groups = new();
    private readonly Dictionary<int, int> _deviceCounts = new();
    private int _idSequence = 10;

    public void AddGroup(Group group)
    {
        if (group.GroupId == 0)
        {
            group.GroupId = ++_idSequence;
        }
        _groups.Add(group);
    }

    public void SetDeviceCount(int groupId, int count) => _deviceCounts[groupId] = count;

    public Task<IReadOnlyList<Group>> GetAllAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<Group>>(_groups.ToList());
    }

    public Task<Group?> GetByIdAsync(int groupId, CancellationToken ct = default)
    {
        return Task.FromResult(_groups.FirstOrDefault(g => g.GroupId == groupId));
    }

    public Task<bool> ExistsAsync(int groupId, CancellationToken ct = default)
    {
        return Task.FromResult(_groups.Any(g => g.GroupId == groupId));
    }

    public Task<int> GetDeviceCountAsync(int groupId, CancellationToken ct = default)
    {
        return Task.FromResult(_deviceCounts.TryGetValue(groupId, out var c) ? c : 0);
    }

    public Task<Dictionary<int, int>> GetDeviceCountsByGroupAsync(CancellationToken ct = default)
    {
        return Task.FromResult(new Dictionary<int, int>(_deviceCounts));
    }

    public Task AddAsync(Group group, CancellationToken ct = default)
    {
        AddGroup(group);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Group group, CancellationToken ct = default)
    {
        var existing = _groups.FirstOrDefault(g => g.GroupId == group.GroupId);
        if (existing != null)
        {
            _groups.Remove(existing);
        }
        _groups.Add(group);
        return Task.CompletedTask;
    }

    public Task<int> DeleteAndReassignAsync(int groupIdToDelete, int targetGroupId, CancellationToken ct = default)
    {
        var group = _groups.FirstOrDefault(g => g.GroupId == groupIdToDelete);
        if (group == null)
        {
            return Task.FromResult(0);
        }

        // Reparent child subgroups
        foreach (var child in _groups.Where(g => g.ParentGroupId == groupIdToDelete))
        {
            child.ParentGroupId = group.ParentGroupId;
        }

        _groups.Remove(group);
        var count = _deviceCounts.TryGetValue(groupIdToDelete, out var c) ? c : 0;
        _deviceCounts.Remove(groupIdToDelete);
        return Task.FromResult(count);
    }
}
