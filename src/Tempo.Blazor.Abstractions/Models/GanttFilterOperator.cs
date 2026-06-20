namespace Tempo.Blazor.Abstractions.Models;

/// <summary>
/// Comparison operators available for Gantt task filters.
/// </summary>
public enum GanttFilterOperator
{
    Equals,
    NotEquals,
    Contains,
    Before,
    After,
    IsEmpty,
    IsNotEmpty
}
