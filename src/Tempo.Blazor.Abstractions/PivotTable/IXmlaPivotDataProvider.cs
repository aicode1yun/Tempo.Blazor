namespace Tempo.Blazor.Abstractions.PivotTable;

/// <summary>
/// Abstract provider for XMLA (XML for Analysis) / OLAP cube data sources.
/// Implementations are responsible for the actual SOAP/HTTP communication
/// with the XMLA endpoint; this interface defines the contract consumed by
/// <see cref="Components.PivotTable.TmPivotTable{TItem}"/>.
/// </summary>
public interface IXmlaPivotDataProvider
{
    /// <summary>
    /// Settings for the XMLA endpoint (URL, catalog, cube).
    /// </summary>
    PivotGridXmlaDataProviderSettings Settings { get; set; }

    /// <summary>
    /// Optional credentials for the XMLA endpoint.
    /// </summary>
    PivotGridXmlaDataProviderCredentials? Credentials { get; set; }

    /// <summary>
    /// Returns the available dimensions (and their hierarchies/levels) from the cube.
    /// </summary>
    Task<IReadOnlyList<PivotXmlaDimension>> GetDimensionsAsync(
        CancellationToken ct = default);

    /// <summary>
    /// Returns the available measures from the cube.
    /// </summary>
    Task<IReadOnlyList<PivotXmlaMeasure>> GetMeasuresAsync(
        CancellationToken ct = default);

    /// <summary>
    /// Executes a pivot query against the XMLA endpoint and returns the result.
    /// </summary>
    Task<PivotTableResult> ExecuteQueryAsync(
        PivotTableConfiguration configuration,
        CancellationToken ct = default);
}
