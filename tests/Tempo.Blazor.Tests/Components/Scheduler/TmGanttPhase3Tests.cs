using Bunit;
using FluentAssertions;
using Tempo.Blazor.Abstractions.Models;
using Tempo.Blazor.Components.Scheduler;
using Tempo.Blazor.Tests.Localization;
using Xunit;

namespace Tempo.Blazor.Tests.Components.Scheduler;

/// <summary>
/// TDD tests for Phase 3: Advanced Scheduling.
/// Each test is written BEFORE the corresponding implementation (Red → Green → Refactor).
/// </summary>
public class TmGanttPhase3Tests : LocalizationTestBase
{
    // ─── Sample data helpers ────────────────────────────────────────

    private static GanttTask MakeTask(string id, string title, DateTime start, DateTime end,
        string? parentId = null) => new()
    {
        Id = id, Title = title, Start = start, End = end, ParentId = parentId
    };

    private static GanttTask T(string id, int startDay, int endDay) =>
        MakeTask(id, $"Task {id}", new DateTime(2024, 1, startDay), new DateTime(2024, 1, endDay));

    private static GanttDependency Dep(string from, string to,
        GanttDependencyType type = GanttDependencyType.FinishToStart, int lag = 0) =>
        new() { Id = Guid.NewGuid().ToString(), FromId = from, ToId = to, DepType = type, LagDays = lag };

    // ═══════════════════════════════════════════════════════════════
    // F3.1 – Auto-scheduling (GanttScheduler)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void GanttScheduler_Schedule_FS_Dependency_Moves_Successor()
    {
        // predecessor ends Jan 10, successor must start Jan 10 or later (FS)
        var predecessor = T("1", 1, 10);
        var successor   = T("2", 5, 8);   // currently starts before predecessor ends
        var tasks = new List<GanttTask> { predecessor, successor };
        var deps  = new List<GanttDependency> { Dep("1", "2") };

        GanttScheduler.Schedule(tasks, deps);

        successor.Start.Should().Be(new DateTime(2024, 1, 10));
        successor.End.Should().Be(new DateTime(2024, 1, 13)); // preserves duration (3 days)
    }

    [Fact]
    public void GanttScheduler_Schedule_SS_Dependency_Aligns_Starts()
    {
        var predecessor = T("1", 5, 10);
        var successor   = T("2", 1, 4);  // starts earlier
        var tasks = new List<GanttTask> { predecessor, successor };
        var deps  = new List<GanttDependency> { Dep("1", "2", GanttDependencyType.StartToStart) };

        GanttScheduler.Schedule(tasks, deps);

        successor.Start.Should().Be(new DateTime(2024, 1, 5));
    }

    [Fact]
    public void GanttScheduler_Schedule_FF_Dependency_Aligns_Ends()
    {
        var predecessor = T("1", 1, 10);
        var successor   = T("2", 1, 5);  // ends before predecessor
        var tasks = new List<GanttTask> { predecessor, successor };
        var deps  = new List<GanttDependency> { Dep("1", "2", GanttDependencyType.FinishToFinish) };

        GanttScheduler.Schedule(tasks, deps);

        successor.End.Should().Be(new DateTime(2024, 1, 10));
    }

    [Fact]
    public void GanttScheduler_Schedule_SF_Dependency()
    {
        // SF: successor must finish no earlier than predecessor starts
        var predecessor = T("1", 10, 15);
        var successor   = T("2", 1, 5);
        var tasks = new List<GanttTask> { predecessor, successor };
        var deps  = new List<GanttDependency> { Dep("1", "2", GanttDependencyType.StartToFinish) };

        GanttScheduler.Schedule(tasks, deps);

        successor.End.Should().Be(new DateTime(2024, 1, 10));
    }

    [Fact]
    public void GanttScheduler_Schedule_FS_With_LagDays_Adds_Offset()
    {
        var predecessor = T("1", 1, 5);
        var successor   = T("2", 1, 3);
        var tasks = new List<GanttTask> { predecessor, successor };
        var deps  = new List<GanttDependency> { Dep("1", "2", GanttDependencyType.FinishToStart, lag: 3) };

        GanttScheduler.Schedule(tasks, deps);

        successor.Start.Should().Be(new DateTime(2024, 1, 8)); // Jan 5 + 3 lag
    }

