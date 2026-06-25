#pragma warning disable MA0048

using Tempo.Reporting.Abstractions.Definitions;
using Tempo.Reporting.Engine.Fonts;

namespace Tempo.Reporting.Engine.Layout;

/// <summary>Paragraph layout options.</summary>
public sealed record ReportParagraphLayoutOptions
{
    /// <summary>Left coordinate.</summary>
    public double X { get; init; }

    /// <summary>Top coordinate.</summary>
    public double Y { get; init; }

    /// <summary>Available paragraph width.</summary>
    public double Width { get; init; }

    /// <summary>Horizontal text alignment.</summary>
    public ReportHorizontalAlignment HorizontalAlignment { get; init; } = ReportHorizontalAlignment.Left;

    /// <summary>Line spacing multiplier.</summary>
    public double LineSpacing { get; init; } = 1;

    /// <summary>Spacing before the first line.</summary>
    public double SpacingBefore { get; init; }

    /// <summary>Spacing after the last line.</summary>
    public double SpacingAfter { get; init; }
}

/// <summary>Paragraph layout result.</summary>
public sealed record ReportParagraphLayout
{
    /// <summary>Creates a paragraph layout.</summary>
    public ReportParagraphLayout(IReadOnlyList<ReportTextLine> lines, double totalHeight)
    {
        Lines = lines.ToArray();
        TotalHeight = totalHeight;
    }

    /// <summary>Positioned lines.</summary>
    public IReadOnlyList<ReportTextLine> Lines { get; }

    /// <summary>Total paragraph height including before/after spacing.</summary>
    public double TotalHeight { get; }
}

/// <summary>Positions wrapped text lines inside a paragraph rectangle.</summary>
public static class ReportParagraphLayouter
{
    /// <summary>Lays out rich text runs as a paragraph.</summary>
    public static ReportParagraphLayout Layout(
        IReadOnlyList<ReportRichTextRun> runs,
        ReportParagraphLayoutOptions options,
        ITextMeasurer measurer)
    {
        ArgumentNullException.ThrowIfNull(runs);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(measurer);

        var brokenLines = ReportLineBreaker.BreakLines(runs, options.Width, measurer);
        var positioned = new List<ReportTextLine>(brokenLines.Count);
        var y = options.Y + options.SpacingBefore;
        foreach (var line in brokenLines)
        {
            var lineX = ResolveLineX(line, options);
            var justification = ResolveJustification(line, options);
            var positionedRuns = PositionRuns(line, lineX, justification);
            positioned.Add(line.WithLayout(positionedRuns, lineX, y, y + line.Ascent, justification));
            y += line.LineHeight * Math.Max(0.1, options.LineSpacing);
        }

        return new ReportParagraphLayout(
            positioned,
            options.SpacingBefore + positioned.Sum(line => line.LineHeight * Math.Max(0.1, options.LineSpacing)) + options.SpacingAfter);
    }

    private static double ResolveLineX(ReportTextLine line, ReportParagraphLayoutOptions options)
    {
        return options.HorizontalAlignment switch
        {
            ReportHorizontalAlignment.Center => options.X + Math.Max(0, options.Width - line.Width) / 2,
            ReportHorizontalAlignment.Right => options.X + Math.Max(0, options.Width - line.Width),
            _ => options.X,
        };
    }

    private static double ResolveJustification(ReportTextLine line, ReportParagraphLayoutOptions options)
    {
        if (options.HorizontalAlignment != ReportHorizontalAlignment.Justify ||
            line.BreakKind != ReportLineBreakKind.Soft)
        {
            return 0;
        }

        var spaces = line.Runs.Count(run => run.IsWhitespace);
        return spaces == 0 ? 0 : Math.Max(0, options.Width - line.Width) / spaces;
    }

    private static IReadOnlyList<ReportTextLineRun> PositionRuns(ReportTextLine line, double x, double justificationSpacing)
    {
        var cursor = x;
        var runs = new List<ReportTextLineRun>(line.Runs.Count);
        foreach (var run in line.Runs)
        {
            var width = run.Width + (run.IsWhitespace ? justificationSpacing : 0);
            runs.Add(run.WithLayout(cursor, width));
            cursor += width;
        }

        return runs;
    }
}

#pragma warning restore MA0048
