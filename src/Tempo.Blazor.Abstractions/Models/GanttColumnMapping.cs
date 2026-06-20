namespace Tempo.Blazor.Abstractions.Models;

/// <summary>Maps a source spreadsheet column header to a Gantt task property.</summary>
public class GanttColumnMapping
{
    public string SourceColumn { get; set; } = string.Empty;
    public GanttColumnKey TargetProperty { get; set; }
}
