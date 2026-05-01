namespace Tempo.Blazor.Abstractions.PivotTable;

/// <summary>
/// Server-side (or async) data provider for TmPivotTable.
/// Implement this interface to power pivot transformations from an API or database.
/// </summary>
/// <typeparam name="TItem">The type of the data item.</typeparam>
public interface IPivotDataProvider<TItem>
{
    /// <summary>
    /// Fetches pivot table result applying the given configuration.
    /// </summary>
    Task<PivotTableResult> GetPivotDataAsync(
        PivotQuery<TItem> query,
        CancellationToken ct = default);
}