    [Fact]
    public void GanttScheduler_Throws_On_Circular_Dependency()
    {
        var t1 = T("1", 1, 5);
        var t2 = T("2", 6, 10);
        var tasks = new List<GanttTask> { t1, t2 };
        var deps  = new List<GanttDependency>
        {
            Dep("1", "2"),
            Dep("2", "1")
        };

        var act = () => GanttScheduler.Schedule(tasks, deps);
        act.Should().Throw<GanttCircularDependencyException>();
    }

    [Fact]
    public void TmGantt_Has_AutoSchedule_Parameter()
    {
        var tasks = new[] { T("1", 1, 5) };
        var cut = RenderComponent<TmGantt>(p => p
            .Add(x => x.Data, tasks)
            .Add(x => x.AutoSchedule, true));

        cut.Instance.AutoSchedule.Should().BeTrue();
    }

    [Fact]
    public void TmGantt_OnAutoScheduled_Fires_When_AutoSchedule_Enabled()
    {
        IReadOnlyList<GanttTask>? firedTasks = null;
        var predecessor = T("1", 1, 5);
        var successor   = T("2", 1, 3);
        var tasks = new[] { predecessor, successor };
        var deps = new[] { Dep("1", "2") };

        var cut = RenderComponent<TmGantt>(p => p
            .Add(x => x.Data, tasks)
            .Add(x => x.Dependencies, deps)
            .Add(x => x.AutoSchedule, true)
            .Add(x => x.OnAutoScheduled, (IReadOnlyList<GanttTask> t) => firedTasks = t));

        firedTasks.Should().NotBeNull();
    }

    // ═══════════════════════════════════════════════════════════════
    // F3.2 – Critical Path
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void CriticalPathCalculator_Calculate_Returns_Critical_Task_Ids()
    {
        // Single chain: 1 → 2 → 3. All on critical path.
        var tasks = new List<GanttTask>
        {
            T("1", 1, 5),
            T("2", 5, 10),
            T("3", 10, 15),
        };
        var deps = new List<GanttDependency> { Dep("1", "2"), Dep("2", "3") };

        var result = CriticalPathCalculator.Calculate(tasks, deps);

        result.Should().Contain("1");
        result.Should().Contain("2");
        result.Should().Contain("3");
    }

    [Fact]
    public void CriticalPathCalculator_Task_With_Zero_Float_Is_Critical()
    {
        // Chain with zero float = critical
        var tasks = new List<GanttTask>
        {
            T("1", 1, 5),
            T("2", 5, 10),
        };
        var deps = new List<GanttDependency> { Dep("1", "2") };

        var result = CriticalPathCalculator.Calculate(tasks, deps);

        result.Should().Contain("1").And.Contain("2");
    }

    [Fact]
    public void CriticalPathCalculator_Parallel_Branches_Only_Longer_Is_Critical()
    {
        // Start → A (5d) → End; Start → B (10d) → End: only B path is critical
        var tasks = new List<GanttTask>
        {
            T("start",  1,  2),   // 1 day
            T("A",      2,  7),   // 5 days
            T("B",      2, 12),   // 10 days
            T("end",   12, 13),   // 1 day
        };
        var deps = new List<GanttDependency>
        {
            Dep("start", "A"),
            Dep("start", "B"),
            Dep("A",     "end"),
            Dep("B",     "end"),
        };

        var result = CriticalPathCalculator.Calculate(tasks, deps);

        result.Should().Contain("B");
        result.Should().Contain("start");
        result.Should().Contain("end");
        result.Should().NotContain("A");
    }

    [Fact]
    public void TmGantt_Has_ShowCriticalPath_Parameter()
    {
        var cut = RenderComponent<TmGantt>(p => p
            .Add(x => x.Data, Array.Empty<GanttTask>())
            .Add(x => x.ShowCriticalPath, true));

        cut.Instance.ShowCriticalPath.Should().BeTrue();
    }

