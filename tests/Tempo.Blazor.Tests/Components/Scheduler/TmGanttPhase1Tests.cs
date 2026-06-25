using Bunit;
using FluentAssertions;
using Tempo.Blazor.Abstractions.Models;
using Tempo.Blazor.Components.Scheduler;
using Tempo.Blazor.Tests.Localization;
using Xunit;

namespace Tempo.Blazor.Tests.Components.Scheduler;

/// <summary>
/// TDD tests for Phase 1: Visual Polish.
/// Each test is written BEFORE the corresponding implementation (Red → Green → Refactor).
/// </summary>
public class TmGanttPhase1Tests : LocalizationTestBase
{
    // ─── Sample data ────────────────────────────────────────────────

    private static IReadOnlyList<TmWorkItem> Tasks(params TmWorkItem[] tasks) => tasks;

    private static TmWorkItem Task1 => new()
    {
        Id = "1", Title = "Project",
        Start = new DateTime(2024, 6, 1), End = new DateTime(2024, 6, 30),
        PercentComplete = 0
    };

    private static TmWorkItem Task2 => new()
    {
        Id = "2", Title = "Design", ParentId = "1",
        Start = new DateTime(2024, 6, 1), End = new DateTime(2024, 6, 10),
        PercentComplete = 100
    };

    private static TmWorkItem Task3 => new()
    {
        Id = "3", Title = "Dev", ParentId = "1",
        Start = new DateTime(2024, 6, 11), End = new DateTime(2024, 6, 25),
        PercentComplete = 50
    };

    private static IReadOnlyList<TmWorkItem> SampleTasks() => Tasks(Task1, Task2, Task3);

    // ═══════════════════════════════════════════════════════════════
    // F1.1 – Per-task Color + Progress Darken Overlay
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void GanttTask_Has_Color_Property_DefaultNull()
    {
        var task = new TmWorkItem();
        task.Color.Should().BeNull();
    }

    [Fact]
    public void Bar_Has_CssVariable_For_Custom_Color()
    {
        var task = new TmWorkItem
        {
            Id = "x", Title = "Colored",
            Start = new DateTime(2024, 6, 1), End = new DateTime(2024, 6, 10),
            Color = "#ef4444"
        };

        var cut = RenderComponent<TmGantt>(p => p.Add(c => c.Items, Tasks(task)));

        var bar = cut.Find(".tm-gantt__bar");
        var style = bar.GetAttribute("style") ?? "";
        style.Should().Contain("--tm-gantt-task-color");
        style.Should().Contain("#ef4444");
    }

    [Fact]
    public void Bar_Without_Color_Uses_Default_CssVariable()
    {
        var cut = RenderComponent<TmGantt>(p => p.Add(c => c.Items, SampleTasks()));

        var bar = cut.Find(".tm-gantt__bar");
        var style = bar.GetAttribute("style") ?? "";
        style.Should().Contain("--tm-gantt-task-color");
    }

    [Fact]
    public void Group_Task_Bar_Has_Group_CSS_Class()
    {
        // Task1 is a parent (has children Task2 and Task3)
        var cut = RenderComponent<TmGantt>(p => p.Add(c => c.Items, SampleTasks()));

        var bars = cut.FindAll(".tm-gantt__bar");
        // First bar = Task1 (group)
        bars[0].ClassList.Should().Contain("tm-gantt__bar--group");
    }

    [Fact]
    public void Leaf_Task_Bar_Does_Not_Have_Group_CSS_Class()
    {
        var cut = RenderComponent<TmGantt>(p => p.Add(c => c.Items, SampleTasks()));

        var bars = cut.FindAll(".tm-gantt__bar");
        // Second bar = Task2 (leaf)
        bars[1].ClassList.Should().NotContain("tm-gantt__bar--group");
    }

    // ═══════════════════════════════════════════════════════════════
    // F1.2 – Drop Shadow + Done/Closed Opacity
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Bar_With_Done_Status_Has_Completed_CSS_Class()
    {
        var task = new TmWorkItem
        {
            Id = "1", Title = "Done Task",
            Start = new DateTime(2024, 6, 1), End = new DateTime(2024, 6, 10),
            Status = TmWorkItemStatus.Done
        };

        var cut = RenderComponent<TmGantt>(p => p.Add(c => c.Items, Tasks(task)));

        var bar = cut.Find(".tm-gantt__bar");
        bar.ClassList.Should().Contain("tm-gantt__bar--completed");
    }

