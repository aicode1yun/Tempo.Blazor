using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.Abstractions.Models;
using Tempo.Blazor.Components.Scheduler;
using Tempo.Blazor.Tests.Localization;
using Xunit;

namespace Tempo.Blazor.Tests.Components.Scheduler;

public class TmGanttDragTests : LocalizationTestBase
{
    private static IReadOnlyList<GanttTask> GetSampleTasks() => new List<GanttTask>
    {
        new() { Id = "1", Title = "Project", Start = new DateTime(2024, 6, 1), End = new DateTime(2024, 6, 30), PercentComplete = 0 },
        new() { Id = "2", Title = "Design", ParentId = "1", Start = new DateTime(2024, 6, 1), End = new DateTime(2024, 6, 10), PercentComplete = 100 },
        new() { Id = "3", Title = "Development", ParentId = "1", Start = new DateTime(2024, 6, 11), End = new DateTime(2024, 6, 25), PercentComplete = 50 },
    };

    [Fact]
    public void TmGantt_TreeRow_HasDragHandle()
    {
        var cut = RenderComponent<TmGantt>(p => p
            .Add(c => c.Data, GetSampleTasks()));

        var handles = cut.FindAll(".tm-gantt__drag-handle");
        handles.Count.Should().Be(3, "each visible row should have a drag handle");
    }

    [Fact]
    public void TmGantt_TreeRow_IsDraggable()
    {
        var cut = RenderComponent<TmGantt>(p => p
            .Add(c => c.Data, GetSampleTasks()));

        var row = cut.Find(".tm-gantt__tree-row");
        row.GetAttribute("draggable").Should().Be("true");
    }

    [Fact]
    public void TmGantt_DragStart_SetsDraggingClass()
    {
        var cut = RenderComponent<TmGantt>(p => p
            .Add(c => c.Data, GetSampleTasks()));

        var row = cut.Find(".tm-gantt__tree-row");
        row.TriggerEvent("ondragstart", new DragEventArgs());

        // Re-render to apply class change
        cut.Render();

        var draggingRow = cut.Find(".tm-gantt__tree-row--dragging");
        draggingRow.Should().NotBeNull("dragging row should have dragging class");
    }

    [Fact]
    public void TmGantt_Drop_Invokes_OnTaskReordered()
    {
        GanttTaskReorderedArgs? reordered = null;
        var cut = RenderComponent<TmGantt>(p => p
            .Add(c => c.Data, GetSampleTasks())
            .Add(c => c.OnTaskReordered, args => reordered = args));

        var rows = cut.FindAll(".tm-gantt__tree-row");
        rows.Count.Should().Be(3);

        // Start drag on first row
        rows[0].DragStart();

        // Re-query rows after drag state change triggers re-render
        rows = cut.FindAll(".tm-gantt__tree-row");

        // Drag over second row with offset in middle (Child)
        rows[1].DragOver(new DragEventArgs { OffsetY = 20 });

        // Re-query again before drop
        rows = cut.FindAll(".tm-gantt__tree-row");

        // Drop on second row
        rows[1].Drop();

        reordered.Should().NotBeNull("drop should invoke OnTaskReordered");
        reordered!.TaskId.Should().Be("1");
        reordered.TargetTaskId.Should().Be("2");
        reordered.Position.Should().Be(GanttDropPosition.Child);
    }
}