    [Fact]
    public void TmGantt_Critical_Task_Bar_Has_CriticalPath_CSS_Class()
    {
        // Single task with no deps: it IS the only task, so it's on the critical path
        var task = T("1", 1, 5);
        var cut = RenderComponent<TmGantt>(p => p
            .Add(x => x.Data, new[] { task })
            .Add(x => x.ShowCriticalPath, true));

        var bar = cut.Find("[data-testid='task-bar-1']");
        bar.ClassList.Should().Contain("tm-gantt__bar--critical-path");
    }

    // ═══════════════════════════════════════════════════════════════
    // F3.3 – Baseline (Snapshot + Ghost Bars + Deviation)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void GanttBaseline_Class_Has_Required_Properties()
    {
        var baseline = new GanttBaseline
        {
            Id = "b1",
            Name = "Sprint 1",
            CreatedAt = DateTime.Today,
            Tasks = new List<GanttBaselineTask>()
        };

        baseline.Id.Should().Be("b1");
        baseline.Name.Should().Be("Sprint 1");
        baseline.Tasks.Should().NotBeNull();
    }

    [Fact]
    public void GanttBaselineTask_Record_Has_Required_Properties()
    {
        var bt = new GanttBaselineTask("task1", new DateTime(2024, 1, 1), new DateTime(2024, 1, 5));

        bt.TaskId.Should().Be("task1");
        bt.Start.Should().Be(new DateTime(2024, 1, 1));
        bt.End.Should().Be(new DateTime(2024, 1, 5));
    }

    [Fact]
    public void TmGantt_Has_Baselines_And_ActiveBaselineId_Parameters()
    {
        var baseline = new GanttBaseline { Id = "b1", Name = "Baseline 1" };
        var cut = RenderComponent<TmGantt>(p => p
            .Add(x => x.Data, Array.Empty<GanttTask>())
            .Add(x => x.Baselines, new[] { baseline })
            .Add(x => x.ActiveBaselineId, "b1"));

        cut.Instance.Baselines.Should().HaveCount(1);
        cut.Instance.ActiveBaselineId.Should().Be("b1");
    }

    [Fact]
    public void TmGantt_Ghost_Bar_Rendered_When_ActiveBaseline_Set()
    {
        var task = new GanttTask
        {
            Id = "t1", Title = "Task",
            Start = new DateTime(2024, 1, 5), End = new DateTime(2024, 1, 10)
        };
        var baselineTask = new GanttBaselineTask("t1", new DateTime(2024, 1, 1), new DateTime(2024, 1, 8));
        var baseline = new GanttBaseline
        {
            Id = "b1", Name = "B1",
            Tasks = new[] { baselineTask }
        };

        var cut = RenderComponent<TmGantt>(p => p
            .Add(x => x.Data, new[] { task })
            .Add(x => x.Baselines, new[] { baseline })
            .Add(x => x.ActiveBaselineId, "b1"));

        var ghost = cut.FindAll("[data-testid='ghost-bar-t1']");
        ghost.Should().HaveCount(1);
    }

    [Fact]
    public void TmGantt_Deviation_Badge_Rendered_When_ActiveBaseline_Set()
    {
        var task = new GanttTask
        {
            Id = "t1", Title = "Task",
            Start = new DateTime(2024, 1, 5), End = new DateTime(2024, 1, 10)
        };
        var baselineTask = new GanttBaselineTask("t1", new DateTime(2024, 1, 1), new DateTime(2024, 1, 8));
        var baseline = new GanttBaseline
        {
            Id = "b1", Name = "B1",
            Tasks = new[] { baselineTask }
        };

        var cut = RenderComponent<TmGantt>(p => p
            .Add(x => x.Data, new[] { task })
            .Add(x => x.Baselines, new[] { baseline })
            .Add(x => x.ActiveBaselineId, "b1"));

        var badge = cut.FindAll("[data-testid='deviation-badge-t1']");
        badge.Should().HaveCount(1);
    }

    [Fact]
    public void TmGantt_SaveBaseline_Fires_OnBaselineSaved()
    {
        GanttBaseline? savedBaseline = null;
        var task = T("1", 1, 5);
        var cut = RenderComponent<TmGantt>(p => p
            .Add(x => x.Data, new[] { task })
            .Add(x => x.OnBaselineSaved, (GanttBaseline b) => savedBaseline = b));

        cut.Instance.SaveBaselineAsync("My Baseline");

        // Allow async to propagate
        cut.WaitForAssertion(() => savedBaseline.Should().NotBeNull());
        savedBaseline!.Name.Should().Be("My Baseline");
    }

