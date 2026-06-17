namespace Tempo.Blazor.EmailTemplates.Abstractions.Model.Blocks;

/// <summary>A single row of an <see cref="EmailTableBlock"/>.</summary>
public sealed class EmailTableRow
{
    /// <summary>Gets or sets whether the row is a header row (rendered with bold cells).</summary>
    public bool IsHeader { get; set; }

    /// <summary>Gets the cells in this row.</summary>
    public IList<EmailTableCell> Cells { get; set; } = new List<EmailTableCell>();
}
