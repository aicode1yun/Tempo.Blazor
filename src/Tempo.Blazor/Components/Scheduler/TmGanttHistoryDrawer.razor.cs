using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Abstractions.Models;

namespace Tempo.Blazor.Components.Scheduler;

/// <summary>Side drawer showing the Gantt audit log with time-travel and rollback.</summary>
public partial class TmGanttHistoryDrawer
{
    /// <summary>Audit history entries to display.</summary>
    [Parameter] public IReadOnlyList<GanttHistoryEntry>? History { get; set; }

    /// <summary>Whether the drawer is visible.</summary>
    [Parameter] public bool IsOpen { get; set; }

    /// <summary>Fires when the user requests time-travel to a specific timestamp.</summary>
    [Parameter] public EventCallback<DateTime> OnTimeTravelRequested { get; set; }

    /// <summary>Fires when the user requests rollback to a specific history entry.</summary>
    [Parameter] public EventCallback<GanttHistoryEntry> OnRollbackRequested { get; set; }

    /// <summary>Fires when the drawer should close.</summary>
    [Parameter] public EventCallback OnClose { get; set; }

    private string GetChangeTypeLabel(string changeType) => changeType switch
    {
        "TaskChanged"       => Loc["GanttHistory_TaskChanged"],
        "StatusChanged"     => Loc["GanttHistory_StatusChanged"],
        "PriorityChanged"   => Loc["GanttHistory_PriorityChanged"],
        "DependencyChanged" => Loc["GanttHistory_DependencyChanged"],
        _                   => changeType
    };

    private async Task TimeTravelAsync(DateTime timestamp) =>
        await OnTimeTravelRequested.InvokeAsync(timestamp);

    private async Task RollbackAsync(GanttHistoryEntry entry) =>
        await OnRollbackRequested.InvokeAsync(entry);
}
