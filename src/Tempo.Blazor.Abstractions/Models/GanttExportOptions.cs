namespace Tempo.Blazor.Abstractions.Models;

/// <summary>Options controlling a Gantt chart export operation.</summary>
public record GanttExportOptions(
    GanttExportFormat Format,
    string? PaperSize = null,
    bool Landscape = false,
    int? ZoomLevel = null,
    bool IncludeCriticalPath = false,
    bool IncludeToday = true,
    bool IncludeWorkload = false,
    IReadOnlyList<GanttColumnKey>? Columns = null);
