namespace Tempo.Blazor.Components.Diagram.Models;

/// <summary>Text alignment options for stencil sections.</summary>
public enum StencilTextAlign
{
    Left,
    Center,
    Right
}

/// <summary>Visual text style overrides for a stencil section.</summary>
public sealed class DiagramStencilTextStyle
{
    /// <summary>Whether the text is bold.</summary>
    public bool IsBold { get; set; }

    /// <summary>Whether the text is italic.</summary>
    public bool IsItalic { get; set; }

    /// <summary>Horizontal text alignment.</summary>
    public StencilTextAlign TextAlign { get; set; } = StencilTextAlign.Left;

    /// <summary>Text color (CSS color value).</summary>
    public string? Color { get; set; }

    /// <summary>Font size in pixels.</summary>
    public double? FontSize { get; set; }

    /// <summary>Font family.</summary>
    public string? FontFamily { get; set; }

    /// <summary>CSS text-transform value.</summary>
    public string? TextTransform { get; set; }

    /// <summary>CSS letter-spacing value.</summary>
    public string? LetterSpacing { get; set; }
}
