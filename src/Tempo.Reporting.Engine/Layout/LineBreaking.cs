#pragma warning disable MA0048

using System.Globalization;
using System.Text;
using Tempo.Reporting.Abstractions.Definitions;
using Tempo.Reporting.Engine.Fonts;

namespace Tempo.Reporting.Engine.Layout;

/// <summary>How a laid-out line ended.</summary>
public enum ReportLineBreakKind
{
    /// <summary>The line wrapped because of width constraints.</summary>
    Soft,

    /// <summary>The line ended at an explicit hard break.</summary>
    Hard,

    /// <summary>The line ended because the input ended.</summary>
    End,
}

/// <summary>Text run positioned inside a laid-out line.</summary>
public sealed record ReportTextLineRun
{
    /// <summary>Creates a text line run.</summary>
    public ReportTextLineRun(
        string text,
        ReportTextStyle style,
        double x,
        double width,
        double naturalWidth,
        double ascent,
        double descent,
        double lineHeight,
        double letterSpacing,
        bool isWhitespace)
    {
        Text = text;
        Style = style;
        X = x;
        Width = width;
        NaturalWidth = naturalWidth;
        Ascent = ascent;
        Descent = descent;
        LineHeight = lineHeight;
        LetterSpacing = letterSpacing;
        IsWhitespace = isWhitespace;
    }

    /// <summary>Run text.</summary>
    public string Text { get; }

    /// <summary>Run style.</summary>
    public ReportTextStyle Style { get; }

    /// <summary>Run x coordinate.</summary>
    public double X { get; init; }

    /// <summary>Run layout width.</summary>
    public double Width { get; init; }

    /// <summary>Measured natural width before justification.</summary>
    public double NaturalWidth { get; }

    /// <summary>Run ascent.</summary>
    public double Ascent { get; }

    /// <summary>Run descent.</summary>
    public double Descent { get; }

    /// <summary>Run line height.</summary>
    public double LineHeight { get; }

    /// <summary>Run letter spacing.</summary>
    public double LetterSpacing { get; }

    /// <summary>Whether this run is whitespace used for positioning only.</summary>
    public bool IsWhitespace { get; }

    /// <summary>Creates a copy with updated layout coordinates.</summary>
    public ReportTextLineRun WithLayout(double x, double width)
        => this with { X = x, Width = width };
}

/// <summary>Single broken or positioned text line.</summary>
public sealed record ReportTextLine
{
    /// <summary>Creates a text line.</summary>
    public ReportTextLine(
        IReadOnlyList<ReportTextLineRun> runs,
        ReportLineBreakKind breakKind,
        double x = 0,
        double y = 0,
        double baseline = 0,
        double justificationSpacing = 0)
    {
        Runs = runs.ToArray();
        BreakKind = breakKind;
        X = x;
        Y = y;
        Baseline = baseline;
        JustificationSpacing = justificationSpacing;
        Text = string.Concat(Runs.Select(run => run.Text));
        Width = Runs.Sum(run => run.Width);
        NaturalWidth = Runs.Sum(run => run.NaturalWidth);
        Ascent = Runs.Count == 0 ? 0 : Runs.Max(run => run.Ascent);
        Descent = Runs.Count == 0 ? 0 : Runs.Max(run => run.Descent);
        LineHeight = Runs.Count == 0 ? 0 : Runs.Max(run => run.LineHeight);
    }

    /// <summary>Line text.</summary>
    public string Text { get; }

    /// <summary>Line runs.</summary>
    public IReadOnlyList<ReportTextLineRun> Runs { get; }

    /// <summary>Line break kind.</summary>
    public ReportLineBreakKind BreakKind { get; }

    /// <summary>Line x coordinate.</summary>
    public double X { get; }

    /// <summary>Line y coordinate.</summary>
    public double Y { get; }

    /// <summary>Line baseline.</summary>
    public double Baseline { get; }

    /// <summary>Line layout width.</summary>
    public double Width { get; }

