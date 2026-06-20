namespace Tempo.Blazor.Components.DocumentEditor.Wysiwyg.Model;

/// <summary>Text alignment options.</summary>
public enum TextAlignment
{
    Left,
    Center,
    Right,
    Justify
}

/// <summary>Paragraph-level formatting properties.</summary>
public class ParagraphProperties
{
    /// <summary>Horizontal text alignment.</summary>
    public TextAlignment Alignment { get; set; } = TextAlignment.Left;

    /// <summary>Line spacing multiplier (e.g. 1.15, 2.0).</summary>
    public double LineSpacing { get; set; } = 1.15;

    /// <summary>Space before paragraph (CSS value).</summary>
    public string? SpaceBefore { get; set; }

    /// <summary>Space after paragraph (CSS value).</summary>
    public string? SpaceAfter { get; set; }

    /// <summary>Left indent (CSS value).</summary>
    public string? LeftIndent { get; set; }

    /// <summary>Right indent (CSS value).</summary>
    public string? RightIndent { get; set; }

    /// <summary>First line indent / hanging indent (CSS value).</summary>
    public string? FirstLineIndent { get; set; }
}
