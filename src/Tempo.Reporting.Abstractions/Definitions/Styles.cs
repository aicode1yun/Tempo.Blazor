#pragma warning disable MA0048

namespace Tempo.Reporting.Abstractions.Definitions;

/// <summary>Horizontal alignment for text content.</summary>
public enum ReportHorizontalAlignment
{
    /// <summary>Align content to the left edge.</summary>
    Left,

    /// <summary>Center content horizontally.</summary>
    Center,

    /// <summary>Align content to the right edge.</summary>
    Right,

    /// <summary>Justify content across the available width.</summary>
    Justify,
}

/// <summary>
/// Base writing direction for text content. Controls the Unicode Bidirectional Algorithm
/// paragraph embedding level used when shaping and drawing runs.
/// </summary>
public enum ReportTextDirection
{
    /// <summary>
    /// Auto-detect the base direction from the first strong character (Unicode rules P2/P3).
    /// This is the default and leaves existing left-to-right content unchanged while rendering
    /// Arabic/Hebrew paragraphs right-to-left automatically.
    /// </summary>
    Auto,

    /// <summary>Force a left-to-right base direction (paragraph embedding level 0).</summary>
    Ltr,

    /// <summary>Force a right-to-left base direction (paragraph embedding level 1).</summary>
    Rtl,
}

/// <summary>Vertical alignment for text content.</summary>
public enum ReportVerticalAlignment
{
    /// <summary>Align content to the top edge.</summary>
    Top,

    /// <summary>Center content vertically.</summary>
    Middle,

    /// <summary>Align content to the bottom edge.</summary>
    Bottom,
}

/// <summary>Text style shared by report definitions and snapshot text runs.</summary>
public sealed record ReportTextStyle
{
    /// <summary>Font family name.</summary>
    public string FontFamily { get; init; } = "Inter";

    /// <summary>Font size in points.</summary>
    public double FontSize { get; init; } = 10;

    /// <summary>Whether text uses a bold face.</summary>
    public bool Bold { get; init; }

    /// <summary>Whether text uses an italic face.</summary>
    public bool Italic { get; init; }

    /// <summary>Whether text is underlined.</summary>
    public bool Underline { get; init; }

    /// <summary>Whether text has strike-through decoration.</summary>
    public bool StrikeThrough { get; init; }

    /// <summary>Text color.</summary>
    public string Color { get; init; } = "#111827";

    /// <summary>Optional highlight or background color.</summary>
    public string? BackgroundColor { get; init; }

    /// <summary>Line height multiplier.</summary>
    public double LineHeight { get; init; } = 1.2;
}

/// <summary>Single border side.</summary>
public sealed record ReportBorderLine
{
    /// <summary>Creates a default border line.</summary>
    public ReportBorderLine()
    {
    }

    /// <summary>Creates a border line with color and width.</summary>
    public ReportBorderLine(string color, double width)
    {
        Color = color;
        Width = width;
    }

    /// <summary>Stroke color.</summary>
    public string Color { get; init; } = "#d1d5db";

    /// <summary>Stroke width in page units.</summary>
    public double Width { get; init; } = 1;
}

/// <summary>Four-sided border value.</summary>
public sealed record ReportBorder
{
    /// <summary>Left border side.</summary>
    public ReportBorderLine? Left { get; init; }

    /// <summary>Top border side.</summary>
    public ReportBorderLine? Top { get; init; }

    /// <summary>Right border side.</summary>
    public ReportBorderLine? Right { get; init; }

    /// <summary>Bottom border side.</summary>
    public ReportBorderLine? Bottom { get; init; }

    /// <summary>Creates an equal border on all sides.</summary>
    public static ReportBorder All(string color, double width)
    {
        var line = new ReportBorderLine(color, width);
        return new ReportBorder
        {
            Left = line,
            Top = line,
            Right = line,
            Bottom = line,
        };
    }
}

/// <summary>Reusable named style.</summary>
public sealed record ReportStyleDefinition
{
    /// <summary>Unique style identifier.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Text style.</summary>
    public ReportTextStyle Text { get; init; } = new();

    /// <summary>Optional fill color.</summary>
    public string? FillColor { get; init; }

    /// <summary>Optional border.</summary>
    public ReportBorder? Border { get; init; }

    /// <summary>Optional padding.</summary>
    public ReportThickness? Padding { get; init; }
}

#pragma warning restore MA0048
