namespace Tempo.Blazor.Abstractions.WorkItems;

/// <summary>
/// Unified workflow status shared by all task-bearing components
/// (Gantt, Notion tasks, external work items, scheduler).
/// Providers may additionally carry a provider-native label in
/// <see cref="TmWorkItem.StatusLabel"/>.
/// </summary>
public enum TmWorkItemStatus
{
    /// <summary>Not started / to do.</summary>
    Open,

    /// <summary>Work has started.</summary>
    InProgress,

    /// <summary>Completed.</summary>
    Done,

    /// <summary>Closed without completion (cancelled / won't do).</summary>
    Closed
}
