using Bunit;
using FluentAssertions;
using Tempo.Blazor.Abstractions.Models;
using Tempo.Blazor.Components.Scheduler;
using Tempo.Blazor.Tests.Localization;
using Xunit;

namespace Tempo.Blazor.Tests.Components.Scheduler;

public class TmGanttAddTaskTests : LocalizationTestBase
{
    private static IReadOnlyList<TmWorkItem> GetSampleTasks() => new List<TmWorkItem>
    {
        new() { Id = "1", Title = "Project", Start = new DateTime(2024, 6, 1), End = new DateTime(2024, 6, 30), PercentComplete = 0 },
        new() { Id = "2", Title = "Design", ParentId = "1", Start = new DateTime(2024, 6, 1), End = new DateTime(2024, 6, 10), PercentComplete = 100 },
    };

    [Fact]
    public void TmGantt_Renders_AddTaskButton()
    {
        var cut = Render<TmGantt>(p => p
            .Add(c => c.Items, GetSampleTasks()));

        var addBtn = cut.Find("button[data-testid='gantt-add-task']");
        addBtn.Should().NotBeNull();
    }

    [Fact]
    public void TmGantt_AddTaskButton_Invokes_OnTaskInserted()
    {
        GanttTaskInsertedArgs? inserted = null;
        var cut = Render<TmGantt>(p => p
            .Add(c => c.Items, GetSampleTasks())
            .Add(c => c.OnTaskInserted, args => inserted = args));

        var addBtn = cut.Find("button[data-testid='gantt-add-task']");
        addBtn.Click();

        inserted.Should().NotBeNull("Add button should invoke OnTaskInserted");
        inserted!.Position.Should().Be(GanttInsertPosition.End);
        inserted.Task.Title.Should().Be("New task");
    }

    [Fact]
    public void TmGantt_ContextMenu_AddTaskAbove_Invokes_OnTaskInserted()
    {
        GanttTaskInsertedArgs? inserted = null;
        var tasks = GetSampleTasks();
        var cut = Render<TmGantt>(p => p
            .Add(c => c.Items, tasks)
            .Add(c => c.OnTaskInserted, args => inserted = args));

        // Right-click on first row to open context menu
        var row = cut.Find(".tm-gantt__tree-row");
        row.ContextMenu();

        var aboveBtn = cut.FindAll(".tm-gantt__context-menu-item")
            .FirstOrDefault(b => b.TextContent.Contains("above", StringComparison.OrdinalIgnoreCase));
        aboveBtn.Should().NotBeNull("Context menu should have 'Add above' option");
        aboveBtn!.Click();

        inserted.Should().NotBeNull();
        inserted!.Position.Should().Be(GanttInsertPosition.Above);
    }

    [Fact]
    public void TmGantt_ContextMenu_DeleteTask_Invokes_OnTaskRemoved()
    {
        string? removedId = null;
        var tasks = GetSampleTasks();
        var cut = Render<TmGantt>(p => p
            .Add(c => c.Items, tasks)
            .Add(c => c.OnTaskRemoved, id => removedId = id));

        var row = cut.Find(".tm-gantt__tree-row");
        row.ContextMenu();

        var deleteBtn = cut.FindAll(".tm-gantt__context-menu-item")
            .FirstOrDefault(b => b.TextContent.Contains("Delete", StringComparison.OrdinalIgnoreCase));
        deleteBtn.Should().NotBeNull("Context menu should have 'Delete' option");
        deleteBtn!.Click();

        removedId.Should().Be("1");
    }
}
