using HarfBuzzSharp;
using SkiaSharp;
using SkiaSharp.HarfBuzz;
using Tempo.Reporting.Abstractions.Definitions;
using HbBuffer = HarfBuzzSharp.Buffer;
using HbDirection = HarfBuzzSharp.Direction;

namespace Tempo.Reporting.Engine.Text;

/// <summary>
/// A single positioned glyph produced by <see cref="BidiTextShaper"/>. Glyphs are ordered in
/// visual (left-to-right) order regardless of the base writing direction.
/// </summary>
/// <param name="GlyphId">Font glyph index to draw (already the mirrored glyph for L4 brackets).</param>
/// <param name="X">Pen x position within the line, before any box-fit horizontal scaling.</param>
/// <param name="Y">Vertical offset from the baseline (non-zero for combining marks).</param>
/// <param name="Advance">Horizontal advance of this glyph.</param>
/// <param name="SourceIndex">UTF-16 index of the glyph's cluster within the original text.</param>
/// <param name="Level">Resolved Unicode embedding level of the source cluster.</param>
/// <param name="Mirrored">Whether the source character is Bidi mirrored on this line (rule L4).</param>
/// <param name="GeometricMirror">
/// Whether the glyph must be mirrored geometrically because the character is mirrored but has no
/// distinct mirror glyph in Unicode (HarfBuzz cannot substitute one).
/// </param>
internal readonly record struct ShapedGlyph(
    ushort GlyphId,
    float X,
    float Y,
    float Advance,
    int SourceIndex,
    byte Level,
    bool Mirrored,
    bool GeometricMirror);

/// <summary>Result of shaping and bidi-ordering a run of text.</summary>
/// <param name="ParagraphLevel">Resolved paragraph embedding level (0 = LTR, 1 = RTL).</param>
/// <param name="Glyphs">Positioned glyphs in visual left-to-right order.</param>
/// <param name="Width">Total shaped advance width, including letter spacing.</param>
internal sealed record ShapedText(
    sbyte ParagraphLevel,
    IReadOnlyList<ShapedGlyph> Glyphs,
    float Width);

/// <summary>
/// Bridges the Unicode Bidirectional Algorithm (<see cref="BidiAlgorithm"/>) with HarfBuzz glyph
/// shaping. Splits text into embedding-level runs, shapes each run with the correct direction, and
/// lays the runs out in visual order so mixed Arabic/Hebrew/Latin/number text is drawn correctly.
/// </summary>
internal static class BidiTextShaper
{
    /// <summary>Maps a report text direction to a bidi paragraph level (null = auto-detect).</summary>
    public static sbyte? ToParagraphLevel(ReportTextDirection direction) => direction switch
    {
        ReportTextDirection.Ltr => (sbyte)0,
        ReportTextDirection.Rtl => (sbyte)1,
        _ => null,
    };

