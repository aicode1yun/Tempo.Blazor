namespace Tempo.Blazor.Abstractions.Models;

/// <summary>A single project entry in a Gantt portfolio view.</summary>
public class GanttPortfolioProject
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public IReadOnlyList<GanttTask> Tasks { get; set; } = [];
    public IReadOnlyList<GanttDependency> Dependencies { get; set; } = [];
}
