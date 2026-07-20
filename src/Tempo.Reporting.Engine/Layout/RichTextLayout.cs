#pragma warning disable MA0048

using Tempo.Reporting.Abstractions.Definitions;
using Tempo.Reporting.Engine.Fonts;
using Tempo.Reporting.Engine.Snapshot;

namespace Tempo.Reporting.Engine.Layout;

/// <summary>Rich text run consumed by the report text layout engine.</summary>
public sealed record ReportRichTextRun
{
    /// <summary>Creates a rich text run.</summary>
    public ReportRichTextRun(string text, ReportTextStyle? style = null, double letterSpacing = 0)
    {
        Text = text ?? string.Empty;
        Style = style ?? new ReportTextStyle();
        LetterSpacing = letterSpacing;
    }

    /// <summary>Run text.</summary>
    public string Text { get; init; }

    /// <summary>Run text style.</summary>
    public ReportTextStyle Style { get; init; }

    /// <summary>Letter spacing in CSS pixels.</summary>
    public double LetterSpacing { get; init; }

    /// <summary>
    /// Base writing direction for the run. Defaults to <see cref="ReportTextDirection.Auto"/>,
    /// which resolves the paragraph level from the run text.
    /// </summary>
    public ReportTextDirection Direction { get; init; } = ReportTextDirection.Auto;

    /// <summary>Creates a copy with different text.</summary>
    public ReportRichTextRun WithText(string text) => this with { Text = text };

    /// <summary>Converts this run to a measurement request.</summary>
    public TextMeasureRequest ToMeasureRequest(string? text = null)
        => new(
            text ?? Text,
            Style.FontFamily,
            Style.FontSize,
            Style.Bold,
            Style.Italic,
            LetterSpacing);

    /// <summary>Converts this run to a snapshot text command.</summary>
    public ReportSnapshotCommand ToSnapshotCommand(
        string id,
        double x,
        double baseline,
        double width,
        double height)
        => ReportSnapshotCommand.TextRun(
            id,
            Text,
            x,
            baseline,
            width,
            height,
            Style.FontFamily,
            Style.FontSize,
            Style.Color,
            Style.Bold ? "700" : "400",
            Style.Italic ? "italic" : "normal",
            LetterSpacing,
            Style.Underline,
            Style.StrikeThrough,
            Style.BackgroundColor,
            textDirection: Direction);
}

/// <summary>Simple rectangle used by layout results.</summary>
public sealed record ReportLayoutRectangle(double X, double Y, double Width, double Height);

#pragma warning restore MA0048
