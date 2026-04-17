namespace Tempo.Blazor.Components.Diagram.Models;

/// <summary>Swimlane configuration stored on a container node.</summary>
public sealed class DiagramNodeSwimlaneData
{
    /// <summary>True for horizontal lanes (rows), false for vertical lanes (columns).</summary>
    public bool IsHorizontal { get; set; } = true;

    /// <summary>Number of rows in the swimlane grid.</summary>
    public int RowCount { get; set; } = 2;

    /// <summary>Number of columns in the swimlane grid.</summary>
    public int ColumnCount { get; set; } = 1;

    /// <summary>Size of the header area perpendicular to the lane direction (row header width or column header height).</summary>
    public double HeaderSize { get; set; } = 30;

    /// <summary>Height of each row. If empty, rows share equal height.</summary>
    public List<double> RowSizes { get; set; } = [];

    /// <summary>Width of each column. If empty, columns share equal width.</summary>
    public List<double> ColumnSizes { get; set; } = [];

    /// <summary>Labels for each cell indexed by row*ColumnCount+column.</summary>
    public List<string> CellLabels { get; set; } = [];
}
