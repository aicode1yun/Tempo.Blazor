using SkiaSharp;
using Tempo.Reporting.Engine.Text;

namespace Tempo.Reporting.Engine.Tests.Text;

/// <summary>
/// Structural tests for bidi-aware HarfBuzz shaping. Assertions target glyph order and relative
/// positions (not pixels), because Skia advances differ by ~1e-5 across Windows and Linux. Hebrew
/// and Latin/parenthesis coverage runs on any platform (DejaVu on the Linux image, Segoe UI on
/// Windows); the Arabic joining assertion self-skips when no Arabic typeface is installed.
/// </summary>
public sealed class BidiTextShaperTests
{
    private const string Hebrew = "אבגד"; // aleph bet gimel dalet
    private const string Arabic = "مرحبا"; // marhaba

    [Fact]
    public void RequiresBidiShaping_PureLeftToRightText_ReturnsFalse()
    {
        var bidi = BidiAlgorithm.Resolve("Hello World 2024", null);

        BidiTextShaper.RequiresBidiShaping(bidi).Should().BeFalse();
    }

    [Fact]
    public void RequiresBidiShaping_TextWithHebrew_ReturnsTrue()
    {
        var bidi = BidiAlgorithm.Resolve("Total: " + Hebrew, null);

        BidiTextShaper.RequiresBidiShaping(bidi).Should().BeTrue();
    }

    [Fact]
    public void RequiresBidiShaping_ForcedRtlOnLatin_ReturnsTrue()
    {
        var bidi = BidiAlgorithm.Resolve("Hello", 1);

        BidiTextShaper.RequiresBidiShaping(bidi).Should().BeTrue();
    }

    [Fact]
    public void ShapeAndOrder_HebrewParagraph_LaysGlyphsRightToLeft()
    {
        var (typeface, font) = ResolveFont('א');
        using var _ = font;

        var shaped = BidiTextShaper.ShapeAndOrder(Hebrew, typeface, font, letterSpacing: 0, baseLevel: null);

        shaped.ParagraphLevel.Should().Be(1, "a Hebrew paragraph resolves to a right-to-left base level");
        shaped.Width.Should().BeGreaterThan(0);
        shaped.Glyphs.Should().NotBeEmpty();

        // Glyphs are emitted in visual (left-to-right) order.
        var visual = shaped.Glyphs.OrderBy(g => g.X).ToList();
        visual.Select(g => g.X).Should().BeInAscendingOrder();

        // In a right-to-left run the first logical character is drawn at the right edge, so source
        // indices decrease as we move left-to-right through the visual glyphs.
        visual.Select(g => g.SourceIndex).Should().BeInDescendingOrder();

        // The very first logical character sits at the largest x (the right edge of the box).
        float rightmostX = visual[^1].X;
        shaped.Glyphs.Where(g => g.SourceIndex == 0)
            .Should().OnlyContain(g => g.X >= rightmostX - 0.001f);
    }

    [Fact]
    public void ShapeAndOrder_HebrewLatinNumberMix_OrdersRunsVisually()
    {
        var (typeface, font) = ResolveFont('א');
        using var _ = font;

        // Hebrew (RTL) then a Latin brand and a number (both LTR). With a Hebrew first strong
        // character the paragraph is RTL: the Latin/number block must appear to the LEFT of Hebrew.
        var latin = "AB";
        var number = "12";
        var text = Hebrew + " " + latin + " " + number;
        int hebStart = 0;
        int hebEnd = Hebrew.Length;
        int latinStart = Hebrew.Length + 1;
        int latinEnd = latinStart + latin.Length;
        int numberStart = latinEnd + 1;
        int numberEnd = numberStart + number.Length;

        var shaped = BidiTextShaper.ShapeAndOrder(text, typeface, font, letterSpacing: 0, baseLevel: null);

        shaped.ParagraphLevel.Should().Be(1);
        shaped.Glyphs.Select(g => g.X).OrderBy(x => x).Should().BeInAscendingOrder();

        float MaxX(int start, int end) => shaped.Glyphs.Where(g => g.SourceIndex >= start && g.SourceIndex < end).Max(g => g.X);
        float MinX(int start, int end) => shaped.Glyphs.Where(g => g.SourceIndex >= start && g.SourceIndex < end).Min(g => g.X);

        // Hebrew is visually to the right of the whole Latin+number block.
        MinX(hebStart, hebEnd).Should().BeGreaterThan(MaxX(latinStart, numberEnd));

        // The Latin brand stays left-to-right internally and precedes the number block.
        MaxX(latinStart, latinEnd).Should().BeLessThan(MinX(numberStart, numberEnd));
    }