    /// <summary>Natural width before justification.</summary>
    public double NaturalWidth { get; }

    /// <summary>Maximum ascent.</summary>
    public double Ascent { get; }

    /// <summary>Maximum descent.</summary>
    public double Descent { get; }

    /// <summary>Maximum line height.</summary>
    public double LineHeight { get; }

    /// <summary>Extra spacing inserted after each natural whitespace run by justification.</summary>
    public double JustificationSpacing { get; }

    /// <summary>Creates a copy with updated line placement and runs.</summary>
    public ReportTextLine WithLayout(
        IReadOnlyList<ReportTextLineRun> runs,
        double x,
        double y,
        double baseline,
        double justificationSpacing)
        => new(runs, BreakKind, x, y, baseline, justificationSpacing);
}

/// <summary>Simplified UAX#14-inspired line breaker for report text.</summary>
public static class ReportLineBreaker
{
    /// <summary>Breaks rich text runs into lines.</summary>
    public static IReadOnlyList<ReportTextLine> BreakLines(
        IReadOnlyList<ReportRichTextRun> runs,
        double maxWidth,
        ITextMeasurer measurer)
    {
        ArgumentNullException.ThrowIfNull(runs);
        ArgumentNullException.ThrowIfNull(measurer);

        var segments = BuildSegments(runs, measurer);
        if (segments.Count == 0)
        {
            return [];
        }

        var lines = new List<ReportTextLine>();
        foreach (var segment in segments)
        {
            lines.AddRange(BreakSegment(segment.Tokens, maxWidth, segment.BreakKind));
        }

        return lines;
    }

    private static IReadOnlyList<ReportTextLine> BreakSegment(
        IReadOnlyList<LineToken> tokens,
        double maxWidth,
        ReportLineBreakKind finalBreakKind)
    {
        if (tokens.Count == 0)
        {
            return [CreateLine([], finalBreakKind)];
        }

        var lines = new List<ReportTextLine>();
        var current = new List<LineToken>();
        foreach (var token in tokens)
        {
            if (token.IsWhitespace && current.Count == 0)
            {
                continue;
            }

            while (current.Count > 0 && Width(current) + token.Width > maxWidth)
            {
                var breakIndex = current.FindLastIndex(item => item.CanBreakAfter);
                if (breakIndex < 0)
                {
                    lines.Add(CreateLine(TrimTrailingWhitespace(current), ReportLineBreakKind.Soft));
                    current.Clear();
                    break;
                }

                lines.Add(CreateLine(TrimTrailingWhitespace(current.Take(breakIndex + 1).ToArray()), ReportLineBreakKind.Soft));
                current = TrimLeadingWhitespace(current.Skip(breakIndex + 1).ToList());
            }

            if (token.IsWhitespace && current.Count == 0)
            {
                continue;
            }

            current.Add(token);
        }

        lines.Add(CreateLine(TrimTrailingWhitespace(current), finalBreakKind));
        return lines;
    }

    private static List<LineSegment> BuildSegments(IReadOnlyList<ReportRichTextRun> runs, ITextMeasurer measurer)
    {
        var segments = new List<LineSegment>();
        var current = new List<LineToken>();
        foreach (var run in runs)
        {
            foreach (var token in Tokenize(run, measurer))
            {
                if (token.HardBreak)
                {
                    segments.Add(new LineSegment(current, ReportLineBreakKind.Hard));
                    current = [];
                    continue;
                }

                current.Add(token);
            }
        }

        segments.Add(new LineSegment(current, ReportLineBreakKind.End));
        return segments;
    }

