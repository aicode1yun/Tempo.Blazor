using FluentAssertions;
using Tempo.Blazor.Abstractions.Models;
using Xunit;

namespace Tempo.Blazor.Tests.Models;

public class GanttHelperTests
{
    [Fact]
    public void BuildTree_Flat_Data_With_ParentId_Creates_Hierarchy()
    {
        var tasks = new List<TmWorkItem>
        {
            new() { Id = "1", Title = "Project", Start = DateTime.Today, End = DateTime.Today.AddDays(7) },
            new() { Id = "2", Title = "Task A", ParentId = "1", Start = DateTime.Today, End = DateTime.Today.AddDays(3) },
            new() { Id = "3", Title = "Task B", ParentId = "1", Start = DateTime.Today.AddDays(3), End = DateTime.Today.AddDays(7) },
            new() { Id = "4", Title = "Subtask", ParentId = "2", Start = DateTime.Today, End = DateTime.Today.AddDays(1) },
        };

        var tree = GanttHelper.BuildTree(tasks);

        tree.Count.Should().Be(1);
        tree[0].Task.Id.Should().Be("1");
        tree[0].Children.Count.Should().Be(2);
        tree[0].Children[0].Task.Id.Should().Be("2");
        tree[0].Children[0].Children.Count.Should().Be(1);
        tree[0].Children[0].Children[0].Task.Id.Should().Be("4");
        tree[0].Children[0].Children[0].Depth.Should().Be(2);
    }

    [Fact]
    public void FlattenVisible_Respects_Expanded_State()
    {
        var root = new GanttTaskNode(new TmWorkItem { Id = "1", Title = "Root", IsExpanded = true });
        var child = new GanttTaskNode(new TmWorkItem { Id = "2", Title = "Child", IsExpanded = false });
        var grandChild = new GanttTaskNode(new TmWorkItem { Id = "3", Title = "GrandChild" });
        root.Children.Add(child);
        child.Children.Add(grandChild);

        var flat = GanttHelper.FlattenVisible(new[] { root });

        flat.Count.Should().Be(2);
        flat[0].Task.Id.Should().Be("1");
        flat[1].Task.Id.Should().Be("2");
    }

    [Fact]
    public void GetTimeRange_Computes_Min_Max()
    {
        var tasks = new List<TmWorkItem>
        {
            new() { Start = new DateTime(2024, 6, 1), End = new DateTime(2024, 6, 5) },
            new() { Start = new DateTime(2024, 6, 10), End = new DateTime(2024, 6, 15) },
        };

        var (start, end) = GanttHelper.GetTimeRange(tasks);
        start.Should().Be(new DateTime(2024, 6, 1));
        end.Should().Be(new DateTime(2024, 6, 15));
    }

    [Fact]
    public void CalculateBarPosition_Within_Timeline()
    {
        var timelineStart = new DateTime(2024, 6, 1);
        var timelineEnd = new DateTime(2024, 6, 11);
        var taskStart = new DateTime(2024, 6, 3);
        var taskEnd = new DateTime(2024, 6, 6);

        var (left, width) = GanttHelper.CalculateBarPosition(taskStart, taskEnd, timelineStart, timelineEnd, 1000);

        left.Should().BeApproximately(200, 1);
        width.Should().BeApproximately(300, 1);
    }
}
