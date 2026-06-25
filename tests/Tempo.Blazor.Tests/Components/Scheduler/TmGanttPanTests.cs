using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.Abstractions.Models;
using Tempo.Blazor.Components.Scheduler;
using Tempo.Blazor.Tests.Localization;
using Xunit;

namespace Tempo.Blazor.Tests.Components.Scheduler;

public class TmGanttPanTests : LocalizationTestBase
{
    private static IReadOnlyList<TmWorkItem> GetSampleTasks() => new List<TmWorkItem>
    {
        new() { Id = "1", Title = "Project", Start = new DateTime(2024, 6, 1), End = new DateTime(2024, 6, 30), PercentComplete = 0 },
        new() { Id = "2", Title = "Design", ParentId = "1", Start = new DateTime(2024, 6, 1), End = new DateTime(2024, 6, 10), PercentComplete = 100 },
        new() { Id = "3", Title = "Development", ParentId = "1", Start = new DateTime(2024, 6, 11), End = new DateTime(2024, 6, 25), PercentComplete = 50 },
        new() { Id = "4", Title = "Launch", IsMilestone = true, Start = new DateTime(2024, 6, 30), End = new DateTime(2024, 6, 30), PercentComplete = 0 },
    };

    /// <summary>
    /// Dragging on the timeline background should invoke JS to update scrollLeft.
    /// </summary>
    [Fact]
    public void TmGantt_Pan_Dragging_Moves_Scroll()
    {
        var cut = RenderComponent<TmGantt>(p => p
            .Add(c => c.Items, GetSampleTasks()));

        var timeline = cut.Find(".tm-gantt__timeline");

        // Mouse down on timeline background starts panning
        timeline.MouseDown(new MouseEventArgs
        {
            ClientX = 100,
            Button = 0
        });

        // Mouse move triggers scrollLeft update via JS interop
        timeline.MouseMove(new MouseEventArgs
        {
            ClientX = 200,
            Button = 0
        });

        // Assert that JS interop was invoked for scrollLeft
        var invocations = JSInterop.Invocations
            .Where(i => i.Identifier is "tmGantt.getScrollLeft" or "tmGantt.setScrollLeft")
            .ToList();

        invocations.Should().NotBeEmpty("panning should invoke JS helpers to read or write scrollLeft");
    }

    /// <summary>
    /// Clicking a task bar should NOT initiate panning — it should select the task instead.
    /// </summary>
    [Fact]
    public void TmGantt_Pan_ClickOnBar_DoesNotPan()
    {
        TmWorkItem? selected = null;
        var cut = RenderComponent<TmGantt>(p => p
            .Add(c => c.Items, GetSampleTasks())
            .Add(c => c.OnTaskSelected, t => selected = t));

        var bar = cut.Find(".tm-gantt__bar");

        // Click the bar to trigger selection
        bar.Click();

        // Assert task was selected
        selected.Should().NotBeNull("clicking a bar should select the task");
        selected!.Id.Should().Be("1"); // Project is first visible bar

        // Assert timeline does not enter panning state after clicking a bar
        var timeline = cut.Find(".tm-gantt__timeline");
        timeline.ClassList.Should().NotContain("tm-gantt__timeline--panning", "timeline should not enter panning mode when a bar is clicked");
    }

    /// <summary>
    /// Timeline gets the --panning modifier class while panning is active.
    /// </summary>
    [Fact]
    public void TmGantt_Pan_Adds_Panning_Class()
    {
        var cut = RenderComponent<TmGantt>(p => p
            .Add(c => c.Items, GetSampleTasks()));

        var timeline = cut.Find(".tm-gantt__timeline");

        timeline.ClassList.Should().NotContain("tm-gantt__timeline--panning", "before mousedown panning class should not be present");

        timeline.MouseDown(new MouseEventArgs
        {
            ClientX = 100,
            Button = 0
        });

        timeline = cut.Find(".tm-gantt__timeline");
        timeline.ClassList.Should().Contain("tm-gantt__timeline--panning", "during panning the modifier class should be present");

        timeline.MouseUp(new MouseEventArgs
        {
            ClientX = 200,
            Button = 0
        });

        timeline = cut.Find(".tm-gantt__timeline");
        timeline.ClassList.Should().NotContain("tm-gantt__timeline--panning", "after mouseup panning class should be removed");
    }
}
