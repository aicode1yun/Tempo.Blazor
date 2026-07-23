using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.NotionEditor.Models;

public class TableBlockContent : ITableBlockContent
{
    public bool HasHeaderRow { get; set; }
    public bool HasHeaderColumn { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public bool HasColumnHeader
    {
        get => HasHeaderRow;
        set => HasHeaderRow = value;
    }
    [System.Text.Json.Serialization.JsonIgnore]
    public bool HasRowHeader
    {
        get => HasHeaderColumn;
        set => HasHeaderColumn = value;
    }
    public int ColumnCount { get; set; }

    /// <summary>Per-column horizontal alignment, indexed by column. Empty means no explicit alignment.</summary>
    public IReadOnlyList<TableColumnAlignment> ColumnAlignments { get; set; } = [];

    /// <summary>Optional preferred width for each column in CSS pixels.</summary>
    public IReadOnlyList<double?> ColumnWidths { get; set; } = [];
}
