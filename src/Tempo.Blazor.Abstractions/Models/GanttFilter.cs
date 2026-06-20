namespace Tempo.Blazor.Abstractions.Models;

/// <summary>
/// Immutable filter specification for narrowing displayed Gantt tasks.
/// Field names: standard properties (e.g. "Status", "Title") or custom field IDs prefixed with "custom:" (e.g. "custom:priority").
/// </summary>
public record GanttFilter(string Field, GanttFilterOperator Operator, string? Value);
