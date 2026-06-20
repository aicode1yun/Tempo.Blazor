namespace Tempo.Blazor.Abstractions.PivotTable;

/// <summary>
/// Describes an OLAP dimension exposed by an XMLA data source.
/// </summary>
public sealed class PivotXmlaDimension
{
    /// <summary>Unique identifier of the dimension.</summary>
    public string UniqueName { get; set; } = string.Empty;

    /// <summary>Human-readable caption.</summary>
    public string Caption { get; set; } = string.Empty;

    /// <summary>Hierarchies belonging to this dimension.</summary>
    public List<PivotXmlaHierarchy> Hierarchies { get; set; } = [];
}
