namespace Tempo.Blazor.EmailTemplates.Abstractions.Model.Blocks;

/// <summary>A data table (<c>mj-table</c>). Rows and cells are modelled, not raw HTML.</summary>
public sealed class EmailTableBlock : EmailBlockBase
{
    /// <inheritdoc />
    public override BlockType Type => BlockType.Table;

    /// <summary>Gets the table rows.</summary>
    public List<EmailTableRow> Rows { get; set; } = new();

    /// <summary>Gets or sets the table alignment (<c>align</c>).</summary>
    public string Align { get; set; } = "left";

    /// <summary>Gets or sets the table border shorthand (<c>border</c>).</summary>
    public string? Border { get; set; }

    /// <summary>Gets or sets the cell padding (<c>cellpadding</c>).</summary>
    public string CellPadding { get; set; } = "0";

    /// <summary>Gets or sets the cell spacing (<c>cellspacing</c>).</summary>
    public string CellSpacing { get; set; } = "0";

    /// <summary>Gets or sets the text colour (<c>color</c>).</summary>
    public string Color { get; set; } = "#000000";

    /// <summary>Gets or sets the font family (<c>font-family</c>).</summary>
    public string FontFamily { get; set; } = "Ubuntu, Helvetica, Arial, sans-serif";

    /// <summary>Gets or sets the font size (<c>font-size</c>).</summary>
    public string FontSize { get; set; } = "13px";

    /// <summary>Gets or sets the line height (<c>line-height</c>).</summary>
    public string LineHeight { get; set; } = "22px";

    /// <summary>Gets or sets the table layout algorithm (<c>table-layout</c>).</summary>
    public string TableLayout { get; set; } = "auto";

    /// <summary>Gets or sets the table width (<c>width</c>).</summary>
    public string Width { get; set; } = "100%";

    /// <summary>Initializes a new instance of the <see cref="EmailTableBlock"/> class.</summary>
    public EmailTableBlock() => Padding = "10px 25px";
}

/// <summary>A single row of an <see cref="EmailTableBlock"/>.</summary>
public sealed class EmailTableRow
{
    /// <summary>Gets or sets whether the row is a header row (rendered with bold cells).</summary>
    public bool IsHeader { get; set; }

    /// <summary>Gets the cells in this row.</summary>
    public List<EmailTableCell> Cells { get; set; } = new();
}

/// <summary>A single cell of an <see cref="EmailTableRow"/>.</summary>
public sealed class EmailTableCell
{
    /// <summary>Gets or sets the cell text content.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Gets or sets the cell text alignment (<c>align</c>).</summary>
    public string? Align { get; set; }

    /// <summary>Gets or sets the column span (<c>colspan</c>), when greater than one.</summary>
    public int? ColSpan { get; set; }

    /// <summary>Gets or sets the row span (<c>rowspan</c>), when greater than one.</summary>
    public int? RowSpan { get; set; }
}