    private static IEnumerable<LineToken> Tokenize(ReportRichTextRun run, ITextMeasurer measurer)
    {
        var text = run.Text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var word = new StringBuilder();

        foreach (var rune in text.EnumerateRunes())
        {
            if (rune.Value == '\n')
            {
                foreach (var token in FlushWord(word, run, measurer, canBreakAfter: false))
                {
                    yield return token;
                }

                yield return LineToken.HardBreakToken;
                continue;
            }

            var value = rune.ToString();
            if (char.IsWhiteSpace(value, 0))
            {
                foreach (var token in FlushWord(word, run, measurer, canBreakAfter: false))
                {
                    yield return token;
                }

                yield return CreateToken(value, run, measurer, canBreakAfter: true, isWhitespace: true);
                continue;
            }

            if (string.Equals(value, "-", StringComparison.Ordinal))
            {
                word.Append(value);
                foreach (var token in FlushWord(word, run, measurer, canBreakAfter: true))
                {
                    yield return token;
                }

                continue;
            }

            if (IsCjk(rune.Value))
            {
                foreach (var token in FlushWord(word, run, measurer, canBreakAfter: false))
                {
                    yield return token;
                }

                yield return CreateToken(value, run, measurer, canBreakAfter: true, isWhitespace: false);
                continue;
            }

            word.Append(value);
        }

        foreach (var token in FlushWord(word, run, measurer, canBreakAfter: false))
        {
            yield return token;
        }
    }

    private static IEnumerable<LineToken> FlushWord(
        StringBuilder word,
        ReportRichTextRun run,
        ITextMeasurer measurer,
        bool canBreakAfter)
    {
        if (word.Length == 0)
        {
            yield break;
        }

        yield return CreateToken(word.ToString(), run, measurer, canBreakAfter, isWhitespace: false);
        word.Clear();
    }

    private static LineToken CreateToken(
        string text,
        ReportRichTextRun run,
        ITextMeasurer measurer,
        bool canBreakAfter,
        bool isWhitespace)
    {
        var measurement = measurer.MeasureRun(run.ToMeasureRequest(text));
        return new LineToken(
            text,
            run.Style,
            run.LetterSpacing,
            measurement.Width,
            measurement.Ascent,
            measurement.Descent,
            measurement.LineHeight,
            canBreakAfter,
            isWhitespace);
    }

    private static ReportTextLine CreateLine(IReadOnlyList<LineToken> tokens, ReportLineBreakKind breakKind)
        => new(tokens.Select(token => token.ToRun()).ToArray(), breakKind);

    private static double Width(IEnumerable<LineToken> tokens) => tokens.Sum(token => token.Width);

    private static List<LineToken> TrimTrailingWhitespace(IEnumerable<LineToken> tokens)
    {
        var result = tokens.ToList();
        while (result.Count > 0 && result[^1].IsWhitespace)
        {
            result.RemoveAt(result.Count - 1);
        }

        return result;
    }

    private static List<LineToken> TrimLeadingWhitespace(List<LineToken> tokens)
    {
        while (tokens.Count > 0 && tokens[0].IsWhitespace)
        {
            tokens.RemoveAt(0);
        }

        return tokens;
    }

    private static bool IsCjk(int codePoint)
        => CharUnicodeInfo.GetUnicodeCategory(char.ConvertFromUtf32(codePoint), 0) == UnicodeCategory.OtherLetter &&
           ((codePoint >= 0x3040 && codePoint <= 0x30ff) ||
            (codePoint >= 0x3400 && codePoint <= 0x9fff));

    private sealed record LineSegment(IReadOnlyList<LineToken> Tokens, ReportLineBreakKind BreakKind);

    private sealed record LineToken(
        string Text,
        ReportTextStyle Style,
        double LetterSpacing,
        double Width,
        double Ascent,
        double Descent,
        double LineHeight,
        bool CanBreakAfter,
        bool IsWhitespace,
        bool HardBreak = false)
    {
        public static LineToken HardBreakToken { get; } = new(
            string.Empty,
            new ReportTextStyle(),
            0,
            0,
            0,
            0,
            0,
            false,
            false,
            true);

        public ReportTextLineRun ToRun()
            => new(Text, Style, 0, Width, Width, Ascent, Descent, LineHeight, LetterSpacing, IsWhitespace);
    }
}

#pragma warning restore MA0048
