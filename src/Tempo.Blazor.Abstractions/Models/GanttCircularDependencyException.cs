namespace Tempo.Blazor.Abstractions.Models;

/// <summary>
/// Thrown when a circular dependency is detected in the Gantt task graph.
/// </summary>
public class GanttCircularDependencyException : Exception
{
    public GanttCircularDependencyException()
        : base("Circular dependency detected in Gantt task graph.") { }

    public GanttCircularDependencyException(string message)
        : base(message) { }
}
