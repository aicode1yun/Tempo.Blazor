namespace Tempo.Blazor.Models;

/// <summary>Pin state for a data table column.</summary>
public enum ColumnPin
{
    /// <summary>Not pinned; scrolls with the table body.</summary>
    None,

    /// <summary>Pinned to the left edge (sticky).</summary>
    Left,

    /// <summary>Pinned to the right edge (sticky).</summary>
    Right
}

/// <summary>
/// Per-user, per-table column layout: pixel widths and pin state keyed by column key.
/// Persisted through an <c>IDataTableLayoutStore</c> so the table can restore a user's
/// resized and pinned columns across sessions.
/// </summary>
public sealed class DataTableLayout
{
    /// <summary>Column pixel widths keyed by column key (PropertyName or Title).</summary>
    public Dictionary<string, int> ColumnWidths { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Column pin state keyed by column key. Absent keys default to <see cref="ColumnPin.None"/>.</summary>
    public Dictionary<string, ColumnPin> ColumnPins { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Returns true when no width or pin overrides are stored.</summary>
    public bool IsEmpty => ColumnWidths.Count == 0 && ColumnPins.Count == 0;

    /// <summary>Returns a deep copy of this layout.</summary>
    public DataTableLayout Clone() => new()
    {
        ColumnWidths = new Dictionary<string, int>(ColumnWidths, StringComparer.Ordinal),
        ColumnPins = new Dictionary<string, ColumnPin>(ColumnPins, StringComparer.Ordinal)
    };
}
