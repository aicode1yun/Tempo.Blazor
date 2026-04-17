namespace Tempo.Blazor.Components.Diagram.Models;

/// <summary>Data for a single cell inside a diagram table node.</summary>
public sealed class DiagramTableCellData
{
    /// <summary>Row index of the cell.</summary>
    public int Row { get; set; }

    /// <summary>Column index of the cell.</summary>
    public int Column { get; set; }

    /// <summary>Number of rows this cell spans.</summary>
    public int RowSpan { get; set; } = 1;

    /// <summary>Number of columns this cell spans.</summary>
    public int ColSpan { get; set; } = 1;

    /// <summary>Text content of the cell.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Visual style overrides for this cell.</summary>
    public DiagramTableCellStyle? Style { get; set; }
}
