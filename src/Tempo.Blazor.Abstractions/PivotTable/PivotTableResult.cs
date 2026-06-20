namespace Tempo.Blazor.Abstractions.PivotTable;

/// <summary>
/// The computed result of a pivot table transformation.
/// </summary>
public sealed class PivotTableResult
{
    /// <summary>Root nodes of the row dimension tree.</summary>
    public List<PivotRowNode> Rows { get; init; } = [];

    /// <summary>Root nodes of the column dimension tree.</summary>
    public List<PivotColumnNode> Columns { get; init; } = [];

    /// <summary>The 2D matrix of cells. First index = row leaf index, second = column leaf index.</summary>
    public PivotCell[,] Cells { get; init; } = new PivotCell[0, 0];

    /// <summary>Grand total values keyed by value field index.</summary>
    public Dictionary<int, PivotCell> GrandTotals { get; init; } = [];

    /// <summary>Number of value fields (columns per leaf column node).</summary>
    public int ValueFieldCount { get; init; } = 1;

    /// <summary>Configuration used to produce this result.</summary>
    public PivotTableConfiguration Configuration { get; init; } = new();

    /// <summary>Total number of leaf row nodes.</summary>
    public int LeafRowCount { get; init; }

    /// <summary>Total number of leaf column nodes.</summary>
    public int LeafColumnCount { get; init; }
}
