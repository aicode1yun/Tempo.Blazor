using FluentAssertions;
using Tempo.Blazor.Abstractions.Models;

namespace Tempo.Blazor.Tests.Models;

public class TreeListHelperTests
{
    private record Employee(int Id, string Name, int? ManagerId);

    [Fact]
    public void BuildTree_FlatData_CreatesHierarchy()
    {
        var data = new List<Employee>
        {
            new(1, "CEO", null),
            new(2, "Alice", 1),
            new(3, "Bob", 1),
            new(4, "Charlie", 2),
            new(5, "Diana", 2),
            new(6, "Eve", 3),
        };

        var tree = TreeListHelper.BuildTree(
            data,
            x => x.Id,
            x => x.ManagerId);

        tree.Select(t => t.Id).Should().Equal(1, 2, 4, 5, 3, 6);
    }

    [Fact]
    public void BuildTree_Indentation_ByLevel()
    {
        var data = new List<Employee>
        {
            new(1, "CEO", null),
            new(2, "Alice", 1),
            new(4, "Charlie", 2),
        };

        var tree = TreeListHelper.BuildTree(data, x => x.Id, x => x.ManagerId);

        tree[0].Level.Should().Be(0);
        tree[1].Level.Should().Be(1);
        tree[2].Level.Should().Be(2);
    }

    [Fact]
    public void BuildTree_ExpandedIds_ShowsChildren()
    {
        var data = new List<Employee>
        {
            new(1, "CEO", null),
            new(2, "Alice", 1),
            new(4, "Charlie", 2),
        };

        var expanded = new HashSet<object> { 1, 2 };
        var tree = TreeListHelper.BuildTree(data, x => x.Id, x => x.ManagerId, expanded);

        tree.Should().AllSatisfy(t => t.IsVisible.Should().BeTrue());
    }

    [Fact]
    public void BuildTree_CollapsedParent_HidesChildren()
    {
        var data = new List<Employee>
        {
            new(1, "CEO", null),
            new(2, "Alice", 1),
            new(4, "Charlie", 2),
        };

        var expanded = new HashSet<object> { 1 }; // 2 is collapsed
        var tree = TreeListHelper.BuildTree(data, x => x.Id, x => x.ManagerId, expanded);

        tree[0].IsVisible.Should().BeTrue();       // CEO
        tree[1].IsVisible.Should().BeTrue();       // Alice
        tree[2].IsVisible.Should().BeFalse();      // Charlie hidden
    }

    [Fact]
    public void BuildTree_OrphanedItems_AsRoots()
    {
        var data = new List<Employee>
        {
            new(1, "CEO", null),
            new(99, "Orphan", 999), // parent 999 does not exist
        };

        var tree = TreeListHelper.BuildTree(data, x => x.Id, x => x.ManagerId);

        tree.Should().HaveCount(2);
        tree[1].Level.Should().Be(0);
        tree[1].ParentId.Should().Be(999);
    }

    [Fact]
    public void BuildTree_HasChildren_Detected()
    {
        var data = new List<Employee>
        {
            new(1, "CEO", null),
            new(2, "Alice", 1),
        };

        var tree = TreeListHelper.BuildTree(data, x => x.Id, x => x.ManagerId);

        tree[0].HasChildren.Should().BeTrue();
        tree[1].HasChildren.Should().BeFalse();
    }
}
