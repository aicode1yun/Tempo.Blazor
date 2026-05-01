namespace Tempo.Blazor.Abstractions.PivotTable;

/// <summary>
/// Configuration settings for connecting to an XMLA (XML for Analysis) data source.
/// </summary>
public sealed class PivotGridXmlaDataProviderSettings
{
    /// <summary>The endpoint URL of the XMLA HTTP service (e.g. https://server/olap/msmdpump.dll).</summary>
    public string ServerUrl { get; set; } = string.Empty;

    /// <summary>The OLAP catalog (database) name.</summary>
    public string Catalog { get; set; } = string.Empty;

    /// <summary>The OLAP cube name within the catalog.</summary>
    public string Cube { get; set; } = string.Empty;
}
