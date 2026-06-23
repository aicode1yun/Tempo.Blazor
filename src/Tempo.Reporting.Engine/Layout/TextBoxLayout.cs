#pragma warning disable MA0048

using Tempo.Reporting.Abstractions.Definitions;
using Tempo.Reporting.Engine.Fonts;
using Tempo.Reporting.Engine.Snapshot;

namespace Tempo.Reporting.Engine.Layout;

/// <summary>Text box layout request.</summary>
public sealed record ReportTextBoxLayoutRequest
{
    /// <summary>Stable text box identifier.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Left coordinate.</summary>
    public double X { get; init; }

    /// <summary>Top coordinate.</summary>
    public double Y { get; init; }

    /// <summary>Text box width.</summary>
    public double Width { get; init; }

    /// <summary>Text box height.</summary>
    public double Height { get; init; }

    /// <summary>Text box padding.</summary>
    public ReportThickness Padding { get; init; } = new();

    /// <summary>Text box border.</summary>
    public ReportBorder? Border { get; init; }

    /// <summary>Optional background fill.</summary>
    public string? FillColor { get; init; }

    /// <summary>Horizontal text alignment.</summary>
    public ReportHorizontalAlignment HorizontalAlignment { get; init; } = ReportHorizontalAlignment.Left;

    /// <summary>Vertical text alignment.</summary>
    public ReportVerticalAlignment VerticalAlignment { get; init; } = ReportVerticalAlignment.Top;

    /// <summary>Line spacing multiplier.</summary>
    public double LineSpacing { get; init; } = 1;

    /// <summary>Whether the text box can grow vertically to fit text.</summary>
    public bool CanGrow { get; init; }

    /// <summary>Rich text runs.</summary>
    public IReadOnlyList<ReportRichTextRun> Runs { get; init; } = [];
}

/// <summary>Text box layout result.</summary>
public sealed record ReportTextBoxLayout
{
    /// <summary>Creates a text box layout.</summary>
    public ReportTextBoxLayout(
        ReportTextBoxLayoutRequest request,
        ReportLayoutRectangle contentRectangle,
        IReadOnlyList<ReportTextLine> lines,
        double actualHeight,
        bool clipped)
    {
        Request = request;
        ContentRectangle = contentRectangle;
        Lines = lines.ToArray();
        ActualHeight = actualHeight;
        Clipped = clipped;
    }

    /// <summary>Source layout request.</summary>
    public ReportTextBoxLayoutRequest Request { get; }

    /// <summary>Content rectangle after padding.</summary>
    public ReportLayoutRectangle ContentRectangle { get; }

    /// <summary>Visible laid-out lines.</summary>
    public IReadOnlyList<ReportTextLine> Lines { get; }

    /// <summary>Actual text box height.</summary>
    public double ActualHeight { get; }

    /// <summary>Whether text was clipped.</summary>
    public bool Clipped { get; }

    /// <summary>Creates snapshot commands for this text box.</summary>
    public IEnumerable<ReportSnapshotCommand> ToSnapshotCommands()
    {
        if (!string.IsNullOrWhiteSpace(Request.FillColor))
        {
            yield return ReportSnapshotCommand.Rectangle(
                $"{Request.Id}-fill",
                Request.X,
                Request.Y,
                Request.Width,
                ActualHeight,
                Request.FillColor);
        }

        var border = FirstBorderLine(Request.Border);
        if (border is not null)
        {
            yield return ReportSnapshotCommand.Rectangle(
                $"{Request.Id}-border",
                Request.X,
                Request.Y,
                Request.Width,
                ActualHeight,
                string.Empty,
                border.Color,
                border.Width);
        }

        if (Clipped)
        {
            yield return ReportSnapshotCommand.ClipPush(
                $"{Request.Id}-clip",
                ContentRectangle.X,
                ContentRectangle.Y,
                ContentRectangle.Width,
                ContentRectangle.Height);
        }

        foreach (var command in CreateTextCommands())
        {
            yield return command;
        }

        if (Clipped)
        {
            yield return ReportSnapshotCommand.ClipPop($"{Request.Id}-clip-pop");
        }
    }

    private IEnumerable<ReportSnapshotCommand> CreateTextCommands()
    {
        var index = 0;
        foreach (var line in Lines)
        {
            foreach (var run in line.Runs.Where(run => !run.IsWhitespace && run.Text.Length > 0))
            {
                if (!string.IsNullOrWhiteSpace(run.Style.BackgroundColor))
                {
                    yield return ReportSnapshotCommand.Rectangle(
                        $"{Request.Id}-highlight-{index}",
                        run.X,
                        line.Y,
                        run.Width,
                        line.LineHeight,
                        run.Style.BackgroundColor);
                }

                var richRun = new ReportRichTextRun(run.Text, run.Style, run.LetterSpacing);
                yield return richRun.ToSnapshotCommand(
                    $"{Request.Id}-text-{index}",
                    run.X,
                    line.Baseline,
                    run.Width,
                    line.LineHeight);

                if (run.Style.Underline)
                {
                    yield return ReportSnapshotCommand.Line(
                        $"{Request.Id}-underline-{index}",
                        run.X,
                        line.Baseline + Math.Max(1, line.Descent * 0.35),
                        run.Width,
                        0,
                        run.Style.Color,
                        1);
                }

                if (run.Style.StrikeThrough)
                {
                    yield return ReportSnapshotCommand.Line(
                        $"{Request.Id}-strike-{index}",
                        run.X,
                        line.Baseline - line.Ascent * 0.35,
                        run.Width,
                        0,
                        run.Style.Color,
                        1);
                }

                index++;
            }
        }
    }

