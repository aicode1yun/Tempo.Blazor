using System.Globalization;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Abstractions.Models;

namespace Tempo.Blazor.Components.Scheduler;

/// <summary>
/// A Gantt chart component for visualizing tasks, dependencies, and timelines.
/// Supports day/week/month views, zoom, expand/collapse, and task selection.
/// </summary>
public partial class TmGantt
{
    private ElementReference _timelineRef;
    private List<GanttTaskNode> _treeRoots = [];
    private List<GanttTaskNode> _visibleNodes = [];
    private List<TaskBarInfo> _taskBars = [];
    private List<DependencyLineInfo> _visibleDependencies = [];
    private List<TimelineHeader> _timelineHeaders = [];
    private double _totalTimelineWidth;
    private int _zoomLevel = 100;

    private const int RowHeight = 40;
    private const int DayWidth = 40;
    private const int HeaderHeight = 40;

    // ── Parameters ───────────────────────────────────────────────

    /// <summary>List of tasks to display.</summary>
    [Parameter] public IReadOnlyList<GanttTask> Data { get; set; } = [];

    /// <summary>List of dependencies between tasks.</summary>
    [Parameter] public IReadOnlyList<GanttDependency> Dependencies { get; set; } = [];

    /// <summary>Current view mode (Day, Week, Month). Default is Week.</summary>
    [Parameter] public GanttView View { get; set; } = GanttView.Week;

    /// <summary>Fires when the view mode changes.</summary>
    [Parameter] public EventCallback<GanttView> ViewChanged { get; set; }

    /// <summary>Currently selected task.</summary>
    [Parameter] public GanttTask? SelectedTask { get; set; }

    /// <summary>Fires when a task is selected.</summary>
    [Parameter] public EventCallback<GanttTask> OnTaskSelected { get; set; }

    /// <summary>Additional CSS class.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Additional attributes spread onto the root element.</summary>
    [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes { get; set; }

    // ── Lifecycle ────────────────────────────────────────────────

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        _treeRoots = GanttHelper.BuildTree(Data).ToList();
        RefreshVisibleData();
    }

    // ── View & Zoom ──────────────────────────────────────────────

    private async Task SetViewAsync(GanttView view)
    {
        View = view;
        await ViewChanged.InvokeAsync(view);
        RefreshVisibleData();
    }

    private async Task ZoomInAsync()
    {
        _zoomLevel = Math.Min(200, _zoomLevel + 25);
        RefreshVisibleData();
    }

    private async Task ZoomOutAsync()
    {
        _zoomLevel = Math.Max(50, _zoomLevel - 25);
        RefreshVisibleData();
    }

    // ── Interaction ──────────────────────────────────────────────

    private async Task ToggleExpandAsync(GanttTask task)
    {
        task.IsExpanded = !task.IsExpanded;
        RefreshVisibleData();
    }

    private async Task SelectTaskAsync(GanttTask task)
    {
        SelectedTask = task;
        await OnTaskSelected.InvokeAsync(task);
    }

    // ── Rendering helpers ────────────────────────────────────────

    private void RefreshVisibleData()
    {
        _visibleNodes = GanttHelper.FlattenVisible(_treeRoots).ToList();

        var (timelineStart, timelineEnd) = GanttHelper.GetTimeRange(Data);
        var zoomFactor = _zoomLevel / 100.0;
        var pixelPerDay = DayWidth * zoomFactor;
        var totalDays = (timelineEnd - timelineStart).TotalDays;
        _totalTimelineWidth = Math.Max(1, totalDays * pixelPerDay);

        BuildTimelineHeaders(timelineStart, timelineEnd, pixelPerDay);
        BuildTaskBars(timelineStart, timelineEnd, pixelPerDay);
        BuildDependencyLines();
    }

    private void BuildTimelineHeaders(DateTime start, DateTime end, double pixelPerDay)
    {
        _timelineHeaders = [];
        var current = start.Date;
        var offset = 0.0;

        while (current < end)
        {
            var (label, duration) = View switch
            {
                GanttView.Day => (current.ToString("dd.MM"), 1),
                GanttView.Week => ($"W{ISOWeek.GetWeekOfYear(current)} {current:yyyy}", 7),
                GanttView.Month => (current.ToString("MMM yyyy"), DateTime.DaysInMonth(current.Year, current.Month)),
                _ => (current.ToString("dd.MM"), 1)
            };

            var width = duration * pixelPerDay;
            _timelineHeaders.Add(new TimelineHeader(label, offset, width));
            current = current.AddDays(duration);
            offset += width;
        }
    }

    private void BuildTaskBars(DateTime timelineStart, DateTime timelineEnd, double pixelPerDay)
    {
        _taskBars = [];
        for (int i = 0; i < _visibleNodes.Count; i++)
        {
            var node = _visibleNodes[i];
            var (left, width) = GanttHelper.CalculateBarPosition(
                node.Task.Start, node.Task.End, timelineStart, timelineEnd, _totalTimelineWidth);

            _taskBars.Add(new TaskBarInfo(node.Task, left, Math.Max(4, width), i * RowHeight + (RowHeight - 20) / 2));
        }
    }

    private void BuildDependencyLines()
    {
        _visibleDependencies = [];
        var taskIndex = _visibleNodes.Select((n, i) => (n.Task.Id, Index: i)).ToDictionary(x => x.Id, x => x.Index);

        foreach (var dep in Dependencies)
        {
            if (!taskIndex.TryGetValue(dep.FromId, out var fromIdx) || !taskIndex.TryGetValue(dep.ToId, out var toIdx))
                continue;

            var fromBar = _taskBars[fromIdx];
            var toBar = _taskBars[toIdx];

            _visibleDependencies.Add(new DependencyLineInfo(
                fromBar.Left + fromBar.Width, fromBar.Top + 10,
                toBar.Left, toBar.Top + 10));
        }
    }

    // ── CSS helpers ──────────────────────────────────────────────

    private string GetRowClass(GanttTaskNode node) =>
        SelectedTask?.Id == node.Task.Id ? "tm-gantt__tree-row--selected" : "";

    private string GetBarClass(GanttTask task) =>
        SelectedTask?.Id == task.Id ? "tm-gantt__bar--selected" : "";

    private string GetArrowPoints(double x, double y) =>
        $"{x},{y - 4} {x + 6},{y} {x},{y + 4}";
}

/// <summary>View mode for the Gantt timeline.</summary>
public enum GanttView { Day, Week, Month }

internal record TimelineHeader(string Label, double Offset, double Width);
internal record TaskBarInfo(GanttTask Task, double Left, double Width, double Top);
internal record DependencyLineInfo(double X1, double Y1, double X2, double Y2);
