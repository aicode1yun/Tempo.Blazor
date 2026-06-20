namespace Tempo.Blazor.Abstractions.PivotTable;

/// <summary>
/// Describes an OLAP hierarchy exposed by an XMLA data source.
/// </summary>
public sealed class PivotXmlaHierarchy
{
    /// <summary>Unique identifier of the hierarchy.</summary>
    public string UniqueName { get; set; } = string.Empty;

    /// <summary>Human-readable caption.</summary>
    public string Caption { get; set; } = string.Empty;

    /// <summary>Levels within this hierarchy.</summary>
    public List<PivotXmlaLevel> Levels { get; set; } = [];
}
