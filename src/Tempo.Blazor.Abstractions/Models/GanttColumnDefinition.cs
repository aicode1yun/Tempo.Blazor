namespace Tempo.Blazor.Abstractions.Models;

/// <summary>Defines the visibility, order and optional width of a Gantt tree-grid column.</summary>
public class GanttColumnDefinition
{
    public GanttColumnKey Key { get; set; }
    public bool Visible { get; set; } = true;
    public double? Width { get; set; }
    public int Order { get; set; }
}