    private static ReportBorderLine? FirstBorderLine(ReportBorder? border)
        => border?.Top ?? border?.Right ?? border?.Bottom ?? border?.Left;
}

/// <summary>Lays out report text boxes.</summary>
public static class ReportTextBoxLayouter
{
    private const string Ellipsis = "\u2026";

    /// <summary>Lays out a text box.</summary>
    public static ReportTextBoxLayout Layout(ReportTextBoxLayoutRequest request, ITextMeasurer measurer)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(measurer);

        var contentWidth = Math.Max(0, request.Width - request.Padding.Left - request.Padding.Right);
        var initialContentHeight = Math.Max(0, request.Height - request.Padding.Top - request.Padding.Bottom);
        var contentX = request.X + request.Padding.Left;
        var contentY = request.Y + request.Padding.Top;
        var paragraph = ReportParagraphLayouter.Layout(
            request.Runs,
            new ReportParagraphLayoutOptions
            {
                X = contentX,
                Y = contentY,
                Width = contentWidth,
                HorizontalAlignment = request.HorizontalAlignment,
                LineSpacing = request.LineSpacing,
            },
            measurer);

        var requiredHeight = paragraph.TotalHeight + request.Padding.Top + request.Padding.Bottom;
        var actualHeight = request.CanGrow ? Math.Max(request.Height, requiredHeight) : request.Height;
        var contentHeight = Math.Max(0, actualHeight - request.Padding.Top - request.Padding.Bottom);
        var clipped = !request.CanGrow && paragraph.TotalHeight > initialContentHeight + 0.0001;
        var visibleLines = SelectVisibleLines(paragraph.Lines, contentY, contentHeight, clipped);
        if (clipped && visibleLines.Count > 0)
        {
            visibleLines[^1] = EllipsizeLine(visibleLines[^1], contentWidth, measurer);
        }

        var offsetY = ResolveVerticalOffset(request, contentHeight, visibleLines);
        if (Math.Abs(offsetY) > 0.0001)
        {
            visibleLines = visibleLines
                .Select(line => line.WithLayout(line.Runs, line.X, line.Y + offsetY, line.Baseline + offsetY, line.JustificationSpacing))
                .ToList();
        }

        return new ReportTextBoxLayout(
            request,
            new ReportLayoutRectangle(contentX, contentY, contentWidth, contentHeight),
            visibleLines,
            actualHeight,
            clipped);
    }

    private static List<ReportTextLine> SelectVisibleLines(
        IReadOnlyList<ReportTextLine> lines,
        double contentY,
        double contentHeight,
        bool clipped)
    {
        if (!clipped)
        {
            return lines.ToList();
        }

        var bottom = contentY + contentHeight + 0.0001;
        var visible = lines
            .Where(line => line.Y + line.LineHeight <= bottom)
            .ToList();
        if (visible.Count == 0 && lines.Count > 0 && contentHeight > 0)
        {
            visible.Add(lines[0]);
        }

        return visible;
    }

    private static double ResolveVerticalOffset(
        ReportTextBoxLayoutRequest request,
        double contentHeight,
        IReadOnlyList<ReportTextLine> lines)
    {
        if (request.CanGrow || lines.Count == 0)
        {
            return 0;
        }

        var textHeight = lines.Sum(line => line.LineHeight * Math.Max(0.1, request.LineSpacing));
        var extra = Math.Max(0, contentHeight - textHeight);
        return request.VerticalAlignment switch
        {
            ReportVerticalAlignment.Middle => extra / 2,
            ReportVerticalAlignment.Bottom => extra,
            _ => 0,
        };
    }

    private static ReportTextLine EllipsizeLine(
        ReportTextLine line,
        double maxWidth,
        ITextMeasurer measurer)
    {
        var firstRun = line.Runs.FirstOrDefault(run => !run.IsWhitespace) ?? line.Runs.FirstOrDefault();
        if (firstRun is null)
        {
            return line;
        }

        var fullText = string.Concat(line.Runs.Select(run => run.Text)).TrimEnd();
        var style = firstRun.Style;
        var letterSpacing = firstRun.LetterSpacing;
        var text = TrimToFit(fullText, style, letterSpacing, maxWidth, measurer);
        var measurement = measurer.MeasureRun(new ReportRichTextRun(text, style, letterSpacing).ToMeasureRequest());
        var run = new ReportTextLineRun(
            text,
            style,
            line.X,
            measurement.Width,
            measurement.Width,
            measurement.Ascent,
            measurement.Descent,
            measurement.LineHeight,
            letterSpacing,
            isWhitespace: false);
        return new ReportTextLine([run], line.BreakKind, line.X, line.Y, line.Baseline, line.JustificationSpacing);
    }

    private static string TrimToFit(
        string text,
        ReportTextStyle style,
        double letterSpacing,
        double maxWidth,
        ITextMeasurer measurer)
    {
        var ellipsisWidth = measurer.MeasureRun(new ReportRichTextRun(Ellipsis, style, letterSpacing).ToMeasureRequest()).Width;
        if (ellipsisWidth > maxWidth)
        {
            return Ellipsis;
        }

        var candidate = text + Ellipsis;
        while (candidate.Length > Ellipsis.Length)
        {
            var width = measurer.MeasureRun(new ReportRichTextRun(candidate, style, letterSpacing).ToMeasureRequest()).Width;
            if (width <= maxWidth + 0.0001)
            {
                return candidate;
            }

            text = text.Length == 0 ? string.Empty : text[..^1].TrimEnd();
            candidate = text + Ellipsis;
        }

        return Ellipsis;
    }
}

#pragma warning restore MA0048
