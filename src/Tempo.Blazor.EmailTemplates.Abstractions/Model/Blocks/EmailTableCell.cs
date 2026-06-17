namespace Tempo.Blazor.EmailTemplates.Abstractions.Model.Blocks;

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
