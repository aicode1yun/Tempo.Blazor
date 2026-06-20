namespace Tempo.Blazor.Abstractions.Models;

/// <summary>
/// Supported types for user-defined custom fields on Gantt tasks.
/// </summary>
public enum GanttFieldType
{
    Text,
    Number,
    Date,
    List,
    Checkbox,
    Color,
    Multiselect,
    People,
    Labels
}
