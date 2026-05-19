namespace Tempo.Blazor.Abstractions.Models;

/// <summary>
/// A named snapshot of a Gantt schedule used to compare planned vs. actual.
/// </summary>
public class GanttBaseline
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public IReadOnlyList<GanttBaselineTask> Tasks { get; set; } = [];
}
