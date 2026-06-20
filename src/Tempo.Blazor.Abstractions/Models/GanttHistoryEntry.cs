namespace Tempo.Blazor.Abstractions.Models;

/// <summary>Immutable audit-log entry recording one change to a Gantt task.</summary>
public record GanttHistoryEntry(
    string Id,
    DateTime Timestamp,
    string Author,
    string ChangeType,
    string TaskId,
    string? OldValue,
    string? NewValue);
