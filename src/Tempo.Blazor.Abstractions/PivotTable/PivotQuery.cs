namespace Tempo.Blazor.Abstractions.PivotTable;

/// <summary>
/// Encapsulates all parameters for a pivot table data fetch.
/// </summary>
/// <typeparam name="TItem">The type of the data item.</typeparam>
public sealed class PivotQuery<TItem>
{
    /// <summary>The source data items (when using in-memory mode via provider).</summary>
    public IEnumerable<TItem>? Items { get; init; }

    /// <summary>The complete pivot table configuration.</summary>
    public PivotTableConfiguration Configuration { get; init; } = new();

    /// <summary>Available field definitions for resolving keys.</summary>
    public List<PivotField<TItem>> Fields { get; init; } = [];
}
