namespace Tempo.Blazor.Abstractions.Models;

/// <summary>
/// Defines a column for the multi-column combo box dropdown grid.
/// </summary>
/// <typeparam name="TItem">The type of data item.</typeparam>
public class MultiColumnComboBoxColumn<TItem>
{
    /// <summary>Column header text.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Func that extracts the cell value from a data item.</summary>
    public Func<TItem, object?> Field { get; set; } = _ => null;

    /// <summary>Optional column width CSS value (e.g. "120px", "30%").</summary>
    public string? Width { get; set; }
}
