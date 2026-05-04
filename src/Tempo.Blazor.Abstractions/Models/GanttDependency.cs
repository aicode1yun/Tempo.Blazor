namespace Tempo.Blazor.Abstractions.Models;

/// <summary>
/// Describes a dependency relationship between two Gantt tasks.
/// </summary>
public class GanttDependency
{
    /// <summary>Unique identifier of the dependency.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Source task identifier.</summary>
    public string FromId { get; set; } = string.Empty;

    /// <summary>Target task identifier.</summary>
    public string ToId { get; set; } = string.Empty;

    /// <summary>Type of dependency: 0=Finish-Start (default), 1=Start-Start, 2=Finish-Finish, 3=Start-Finish.</summary>
    public int Type { get; set; }
}