    // ═══════════════════════════════════════════════════════════════
    // F3.4 – Custom Fields
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void GanttFieldType_Enum_Has_All_Values()
    {
        var values = Enum.GetValues<GanttFieldType>();
        values.Should().Contain(GanttFieldType.Text);
        values.Should().Contain(GanttFieldType.Number);
        values.Should().Contain(GanttFieldType.Date);
        values.Should().Contain(GanttFieldType.List);
        values.Should().Contain(GanttFieldType.Checkbox);
        values.Should().Contain(GanttFieldType.Color);
        values.Should().Contain(GanttFieldType.Multiselect);
        values.Should().Contain(GanttFieldType.People);
        values.Should().Contain(GanttFieldType.Labels);
    }

    [Fact]
    public void GanttCustomField_Class_Has_Required_Properties()
    {
        var field = new GanttCustomField
        {
            Id = "f1",
            Name = "Notes",
            Type = GanttFieldType.Text,
            Options = null
        };

        field.Id.Should().Be("f1");
        field.Name.Should().Be("Notes");
        field.Type.Should().Be(GanttFieldType.Text);
    }

    [Fact]
    public void GanttTask_Has_CustomValues_Dictionary()
    {
        var task = new GanttTask();
        task.CustomValues.Should().NotBeNull();
        task.CustomValues["key"] = "value";
        task.CustomValues["key"].Should().Be("value");
    }

    [Fact]
    public void TmGantt_Has_CustomFields_Parameter()
    {
        var field = new GanttCustomField { Id = "f1", Name = "Notes", Type = GanttFieldType.Text };
        var cut = RenderComponent<TmGantt>(p => p
            .Add(x => x.Data, Array.Empty<GanttTask>())
            .Add(x => x.CustomFields, new[] { field }));

        cut.Instance.CustomFields.Should().HaveCount(1);
    }

    [Fact]
    public void TmGantt_CustomField_Text_Renders_In_Header()
    {
        var field = new GanttCustomField { Id = "f1", Name = "Notes", Type = GanttFieldType.Text };
        var cut = RenderComponent<TmGantt>(p => p
            .Add(x => x.Data, Array.Empty<GanttTask>())
            .Add(x => x.CustomFields, new[] { field }));

        cut.Markup.Should().Contain("Notes");
    }

    [Fact]
    public void TmGantt_CustomField_Value_Renders_In_Row()
    {
        var task = new GanttTask
        {
            Id = "t1", Title = "T", Start = new DateTime(2024, 1, 1), End = new DateTime(2024, 1, 5),
            CustomValues = { ["f1"] = "my note" }
        };
        var field = new GanttCustomField { Id = "f1", Name = "Notes", Type = GanttFieldType.Text };

        var cut = RenderComponent<TmGantt>(p => p
            .Add(x => x.Data, new[] { task })
            .Add(x => x.CustomFields, new[] { field }));

        cut.Markup.Should().Contain("my note");
    }

    [Fact]
    public void TmGantt_CustomField_Inline_Edit_Renders_Input()
    {
        var task = new GanttTask
        {
            Id = "t1", Title = "T", Start = new DateTime(2024, 1, 1), End = new DateTime(2024, 1, 5)
        };
        var field = new GanttCustomField { Id = "f1", Name = "Notes", Type = GanttFieldType.Text };

        var cut = RenderComponent<TmGantt>(p => p
            .Add(x => x.Data, new[] { task })
            .Add(x => x.CustomFields, new[] { field }));

        // Start inline edit on custom field cell
        var cell = cut.Find($"[data-testid='custom-cell-f1-t1']");
        cell.DoubleClick();
        cut.Render();

        cut.FindAll("[data-testid='inline-edit-input']").Should().NotBeEmpty();
    }

