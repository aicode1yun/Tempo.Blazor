namespace Tempo.Blazor.Abstractions.PivotTable;

/// <summary>
/// Identifies the type of pivot data provider backing the component.
/// </summary>
public enum PivotDataProviderType
{
    /// <summary>In-memory client-side processing (default).</summary>
    Client,

    /// <summary>Custom server-side data provider via <see cref="IPivotDataProvider{TItem}"/>.</summary>
    Server,

    /// <summary>XMLA (XML for Analysis) / OLAP cube provider.</summary>
    Xmla
}
