using System.Text.Json.Serialization;

namespace Tempo.Blazor.NotionEditor.Models;

/// <summary>Canonical logical table definition used by aggregate authoring.</summary>
public sealed class NotionAuthoringTable
{
    /// <summary>Number of logical table columns.</summary>
    [JsonPropertyName("columnCount")]
    public int ColumnCount { get; set; }

    /// <summary>Whether the first logical row is a header row.</summary>
    [JsonPropertyName("hasHeaderRow")]
    public bool HasHeaderRow { get; set; }

    /// <summary>Whether the first logical column is a header column.</summary>
    [JsonPropertyName("hasHeaderColumn")]
    public bool HasHeaderColumn { get; set; }

    /// <summary>Optional horizontal alignment for each column.</summary>
    [JsonPropertyName("columnAlignments")]
    public IReadOnlyList<NotionTableHorizontalAlignment> ColumnAlignments { get; set; } = [];

    /// <summary>Optional preferred width for each column in CSS pixels.</summary>
    [JsonPropertyName("columnWidths")]
    public IReadOnlyList<double?> ColumnWidths { get; set; } = [];
}

/// <summary>One logical row in a canonical authoring table.</summary>
public sealed class NotionAuthoringTableRow
{
    /// <summary>
    /// Logical cells in reading order.
    /// </summary>
    /// <remarks>
    /// Covered positions of merged cells are intentionally absent. Consumers derive the physical
    /// grid from <see cref="NotionAuthoringTableCell.RowSpan"/> and
    /// <see cref="NotionAuthoringTableCell.ColumnSpan"/>.
    /// </remarks>
    [JsonPropertyName("cells")]
    public IReadOnlyList<NotionAuthoringTableCell> Cells { get; set; } = [];
}

/// <summary>A logical rich-text cell in the canonical Notion authoring model.</summary>
public sealed class NotionAuthoringTableCell
{
    /// <summary>Sanitizable rich HTML representation of the cell content.</summary>
    /// <remarks>
    /// This is the canonical representation when <see cref="Inlines"/> is empty. When structured
    /// inlines are present, they are authoritative and this value must represent the same content.
    /// </remarks>
    [JsonPropertyName("html")]
    public string Html { get; set; } = string.Empty;

    /// <summary>Structured rich-text representation of the cell content.</summary>
    /// <remarks>A non-empty list is authoritative over <see cref="Html"/>.</remarks>
    [JsonPropertyName("inlines")]
    public IReadOnlyList<NotionRichTextInline> Inlines { get; set; } = [];

    /// <summary>Optional cell background color.</summary>
    [JsonPropertyName("backgroundColor")]
    public string? BackgroundColor { get; set; }

    /// <summary>Optional default text color.</summary>
    [JsonPropertyName("textColor")]
    public string? TextColor { get; set; }

    /// <summary>Horizontal content alignment.</summary>
    [JsonPropertyName("horizontalAlignment")]
    public NotionTableHorizontalAlignment HorizontalAlignment { get; set; } =
        NotionTableHorizontalAlignment.Left;

    /// <summary>Vertical content alignment.</summary>
    [JsonPropertyName("verticalAlignment")]
    public NotionTableVerticalAlignment VerticalAlignment { get; set; } =
        NotionTableVerticalAlignment.Top;

    /// <summary>Number of logical rows occupied by the cell.</summary>
    [JsonPropertyName("rowSpan")]
    public int RowSpan { get; set; } = 1;

    /// <summary>Number of logical columns occupied by the cell.</summary>
    [JsonPropertyName("columnSpan")]
    public int ColumnSpan { get; set; } = 1;

    /// <summary>Optional preferred cell width in CSS pixels.</summary>
    [JsonPropertyName("width")]
    public double? Width { get; set; }

    /// <summary>Optional per-side cell borders.</summary>
    [JsonPropertyName("borders")]
    public NotionTableCellBorders Borders { get; set; } = new();
}

/// <summary>One structured rich-text segment in a table cell.</summary>
public sealed class NotionRichTextInline
{
    /// <summary>Segment text.</summary>
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    /// <summary>Optional link target.</summary>
    [JsonPropertyName("href")]
    public string? Href { get; set; }

    /// <summary>Whether the segment is bold.</summary>
    [JsonPropertyName("bold")]
    public bool Bold { get; set; }

    /// <summary>Whether the segment is italic.</summary>
    [JsonPropertyName("italic")]
    public bool Italic { get; set; }

    /// <summary>Whether the segment is underlined.</summary>
    [JsonPropertyName("underline")]
    public bool Underline { get; set; }

    /// <summary>Whether the segment is struck through.</summary>
    [JsonPropertyName("strikethrough")]
    public bool Strikethrough { get; set; }

    /// <summary>Whether the segment is inline code.</summary>
    [JsonPropertyName("code")]
    public bool Code { get; set; }

    /// <summary>Optional segment text color.</summary>
    [JsonPropertyName("textColor")]
    public string? TextColor { get; set; }

    /// <summary>Optional segment background color.</summary>
    [JsonPropertyName("backgroundColor")]
    public string? BackgroundColor { get; set; }
}

/// <summary>Per-side borders for one logical table cell.</summary>
public sealed class NotionTableCellBorders
{
    /// <summary>Optional top border.</summary>
    [JsonPropertyName("top")]
    public NotionTableBorder? Top { get; set; }

    /// <summary>Optional right border.</summary>
    [JsonPropertyName("right")]
    public NotionTableBorder? Right { get; set; }

    /// <summary>Optional bottom border.</summary>
    [JsonPropertyName("bottom")]
    public NotionTableBorder? Bottom { get; set; }

    /// <summary>Optional left border.</summary>
    [JsonPropertyName("left")]
    public NotionTableBorder? Left { get; set; }
}

/// <summary>A supported table border.</summary>
public sealed class NotionTableBorder
{
    /// <summary>Border line style.</summary>
    [JsonPropertyName("style")]
    public NotionTableBorderStyle Style { get; set; }

    /// <summary>Optional border color.</summary>
    [JsonPropertyName("color")]
    public string? Color { get; set; }

    /// <summary>Border width in CSS pixels.</summary>
    [JsonPropertyName("width")]
    public double Width { get; set; } = 1;
}

/// <summary>Horizontal alignment supported by logical table cells and columns.</summary>
public enum NotionTableHorizontalAlignment
{
    /// <summary>Align content to the leading edge.</summary>
    Left,

    /// <summary>Center content.</summary>
    Center,

    /// <summary>Align content to the trailing edge.</summary>
    Right
}

/// <summary>Vertical alignment supported by logical table cells.</summary>
public enum NotionTableVerticalAlignment
{
    /// <summary>Align content to the top.</summary>
    Top,

    /// <summary>Center content vertically.</summary>
    Middle,

    /// <summary>Align content to the bottom.</summary>
    Bottom
}

/// <summary>Line styles supported by logical table cell borders.</summary>
public enum NotionTableBorderStyle
{
    /// <summary>No visible line.</summary>
    None,

    /// <summary>Single solid line.</summary>
    Solid,

    /// <summary>Dashed line.</summary>
    Dashed,

    /// <summary>Dotted line.</summary>
    Dotted,

    /// <summary>Double line.</summary>
    Double
}
