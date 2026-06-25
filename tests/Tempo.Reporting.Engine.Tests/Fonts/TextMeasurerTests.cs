using Tempo.Reporting.Engine.Fonts;

namespace Tempo.Reporting.Engine.Tests.Fonts;

public sealed class TextMeasurerTests
{
    [Fact]
    public void MeasureRun_UsesAdvanceWidthsKerningLetterSpacingAndFontSize()
    {
        var face = new FontMetricFace(
            "Tempo F0 Sans",
            FontStyleKey.Regular,
            unitsPerEm: 1000,
            ascent: 800,
            descent: -200,
            lineGap: 0,
            missingGlyphAdvanceWidth: 500,
            advanceWidths: new Dictionary<int, ushort>
            {
                ['A'] = 600,
                ['V'] = 620
            },
            kerningPairs: new Dictionary<FontKerningPair, short>
            {
                [new('A', 'V')] = -80
            });
        ITextMeasurer measurer = new TableTextMeasurer(new FontMetricTable([face], "Tempo F0 Sans"));

        var measurement = measurer.MeasureRun(new TextMeasureRequest(
            Text: "AV",
            FontFamily: "Tempo F0 Sans",
            FontSize: 20,
            Bold: false,
            Italic: false,
            LetterSpacing: 1.5,
            Kerning: true));

        measurement.Width.Should().BeApproximately(24.3, 0.0001);
        measurement.Ascent.Should().BeApproximately(16, 0.0001);
        measurement.Descent.Should().BeApproximately(4, 0.0001);
        measurement.LineHeight.Should().BeApproximately(20, 0.0001);
    }

    [Fact]
    public void MeasureRun_FallsBackWhenPrimaryFaceMissesGlyph()
    {
        var primary = new FontMetricFace(
            "Tempo F0 Sans",
            FontStyleKey.Regular,
            1000,
            800,
            -200,
            0,
            400,
            new Dictionary<int, ushort> { ['A'] = 600 },
            new Dictionary<FontKerningPair, short>());
        var fallback = new FontMetricFace(
            "Tempo F0 CJK",
            FontStyleKey.Regular,
            1000,
            880,
            -220,
            20,
            500,
            new Dictionary<int, ushort> { ['会'] = 1000 },
            new Dictionary<FontKerningPair, short>());
        ITextMeasurer measurer = new TableTextMeasurer(new FontMetricTable([primary, fallback], "Tempo F0 Sans", ["Tempo F0 CJK"]));

        var measurement = measurer.MeasureRun(new TextMeasureRequest("A会", "Tempo F0 Sans", 10));

        measurement.Width.Should().BeApproximately(16, 0.0001);
        measurement.MissingGlyphCount.Should().Be(0);
        measurement.FallbackGlyphCount.Should().Be(1);
    }

    [Fact]
    public void MeasureRun_UsesHintedAdvanceWidthsForIntegerCssPixels()
    {
        var face = new FontMetricFace(
            "Tempo F0 Sans",
            FontStyleKey.Regular,
            unitsPerEm: 1000,
            ascent: 800,
            descent: -200,
            lineGap: 0,
            missingGlyphAdvanceWidth: 500,
            advanceWidths: new Dictionary<int, ushort>
            {
                ['A'] = 600,
                ['V'] = 620
            },
            kerningPairs: new Dictionary<FontKerningPair, short>(),
            hintedAdvanceWidths: new Dictionary<int, IReadOnlyDictionary<int, ushort>>
            {
                [20] = new Dictionary<int, ushort>
                {
                    ['A'] = 11,
                    ['V'] = 12
                }
            });
        ITextMeasurer measurer = new TableTextMeasurer(new FontMetricTable([face], "Tempo F0 Sans"));

        var hinted = measurer.MeasureRun(new TextMeasureRequest("AV", "Tempo F0 Sans", 20, Kerning: false));
        var fractional = measurer.MeasureRun(new TextMeasureRequest("AV", "Tempo F0 Sans", 20.5, Kerning: false));

        hinted.Width.Should().BeApproximately(23, 0.0001);
        fractional.Width.Should().BeApproximately(25.01, 0.0001);
    }
}