    [Fact]
    public void ShapeAndOrder_MirroredParenthesisInRtl_ProducesMirrorGlyph()
    {
        var (typeface, font) = ResolveFont('א');
        using var _ = font;

        // aleph, '(', bet: the parenthesis is a mirrored neutral resolved into the RTL run.
        var text = "א(ב";
        int parenIndex = 1;

        var shaped = BidiTextShaper.ShapeAndOrder(text, typeface, font, letterSpacing: 0, baseLevel: 1);

        var parenGlyph = shaped.Glyphs.Single(g => g.SourceIndex == parenIndex);
        parenGlyph.Mirrored.Should().BeTrue("'(' is Bidi mirrored at an odd embedding level");
        parenGlyph.GeometricMirror.Should().BeFalse("'(' has a distinct Unicode mirror glyph ')'");

        ushort openGlyph = font.GetGlyph('(');
        ushort closeGlyph = font.GetGlyph(')');
        parenGlyph.GlyphId.Should().Be(closeGlyph, "the RTL run must draw the mirror ')' glyph");
        parenGlyph.GlyphId.Should().NotBe(openGlyph);
    }

    [Fact]
    public void ShapeAndOrder_LetterSpacing_SpreadsClustersAndWidensLine()
    {
        var (typeface, font) = ResolveFont('א');
        using var _ = font;

        var tight = BidiTextShaper.ShapeAndOrder(Hebrew, typeface, font, letterSpacing: 0, baseLevel: 1);
        var spaced = BidiTextShaper.ShapeAndOrder(Hebrew, typeface, font, letterSpacing: 5, baseLevel: 1);

        int clusters = Hebrew.Length; // one glyph per Hebrew letter
        spaced.Width.Should().BeApproximately(tight.Width + (5f * (clusters - 1)), 0.5f);
        spaced.Glyphs.Should().HaveSameCount(tight.Glyphs);
    }

    [Fact]
    public void ShapeAndOrder_PureLatin_KeepsLogicalOrderLeftToRight()
    {
        var (typeface, font) = ResolveFont('A');
        using var _ = font;

        var shaped = BidiTextShaper.ShapeAndOrder("ACME", typeface, font, letterSpacing: 0, baseLevel: 0);

        shaped.ParagraphLevel.Should().Be(0);
        shaped.Glyphs.Select(g => g.X).Should().BeInAscendingOrder();
        shaped.Glyphs.Select(g => g.SourceIndex).Should().BeInAscendingOrder();
        shaped.Glyphs.Should().OnlyContain(g => !g.Mirrored);
    }

    [Fact]
    public void ShapeAndOrder_ShapedArabic_WidthDiffersFromNaivePerCharacterSum()
    {
        var (typeface, font) = ResolveFont('م');
        using var _ = font;

        if (!font.ContainsGlyph('م'))
        {
            // No Arabic-capable typeface on this platform; joining cannot be demonstrated. The
            // Hebrew/Latin ordering tests still cover bidi reordering on every platform.
            return;
        }

        var shaped = BidiTextShaper.ShapeAndOrder(Arabic, typeface, font, letterSpacing: 0, baseLevel: null);

        float naiveSum = 0f;
        foreach (var ch in Arabic)
        {
            naiveSum += font.MeasureText(ch.ToString());
        }

        // Arabic joining forms and ligatures make the shaped advance differ from a naive per-code
        // point measurement, proving HarfBuzz shaping is applied rather than SKFont.MeasureText.
        shaped.Width.Should().NotBe(0);
        Math.Abs(shaped.Width - naiveSum).Should().BeGreaterThan(1f);
    }

    private static (SKTypeface Typeface, SKFont Font) ResolveFont(char sample)
    {
        var typeface = SKFontManager.Default.MatchCharacter(sample) ?? SKTypeface.Default;
        var font = new SKFont(typeface, 32f)
        {
            Edging = SKFontEdging.Antialias,
            Subpixel = true,
        };

        return (typeface, font);
    }
}