    [Fact]
    public void Bar_With_Closed_Status_Has_Completed_CSS_Class()
    {
        var task = new TmWorkItem
        {
            Id = "1", Title = "Closed Task",
            Start = new DateTime(2024, 6, 1), End = new DateTime(2024, 6, 10),
            Status = TmWorkItemStatus.Closed
        };

        var cut = RenderComponent<TmGantt>(p => p.Add(c => c.Items, Tasks(task)));

        var bar = cut.Find(".tm-gantt__bar");
        bar.ClassList.Should().Contain("tm-gantt__bar--completed");
    }

    [Fact]
    public void Bar_With_Open_Status_Does_Not_Have_Completed_Class()
    {
        var task = new TmWorkItem
        {
            Id = "1", Title = "Open Task",
            Start = new DateTime(2024, 6, 1), End = new DateTime(2024, 6, 10),
            Status = TmWorkItemStatus.Open
        };

        var cut = RenderComponent<TmGantt>(p => p.Add(c => c.Items, Tasks(task)));

        cut.Find(".tm-gantt__bar").ClassList.Should().NotContain("tm-gantt__bar--completed");
    }

    // ═══════════════════════════════════════════════════════════════
    // F1.3 – Hover Tooltip + Bar Click
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Bar_Renders_Tooltip_Element()
    {
        var cut = RenderComponent<TmGantt>(p => p.Add(c => c.Items, SampleTasks()));

        var tooltip = cut.Find("[data-testid='bar-tooltip']");
        tooltip.Should().NotBeNull();
    }

    [Fact]
    public void Click_On_Bar_Fires_OnTaskSelected()
    {
        TmWorkItem? selected = null;
        var tasks = SampleTasks();
        var cut = RenderComponent<TmGantt>(p => p
            .Add(c => c.Items, tasks)
            .Add(c => c.OnTaskSelected, t => selected = t));

        cut.Find(".tm-gantt__bar").Click();

        selected.Should().NotBeNull();
        selected!.Id.Should().Be("1");
    }

    // ═══════════════════════════════════════════════════════════════
    // F1.4 – Status Badge + Priority Indicator
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void TmWorkItemStatus_Has_Four_Values()
    {
        var values = Enum.GetValues<TmWorkItemStatus>();
        values.Should().HaveCount(4);
        values.Should().Contain(TmWorkItemStatus.Open);
        values.Should().Contain(TmWorkItemStatus.InProgress);
        values.Should().Contain(TmWorkItemStatus.Done);
        values.Should().Contain(TmWorkItemStatus.Closed);
    }

    [Fact]
    public void TmWorkItemPriority_Has_Five_Values()
    {
        var values = Enum.GetValues<TmWorkItemPriority>();
        values.Should().HaveCount(5);
        values.Should().Contain(TmWorkItemPriority.Highest);
        values.Should().Contain(TmWorkItemPriority.High);
        values.Should().Contain(TmWorkItemPriority.Medium);
        values.Should().Contain(TmWorkItemPriority.Low);
        values.Should().Contain(TmWorkItemPriority.Lowest);
    }

    [Fact]
    public void GanttTask_Status_Default_Is_Open()
    {
        var task = new TmWorkItem();
        task.Status.Should().Be(TmWorkItemStatus.Open);
    }

    [Fact]
    public void GanttTask_Priority_Default_Is_Medium()
    {
        var task = new TmWorkItem();
        task.Priority.Should().Be(TmWorkItemPriority.Medium);
    }

    [Fact]
    public void Tree_Row_Has_Status_Badge_Element()
    {
        var cut = RenderComponent<TmGantt>(p => p.Add(c => c.Items, SampleTasks()));

        var badges = cut.FindAll("[data-testid='status-badge']");
        badges.Count.Should().Be(SampleTasks().Count);
    }

    [Fact]
    public void Tree_Row_Has_Priority_Icon_Element()
    {
        var cut = RenderComponent<TmGantt>(p => p.Add(c => c.Items, SampleTasks()));

        var icons = cut.FindAll("[data-testid='priority-icon']");
        icons.Count.Should().Be(SampleTasks().Count);
    }

