namespace Tempo.Blazor.Abstractions.Models;

/// <summary>Type of dependency relationship between two Gantt tasks.</summary>
public enum GanttDependencyType
{
    FinishToStart = 0,
    StartToStart = 1,
    FinishToFinish = 2,
    StartToFinish = 3
}