    /// <summary>
    /// Whether a bidi result requires the shaped, reordered draw path. Pure left-to-right text with
    /// no right-to-left runs and no mirrored characters returns <see langword="false"/> so the
    /// existing simple render path is used unchanged.
    /// </summary>
    public static bool RequiresBidiShaping(BidiResult bidi)
    {
        if (bidi.ParagraphLevel == 1)
        {
            return true;
        }

        var levels = bidi.Levels;
        for (int i = 0; i < levels.Count; i++)
        {
            if ((levels[i] & 1) == 1)
            {
                return true;
            }
        }

        var mirrored = bidi.Mirrored;
        for (int i = 0; i < mirrored.Count; i++)
        {
            if (mirrored[i])
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Resolves bidi levels, shapes each level run with HarfBuzz, and returns the glyphs laid out in
    /// visual order. Letter spacing is applied per grapheme cluster (not per UTF-16 code unit).
    /// </summary>
    public static ShapedText ShapeAndOrder(
        string text,
        SKTypeface typeface,
        SKFont font,
        double letterSpacing,
        sbyte? baseLevel)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(typeface);
        ArgumentNullException.ThrowIfNull(font);

        var bidi = BidiAlgorithm.Resolve(text, baseLevel);
        int length = text.Length;
        if (length == 0)
        {
            return new ShapedText(bidi.ParagraphLevel, [], 0f);
        }

        var levels = bidi.Levels;
        var visualOrder = bidi.VisualOrder;
        var mirroredFlags = bidi.Mirrored;

        // Maximal runs of equal embedding level, in logical order.
        var logicalRuns = new List<(int Start, int Length, byte Level)>();
        int index = 0;
        while (index < length)
        {
            byte level = levels[index];
            int start = index;
            index++;
            while (index < length && levels[index] == level)
            {
                index++;
            }

            logicalRuns.Add((start, index - start, level));
        }

        // Order the runs left-to-right using the conformance-tested L2 visual map: a run's visual
        // position is the smallest visual index among its code units.
        int[] runOrder = new int[logicalRuns.Count];
        int[] runVisual = new int[logicalRuns.Count];
        for (int r = 0; r < logicalRuns.Count; r++)
        {
            runOrder[r] = r;
            var run = logicalRuns[r];
            int min = int.MaxValue;
            for (int j = run.Start; j < run.Start + run.Length; j++)
            {
                if (visualOrder[j] < min)
                {
                    min = visualOrder[j];
                }
            }

            runVisual[r] = min;
        }

        Array.Sort(runOrder, (a, b) => runVisual[a].CompareTo(runVisual[b]));

        var glyphs = new List<ShapedGlyph>(length);
        float lineCursor = 0f;
        int clusterBoundaries = 0;
        bool firstGlyph = true;
        long previousClusterKey = -1;

        using var shaper = new SKShaper(typeface);
        foreach (int ri in runOrder)
        {
            var run = logicalRuns[ri];
            bool rtl = (run.Level & 1) == 1;
            string runText = text.Substring(run.Start, run.Length);

            using var buffer = new HbBuffer();
            buffer.AddUtf16(runText);
            buffer.GuessSegmentProperties();
            buffer.Direction = rtl ? HbDirection.RightToLeft : HbDirection.LeftToRight;

            var result = shaper.Shape(buffer, font);
            var codepoints = result.Codepoints;
            var clusters = result.Clusters;
            var points = result.Points;
            float runWidth = result.Width;
            int glyphCount = codepoints.Length;

            for (int k = 0; k < glyphCount; k++)
            {
                float advance = (k + 1 < glyphCount ? points[k + 1].X : runWidth) - points[k].X;
                int clusterLocal = (int)clusters[k];
                int sourceIndex = run.Start + clusterLocal;

                long clusterKey = ((long)ri << 32) | (uint)clusterLocal;
                if (!firstGlyph && clusterKey != previousClusterKey)
                {
                    clusterBoundaries++;
                }

                firstGlyph = false;
                previousClusterKey = clusterKey;

                bool mirrored = sourceIndex < mirroredFlags.Count && mirroredFlags[sourceIndex];
                bool geometricMirror = false;
                if (mirrored)
                {
                    int codePoint = char.ConvertToUtf32(text, sourceIndex);
                    geometricMirror = BidiAlgorithm.GetMirrorGlyph(codePoint) == codePoint;
                }

                float x = lineCursor + points[k].X + ((float)letterSpacing * clusterBoundaries);
                glyphs.Add(new ShapedGlyph(
                    (ushort)codepoints[k],
                    x,
                    points[k].Y,
                    advance,
                    sourceIndex,
                    run.Level,
                    mirrored,
                    geometricMirror));
            }

            lineCursor += runWidth;
        }

        float totalWidth = lineCursor + ((float)letterSpacing * clusterBoundaries);
        return new ShapedText(bidi.ParagraphLevel, glyphs, totalWidth);
    }

    /// <summary>
    /// Draws shaped glyphs at their computed positions with the baseline at the current origin.
    /// Glyphs flagged for geometric mirroring are flipped horizontally about their advance center.
    /// </summary>
    public static void Draw(SKCanvas canvas, ShapedText shaped, SKFont font, SKPaint paint)
    {
        var glyphs = shaped.Glyphs;
        int i = 0;
        while (i < glyphs.Count)
        {
            if (glyphs[i].GeometricMirror)
            {
                DrawGeometricMirror(canvas, glyphs[i], font, paint);
                i++;
                continue;
            }

            int start = i;
            while (i < glyphs.Count && !glyphs[i].GeometricMirror)
            {
                i++;
            }

            DrawGlyphRange(canvas, glyphs, start, i, font, paint);
        }
    }

    private static void DrawGlyphRange(
        SKCanvas canvas,
        IReadOnlyList<ShapedGlyph> glyphs,
        int start,
        int end,
        SKFont font,
        SKPaint paint)
    {
        int count = end - start;
        if (count <= 0)
        {
            return;
        }

        ushort[] ids = new ushort[count];
        SKPoint[] positions = new SKPoint[count];
        for (int k = 0; k < count; k++)
        {
            var glyph = glyphs[start + k];
            ids[k] = glyph.GlyphId;
            positions[k] = new SKPoint(glyph.X, glyph.Y);
        }

        using var builder = new SKTextBlobBuilder();
        var runBuffer = builder.AllocatePositionedRun(font, count);
        runBuffer.SetGlyphs(ids);
        runBuffer.SetPositions(positions);
        using var blob = builder.Build();
        canvas.DrawText(blob, 0, 0, paint);
    }

    private static void DrawGeometricMirror(SKCanvas canvas, ShapedGlyph glyph, SKFont font, SKPaint paint)
    {
        float center = glyph.X + (glyph.Advance / 2f);
        canvas.Save();
        canvas.Translate(center, 0);
        canvas.Scale(-1, 1);
        canvas.Translate(-center, 0);

        using var builder = new SKTextBlobBuilder();
        var runBuffer = builder.AllocatePositionedRun(font, 1);
        runBuffer.SetGlyphs([glyph.GlyphId]);
        runBuffer.SetPositions([new SKPoint(glyph.X, glyph.Y)]);
        using var blob = builder.Build();
        canvas.DrawText(blob, 0, 0, paint);

        canvas.Restore();
    }
}
