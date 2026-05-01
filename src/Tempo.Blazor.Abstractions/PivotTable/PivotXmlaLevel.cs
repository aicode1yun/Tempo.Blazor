namespace Tempo.Blazor.Abstractions.PivotTable;

/// <summary>
/// Describes an OLAP level exposed by an XMLA data source.
/// </summary>
public sealed class PivotXmlaLevel
{
    /// <summary>Unique identifier of the level.</summary>
    public string UniqueName { get; set; } = string.Empty;

    /// <summary>Human-readable caption.</summary>
    public string Caption { get; set; } = string.Empty;

    /// <summary>Depth of the level within its hierarchy (0-based).</summary>
    public int Depth { get; set; }
}