    [Fact]
    public void Bar_Has_Priority_CSS_Class()
    {
        var task = new TmWorkItem
        {
            Id = "1", Title = "High Priority",
            Start = new DateTime(2024, 6, 1), End = new DateTime(2024, 6, 10),
            Priority = TmWorkItemPriority.High
        };

        var cut = RenderComponent<TmGantt>(p => p.Add(c => c.Items, Tasks(task)));

        cut.Find(".tm-gantt__bar").ClassList.Should().Contain("tm-gantt__bar--priority-high");
    }

    [Fact]
    public void Bar_Priority_Class_Reflects_Each_Level()
    {
        foreach (var priority in Enum.GetValues<TmWorkItemPriority>())
        {
            var task = new TmWorkItem
            {
                Id = "1", Title = "T",
                Start = new DateTime(2024, 6, 1), End = new DateTime(2024, 6, 10),
                Priority = priority
            };

            var cut = RenderComponent<TmGantt>(p => p.Add(c => c.Items, Tasks(task)));
            var expectedClass = $"tm-gantt__bar--priority-{priority.ToString().ToLowerInvariant()}";
            cut.Find(".tm-gantt__bar").ClassList.Should().Contain(expectedClass,
                $"priority {priority} should add class {expectedClass}");
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // F1.5 – Deadline Marker (Flame Icon)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void GanttTask_Has_Deadline_Property_DefaultNull()
    {
        var task = new TmWorkItem();
        task.DueDate.Should().BeNull();
    }

    [Fact]
    public void Bar_Shows_Deadline_Marker_When_End_Exceeds_Deadline()
    {
        var task = new TmWorkItem
        {
            Id = "1", Title = "Late",
            Start = new DateTime(2024, 6, 1), End = new DateTime(2024, 6, 20),
            DueDate = new DateTime(2024, 6, 10)
        };

        var cut = RenderComponent<TmGantt>(p => p.Add(c => c.Items, Tasks(task)));

        cut.Find("[data-testid='deadline-marker']").Should().NotBeNull();
    }

    [Fact]
    public void Bar_Does_Not_Show_Deadline_Marker_When_On_Time()
    {
        var task = new TmWorkItem
        {
            Id = "1", Title = "On Time",
            Start = new DateTime(2024, 6, 1), End = new DateTime(2024, 6, 10),
            DueDate = new DateTime(2024, 6, 15)
        };

        var cut = RenderComponent<TmGantt>(p => p.Add(c => c.Items, Tasks(task)));

        cut.FindAll("[data-testid='deadline-marker']").Should().BeEmpty();
    }

    [Fact]
    public void Bar_Does_Not_Show_Deadline_Marker_When_Deadline_Null()
    {
        var cut = RenderComponent<TmGantt>(p => p.Add(c => c.Items, SampleTasks()));

        cut.FindAll("[data-testid='deadline-marker']").Should().BeEmpty();
    }

    // ═══════════════════════════════════════════════════════════════
    // F1.6 – Today Marker
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void GanttHelper_GetTodayOffset_Returns_Correct_Offset()
    {
        var start = DateTime.Today.AddDays(-10);
        var pixelPerDay = 40.0;

        var offset = GanttHelper.GetTodayOffset(start, pixelPerDay);

        offset.Should().BeApproximately(400.0, 0.001); // 10 days × 40 px/day
    }

    [Fact]
    public void GanttHelper_GetTodayOffset_Returns_Zero_When_Today_Is_Start()
    {
        var offset = GanttHelper.GetTodayOffset(DateTime.Today, 40.0);
        offset.Should().BeApproximately(0.0, 0.001);
    }

    [Fact]
    public void GanttHelper_GetTodayOffset_Returns_Negative_When_Today_Before_Start()
    {
        var start = DateTime.Today.AddDays(5);
        var offset = GanttHelper.GetTodayOffset(start, 40.0);
        offset.Should().BeNegative();
    }

    [Fact]
    public void Timeline_Renders_Today_Marker_By_Default()
    {
        // Timeline covers today
        var tasks = Tasks(new TmWorkItem
        {
            Id = "1", Title = "T",
            Start = DateTime.Today.AddDays(-5),
            End = DateTime.Today.AddDays(5)
        });

        var cut = RenderComponent<TmGantt>(p => p.Add(c => c.Items, tasks));

        cut.Find("[data-testid='today-marker']").Should().NotBeNull();
    }

    [Fact]
    public void Timeline_Hides_Today_Marker_When_ViewSettings_ShowTodayMarker_False()
    {
        var tasks = Tasks(new TmWorkItem
        {
            Id = "1", Title = "T",
            Start = DateTime.Today.AddDays(-5),
            End = DateTime.Today.AddDays(5)
        });

        var settings = new GanttViewSettings { ShowTodayMarker = false };

        var cut = RenderComponent<TmGantt>(p => p
            .Add(c => c.Items, tasks)
            .Add(c => c.ViewSettings, settings));

        cut.FindAll("[data-testid='today-marker']").Should().BeEmpty();
    }

    // ═══════════════════════════════════════════════════════════════
    // F1.7 – Non-working Days Overlay
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void WorkingSchedule_Default_NonWorkingDays_Are_Weekend()
    {
        var schedule = new WorkingSchedule();
        schedule.NonWorkingDaysOfWeek.Should().Contain(DayOfWeek.Saturday);
        schedule.NonWorkingDaysOfWeek.Should().Contain(DayOfWeek.Sunday);
    }

    [Fact]
    public void WorkingSchedule_Default_Working_Hours_Are_8_To_17()
    {
        var schedule = new WorkingSchedule();
        schedule.WorkDayStartHour.Should().Be(8);
        schedule.WorkDayEndHour.Should().Be(17);
    }

    [Fact]
    public void GanttHelper_GetNonWorkingDayRects_Returns_Weekend_Rects()
    {
        // Monday 2024-06-03 to Sunday 2024-06-09 (1 week)
        var start = new DateTime(2024, 6, 3); // Monday
        var end = new DateTime(2024, 6, 9);   // Sunday (exclusive → covers Sa+Su)
        var schedule = new WorkingSchedule();

        var rects = GanttHelper.GetNonWorkingDayRects(start, end, 40.0, schedule).ToList();

        // Saturday 2024-06-08 = day 5 → offset 200; Sunday 2024-06-09 would be ≥ end, so only Sa
        rects.Should().HaveCount(1); // only Saturday (Sunday == end → excluded)
        rects[0].Offset.Should().BeApproximately(5 * 40.0, 0.001); // 5 days from Monday
    }

    [Fact]
    public void GanttHelper_GetNonWorkingDayRects_Empty_When_No_NonWorking_Days()
    {
        var start = new DateTime(2024, 6, 3); // Monday
        var end = new DateTime(2024, 6, 8);   // Saturday (exclusive → only weekdays)
        var schedule = new WorkingSchedule { NonWorkingDaysOfWeek = [] };

        var rects = GanttHelper.GetNonWorkingDayRects(start, end, 40.0, schedule).ToList();

        rects.Should().BeEmpty();
    }

    [Fact]
    public void GanttHelper_GetNonWorkingDayRects_Includes_Holiday()
    {
        var start = new DateTime(2024, 6, 3); // Monday
        var end = new DateTime(2024, 6, 8);
        var holiday = new DateTime(2024, 6, 5); // Wednesday
        var schedule = new WorkingSchedule
        {
            NonWorkingDaysOfWeek = [],
            Holidays = [holiday]
        };

        var rects = GanttHelper.GetNonWorkingDayRects(start, end, 40.0, schedule).ToList();

        rects.Should().HaveCount(1);
        rects[0].Offset.Should().BeApproximately(2 * 40.0, 0.001); // 2 days from start
    }

    [Fact]
    public void TmGantt_Accepts_WorkingSchedule_Parameter()
    {
        var schedule = new WorkingSchedule();
        var cut = RenderComponent<TmGantt>(p => p
            .Add(c => c.Items, SampleTasks())
            .Add(c => c.WorkingSchedule, schedule));

        cut.Find(".tm-gantt").Should().NotBeNull();
    }

    [Fact]
    public void TmGantt_Renders_NonWorking_Overlay_When_ShowDaysOff_True()
    {
        // Use a task range that includes a weekend
        var tasks = Tasks(new TmWorkItem
        {
            Id = "1", Title = "T",
            Start = new DateTime(2024, 6, 3),  // Monday
            End = new DateTime(2024, 6, 10)    // Monday next week
        });

        var settings = new GanttViewSettings { ShowDaysOff = true };
        var cut = RenderComponent<TmGantt>(p => p
            .Add(c => c.Items, tasks)
            .Add(c => c.ViewSettings, settings));

        var overlays = cut.FindAll("[data-testid='nonworking-overlay']");
        overlays.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void TmGantt_Hides_NonWorking_Overlay_When_ShowDaysOff_False()
    {
        var tasks = Tasks(new TmWorkItem
        {
            Id = "1", Title = "T",
            Start = new DateTime(2024, 6, 3),
            End = new DateTime(2024, 6, 10)
        });

        var settings = new GanttViewSettings { ShowDaysOff = false };
        var cut = RenderComponent<TmGantt>(p => p
            .Add(c => c.Items, tasks)
            .Add(c => c.ViewSettings, settings));

        cut.FindAll("[data-testid='nonworking-overlay']").Should().BeEmpty();
    }

    // ═══════════════════════════════════════════════════════════════
    // F1.8 – Expand / Collapse All
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void GanttHelper_SetAllExpanded_True_Expands_All_Nodes()
    {
        var roots = GanttHelper.BuildTree(SampleTasks());
        // Collapse first
        GanttHelper.SetAllExpanded(roots, false);

        GanttHelper.SetAllExpanded(roots, true);

        var allNodes = GanttHelper.FlattenVisible(roots);
        allNodes.Should().HaveCount(SampleTasks().Count); // all visible
        roots.All(r => r.Task.IsExpanded).Should().BeTrue();
    }

    [Fact]
    public void GanttHelper_SetAllExpanded_False_Collapses_All_Nodes()
    {
        var roots = GanttHelper.BuildTree(SampleTasks());

        GanttHelper.SetAllExpanded(roots, false);

        // After collapse, only root-level nodes visible
        var visible = GanttHelper.FlattenVisible(roots);
        visible.Should().HaveCount(1); // only "Project" (root); children collapsed
    }

    [Fact]
    public void GanttHelper_SetAllExpanded_Works_On_Deep_Nesting()
    {
        var tasks = new List<TmWorkItem>
        {
            new() { Id = "1", Title = "L1", Start = DateTime.Today, End = DateTime.Today.AddDays(1) },
            new() { Id = "2", Title = "L2", ParentId = "1", Start = DateTime.Today, End = DateTime.Today.AddDays(1) },
            new() { Id = "3", Title = "L3", ParentId = "2", Start = DateTime.Today, End = DateTime.Today.AddDays(1) }
        };
        var roots = GanttHelper.BuildTree(tasks);

        GanttHelper.SetAllExpanded(roots, false);
        GanttHelper.FlattenVisible(roots).Should().HaveCount(1);

        GanttHelper.SetAllExpanded(roots, true);
        GanttHelper.FlattenVisible(roots).Should().HaveCount(3);
    }

    [Fact]
    public void Toolbar_Has_ExpandAll_Button()
    {
        var cut = RenderComponent<TmGantt>(p => p.Add(c => c.Items, SampleTasks()));
        cut.Find("[data-testid='gantt-expand-all']").Should().NotBeNull();
    }

    [Fact]
    public void Toolbar_Has_CollapseAll_Button()
    {
        var cut = RenderComponent<TmGantt>(p => p.Add(c => c.Items, SampleTasks()));
        cut.Find("[data-testid='gantt-collapse-all']").Should().NotBeNull();
    }

    [Fact]
    public void CollapseAll_Button_Collapses_Tree()
    {
        var cut = RenderComponent<TmGantt>(p => p.Add(c => c.Items, SampleTasks()));

        cut.Find("[data-testid='gantt-collapse-all']").Click();

        cut.FindAll(".tm-gantt__tree-row").Should().HaveCount(1);
    }

    [Fact]
    public void ExpandAll_Button_Expands_Tree()
    {
        var cut = RenderComponent<TmGantt>(p => p.Add(c => c.Items, SampleTasks()));

        // Collapse first, then expand
        cut.Find("[data-testid='gantt-collapse-all']").Click();
        cut.Find("[data-testid='gantt-expand-all']").Click();

        cut.FindAll(".tm-gantt__tree-row").Should().HaveCount(SampleTasks().Count);
    }

    // ═══════════════════════════════════════════════════════════════
    // F1.9 – View Settings
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void GanttViewSettings_Has_All_Required_Properties()
    {
        var s = new GanttViewSettings();
        s.ShowAvatarsOnChart.Should().BeTrue();
        s.ShowTodayMarker.Should().BeTrue();
        s.ShowDaysOff.Should().BeTrue();
        s.ShowClosedTasks.Should().BeTrue();
        s.TaskNameLocation.Should().Be(GanttTaskNameLocation.InsideBar);
        s.ViewDensity.Should().Be(GanttViewDensity.Comfortable);
        s.ShowAdvancedContextButtons.Should().BeFalse();
        s.Theme.Should().Be(GanttTheme.Auto);
    }

    [Fact]
    public void TmGantt_Accepts_ViewSettings_Parameter()
    {
        var settings = new GanttViewSettings();
        var cut = RenderComponent<TmGantt>(p => p
            .Add(c => c.Items, SampleTasks())
            .Add(c => c.ViewSettings, settings));

        cut.Find(".tm-gantt").Should().NotBeNull();
    }

    [Fact]
    public void ShowClosedTasks_False_Hides_Done_Tasks_From_Tree()
    {
        var tasks = Tasks(
            new TmWorkItem { Id = "1", Title = "Open", Start = new DateTime(2024, 6, 1), End = new DateTime(2024, 6, 5), Status = TmWorkItemStatus.Open },
            new TmWorkItem { Id = "2", Title = "Done", Start = new DateTime(2024, 6, 1), End = new DateTime(2024, 6, 5), Status = TmWorkItemStatus.Done },
            new TmWorkItem { Id = "3", Title = "Closed", Start = new DateTime(2024, 6, 1), End = new DateTime(2024, 6, 5), Status = TmWorkItemStatus.Closed }
        );

        var settings = new GanttViewSettings { ShowClosedTasks = false };
        var cut = RenderComponent<TmGantt>(p => p
            .Add(c => c.Items, tasks)
            .Add(c => c.ViewSettings, settings));

        var rows = cut.FindAll(".tm-gantt__tree-row");
        rows.Should().HaveCount(1); // Only Open task visible
    }

    [Fact]
    public void ShowClosedTasks_True_Shows_All_Tasks()
    {
        var tasks = Tasks(
            new TmWorkItem { Id = "1", Title = "Open", Start = new DateTime(2024, 6, 1), End = new DateTime(2024, 6, 5), Status = TmWorkItemStatus.Open },
            new TmWorkItem { Id = "2", Title = "Done", Start = new DateTime(2024, 6, 1), End = new DateTime(2024, 6, 5), Status = TmWorkItemStatus.Done }
        );

        var settings = new GanttViewSettings { ShowClosedTasks = true };
        var cut = RenderComponent<TmGantt>(p => p
            .Add(c => c.Items, tasks)
            .Add(c => c.ViewSettings, settings));

        cut.FindAll(".tm-gantt__tree-row").Should().HaveCount(2);
    }

    [Fact]
    public void TaskNameLocation_Hidden_Renders_No_Bar_Label()
    {
        var tasks = Tasks(new TmWorkItem
        {
            Id = "1", Title = "Wide Task",
            Start = new DateTime(2024, 6, 1), End = new DateTime(2024, 6, 30)
        });

        var settings = new GanttViewSettings { TaskNameLocation = GanttTaskNameLocation.Hidden };
        var cut = RenderComponent<TmGantt>(p => p
            .Add(c => c.Items, tasks)
            .Add(c => c.ViewSettings, settings));

        cut.FindAll(".tm-gantt__bar-label").Should().BeEmpty();
    }

    [Fact]
    public void TaskNameLocation_InsideBar_Renders_Bar_Label_For_Wide_Bars()
    {
        var tasks = Tasks(new TmWorkItem
        {
            Id = "1", Title = "Wide Task",
            Start = new DateTime(2024, 6, 1), End = new DateTime(2024, 6, 30)
        });

        var settings = new GanttViewSettings { TaskNameLocation = GanttTaskNameLocation.InsideBar };
        var cut = RenderComponent<TmGantt>(p => p
            .Add(c => c.Items, tasks)
            .Add(c => c.ViewSettings, settings));

        cut.FindAll(".tm-gantt__bar-label").Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public void ViewDensity_Compact_Reduces_RowHeight_In_Timeline_Content()
    {
        var tasks = Tasks(
            new TmWorkItem { Id = "1", Title = "A", Start = new DateTime(2024, 6, 1), End = new DateTime(2024, 6, 5) },
            new TmWorkItem { Id = "2", Title = "B", Start = new DateTime(2024, 6, 1), End = new DateTime(2024, 6, 5) }
        );

        var compact = new GanttViewSettings { ViewDensity = GanttViewDensity.Compact };
        var comfortable = new GanttViewSettings { ViewDensity = GanttViewDensity.Comfortable };

        var cutCompact = RenderComponent<TmGantt>(p => p.Add(c => c.Items, tasks).Add(c => c.ViewSettings, compact));
        var cutComfortable = RenderComponent<TmGantt>(p => p.Add(c => c.Items, tasks).Add(c => c.ViewSettings, comfortable));

        var compactStyle = cutCompact.Find(".tm-gantt__timeline-content").GetAttribute("style") ?? "";
        var comfortableStyle = cutComfortable.Find(".tm-gantt__timeline-content").GetAttribute("style") ?? "";

        // Comfortable uses 40px rows, compact uses 28px
        comfortableStyle.Should().Contain("80"); // 2 × 40
        compactStyle.Should().Contain("56");     // 2 × 28
    }

    [Fact]
    public void OnViewSettingsChanged_Fires_After_Settings_Update()
    {
        GanttViewSettings? received = null;
        var settings = new GanttViewSettings();

        var cut = RenderComponent<TmGantt>(p => p
            .Add(c => c.Items, SampleTasks())
            .Add(c => c.ViewSettings, settings)
            .Add(c => c.OnViewSettingsChanged, vs => received = vs));

        // Trigger via the View Settings button toggle
        cut.Find("[data-testid='gantt-view-settings-btn']").Click();
        cut.Find("[data-testid='gantt-view-settings-btn']").Click(); // close

        // Settings button opens dropdown — change ShowTodayMarker via dropdown
        cut.Find("[data-testid='gantt-view-settings-btn']").Click();
        cut.Find("[data-testid='vset-show-today']").Change(false);

        received.Should().NotBeNull();
        received!.ShowTodayMarker.Should().BeFalse();
    }

    [Fact]
    public void GanttTheme_Enum_Has_Three_Values()
    {
        var values = Enum.GetValues<GanttTheme>();
        values.Should().HaveCount(3);
        values.Should().Contain(GanttTheme.Auto);
        values.Should().Contain(GanttTheme.Light);
        values.Should().Contain(GanttTheme.Dark);
    }

    [Fact]
    public void Theme_Dark_Applies_CSS_Class_To_Root()
    {
        var settings = new GanttViewSettings { Theme = GanttTheme.Dark };
        var cut = RenderComponent<TmGantt>(p => p
            .Add(c => c.Items, SampleTasks())
            .Add(c => c.ViewSettings, settings));

        cut.Find(".tm-gantt").ClassList.Should().Contain("tm-gantt--theme-dark");
    }

    [Fact]
    public void Theme_Light_Applies_CSS_Class_To_Root()
    {
        var settings = new GanttViewSettings { Theme = GanttTheme.Light };
        var cut = RenderComponent<TmGantt>(p => p
            .Add(c => c.Items, SampleTasks())
            .Add(c => c.ViewSettings, settings));

        cut.Find(".tm-gantt").ClassList.Should().Contain("tm-gantt--theme-light");
    }

    [Fact]
    public void Theme_Auto_Does_Not_Apply_Explicit_Theme_Class()
    {
        var settings = new GanttViewSettings { Theme = GanttTheme.Auto };
        var cut = RenderComponent<TmGantt>(p => p
            .Add(c => c.Items, SampleTasks())
            .Add(c => c.ViewSettings, settings));

        var classList = cut.Find(".tm-gantt").ClassList;
        classList.Should().NotContain("tm-gantt--theme-dark");
        classList.Should().NotContain("tm-gantt--theme-light");
    }

    [Fact]
    public void Toolbar_Has_ViewSettings_Button()
    {
        var cut = RenderComponent<TmGantt>(p => p.Add(c => c.Items, SampleTasks()));
        cut.Find("[data-testid='gantt-view-settings-btn']").Should().NotBeNull();
    }
}