    [Fact]
    public void TmGantt_OnCustomFieldChanged_Fires_After_Commit()
    {
        (string? taskId, string? fieldId, string? value) fired = default;
        var task = new GanttTask
        {
            Id = "t1", Title = "T", Start = new DateTime(2024, 1, 1), End = new DateTime(2024, 1, 5)
        };
        var field = new GanttCustomField { Id = "f1", Name = "Notes", Type = GanttFieldType.Text };

        var cut = RenderComponent<TmGantt>(p => p
            .Add(x => x.Data, new[] { task })
            .Add(x => x.CustomFields, new[] { field })
            .Add(x => x.OnCustomFieldChanged, ((string tid, string fid, string? v) args) =>
                fired = args));

        var cell = cut.Find($"[data-testid='custom-cell-f1-t1']");
        cell.DoubleClick();
        cut.Render();

        var input = cut.Find("[data-testid='inline-edit-input']");
        input.Input("new value");
        input.KeyDown("Enter");
        cut.Render();

        fired.taskId.Should().Be("t1");
        fired.fieldId.Should().Be("f1");
        fired.value.Should().Be("new value");
    }

    // ═══════════════════════════════════════════════════════════════
    // F3.5 – Filter Panel
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void GanttFilterOperator_Enum_Has_All_Values()
    {
        var values = Enum.GetValues<GanttFilterOperator>();
        values.Should().Contain(GanttFilterOperator.Equals);
        values.Should().Contain(GanttFilterOperator.NotEquals);
        values.Should().Contain(GanttFilterOperator.Contains);
        values.Should().Contain(GanttFilterOperator.Before);
        values.Should().Contain(GanttFilterOperator.After);
        values.Should().Contain(GanttFilterOperator.IsEmpty);
        values.Should().Contain(GanttFilterOperator.IsNotEmpty);
    }

    [Fact]
    public void GanttFilter_Record_Has_Required_Properties()
    {
        var filter = new GanttFilter("Status", GanttFilterOperator.Equals, "Done");

        filter.Field.Should().Be("Status");
        filter.Operator.Should().Be(GanttFilterOperator.Equals);
        filter.Value.Should().Be("Done");
    }

    [Fact]
    public void GanttHelper_ApplyFilters_Status_Equals_Hides_NonMatching()
    {
        var nodes = GanttHelper.BuildTree(new[]
        {
            new GanttTask { Id = "1", Title = "A", Start = DateTime.Today, End = DateTime.Today.AddDays(1), Status = GanttTaskStatus.Done },
            new GanttTask { Id = "2", Title = "B", Start = DateTime.Today, End = DateTime.Today.AddDays(1), Status = GanttTaskStatus.Open },
        }).ToList();

        var filters = new[] { new GanttFilter("Status", GanttFilterOperator.Equals, "Done") };
        var result = GanttHelper.ApplyFilters(nodes, filters);

        result.Should().HaveCount(1);
        result[0].Task.Id.Should().Be("1");
    }

    [Fact]
    public void GanttHelper_ApplyFilters_Parent_Stays_Visible_If_Child_Matches()
    {
        var nodes = GanttHelper.BuildTree(new[]
        {
            new GanttTask { Id = "parent", Title = "Parent", Start = DateTime.Today, End = DateTime.Today.AddDays(5), Status = GanttTaskStatus.Open },
            new GanttTask { Id = "child",  Title = "Child",  Start = DateTime.Today, End = DateTime.Today.AddDays(1), Status = GanttTaskStatus.Done, ParentId = "parent" },
        }).ToList();

        var filters = new[] { new GanttFilter("Status", GanttFilterOperator.Equals, "Done") };
        var result = GanttHelper.ApplyFilters(nodes, filters);

        // parent should stay visible because its child matches
        result.Any(n => n.Task.Id == "parent").Should().BeTrue();
        result.Any(n => n.Task.Id == "child").Should().BeTrue();
    }

    [Fact]
    public void GanttHelper_ApplyFilters_Title_Contains()
    {
        var nodes = GanttHelper.BuildTree(new[]
        {
            new GanttTask { Id = "1", Title = "Deploy to prod",  Start = DateTime.Today, End = DateTime.Today.AddDays(1) },
            new GanttTask { Id = "2", Title = "Write unit tests", Start = DateTime.Today, End = DateTime.Today.AddDays(1) },
        }).ToList();

        var filters = new[] { new GanttFilter("Title", GanttFilterOperator.Contains, "deploy") };
        var result = GanttHelper.ApplyFilters(nodes, filters);

        result.Should().HaveCount(1);
        result[0].Task.Title.Should().Contain("Deploy");
    }

