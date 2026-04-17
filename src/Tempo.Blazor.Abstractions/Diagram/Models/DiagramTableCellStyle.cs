namespace Tempo.Blazor.Components.Diagram.Models;

/// <summary>Visual style overrides for a single table cell.</summary>
public sealed class DiagramTableCellStyle
{
    /// <summary>Background color (CSS color string).</summary>
    public string? BackgroundColor { get; set; }

    /// <summary>Border color (CSS color string).</summary>
    public string? BorderColor { get; set; }

    /// <summary>Text align: left, center, right.</summary>
    public string? TextAlign { get; set; }

    /// <summary>Font weight, e.g. "bold" or "normal".</summary>
    public string? FontWeight { get; set; }
}