    [Fact]
    public void GanttHelper_ApplyFilters_CustomField_Matches()
    {
        var tasks = new[]
        {
            new GanttTask { Id = "1", Title = "A", Start = DateTime.Today, End = DateTime.Today.AddDays(1), CustomValues = { ["priority"] = "high" } },
            new GanttTask { Id = "2", Title = "B", Start = DateTime.Today, End = DateTime.Today.AddDays(1), CustomValues = { ["priority"] = "low"  } },
        };
        var nodes = GanttHelper.BuildTree(tasks).ToList();
        var filters = new[] { new GanttFilter("custom:priority", GanttFilterOperator.Equals, "high") };

        var result = GanttHelper.ApplyFilters(nodes, filters);

        result.Should().HaveCount(1);
        result[0].Task.Id.Should().Be("1");
    }

    [Fact]
    public void GanttHelper_ApplyFilters_Empty_Filters_Returns_All()
    {
        var nodes = GanttHelper.BuildTree(new[]
        {
            new GanttTask { Id = "1", Title = "A", Start = DateTime.Today, End = DateTime.Today.AddDays(1) },
            new GanttTask { Id = "2", Title = "B", Start = DateTime.Today, End = DateTime.Today.AddDays(1) },
        }).ToList();

        var result = GanttHelper.ApplyFilters(nodes, Array.Empty<GanttFilter>());

        result.Should().HaveCount(2);
    }

    [Fact]
    public void TmGantt_Has_Filters_Parameter()
    {
        var filter = new GanttFilter("Status", GanttFilterOperator.Equals, "Done");
        var cut = RenderComponent<TmGantt>(p => p
            .Add(x => x.Data, Array.Empty<GanttTask>())
            .Add(x => x.Filters, new[] { filter }));

        cut.Instance.Filters.Should().HaveCount(1);
    }

    [Fact]
    public void TmGantt_Filter_Hides_NonMatching_Tasks_In_Tree()
    {
        var tasks = new[]
        {
            new GanttTask { Id = "1", Title = "Alpha", Start = new DateTime(2024,1,1), End = new DateTime(2024,1,5), Status = GanttTaskStatus.Done },
            new GanttTask { Id = "2", Title = "Beta",  Start = new DateTime(2024,1,1), End = new DateTime(2024,1,5), Status = GanttTaskStatus.Open },
        };
        var filter = new GanttFilter("Status", GanttFilterOperator.Equals, "Done");

        var cut = RenderComponent<TmGantt>(p => p
            .Add(x => x.Data, tasks)
            .Add(x => x.Filters, new[] { filter }));

        // Only task 1 (Done) should be visible
        cut.FindAll("[data-testid^='tree-row-']").Should().HaveCount(1);
        cut.Find("[data-testid='tree-row-1']").Should().NotBeNull();
    }

    [Fact]
    public void TmGantt_Renders_Filter_Button_In_Toolbar()
    {
        var cut = RenderComponent<TmGantt>(p => p
            .Add(x => x.Data, Array.Empty<GanttTask>()));

        cut.Find("[data-testid='gantt-filter-btn']").Should().NotBeNull();
    }

    [Fact]
    public void TmGantt_Filter_Panel_Toggles_On_Filter_Button_Click()
    {
        var cut = RenderComponent<TmGantt>(p => p
            .Add(x => x.Data, Array.Empty<GanttTask>()));

        cut.Find("[data-testid='gantt-filter-btn']").Click();
        cut.Render();

        cut.Find("[data-testid='gantt-filter-panel']").Should().NotBeNull();
    }

    [Fact]
    public void TmGantt_OnFiltersChanged_Fires_When_Filter_Applied()
    {
        IReadOnlyList<GanttFilter>? firedFilters = null;
        var cut = RenderComponent<TmGantt>(p => p
            .Add(x => x.Data, Array.Empty<GanttTask>())
            .Add(x => x.OnFiltersChanged,
                (IReadOnlyList<GanttFilter> f) => firedFilters = f));

        cut.Instance.ApplyFiltersAsync(new[] { new GanttFilter("Status", GanttFilterOperator.Equals, "Done") });

        cut.WaitForAssertion(() => firedFilters.Should().NotBeNull());
    }
}
