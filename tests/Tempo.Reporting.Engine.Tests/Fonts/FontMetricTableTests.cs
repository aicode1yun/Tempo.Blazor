using Tempo.Reporting.Engine.Fonts;

namespace Tempo.Reporting.Engine.Tests.Fonts;

public sealed class FontMetricTableTests
{
    [Fact]
    public void BuildFromTrueType_ReadsAdvanceWidthsAscentDescentAndKerning()
    {
        var fontBytes = TestTrueTypeFontBuilder.BuildMinimalFont(
            unitsPerEm: 1000,
            ascent: 820,
            descent: -210,
            lineGap: 40,
            advances: new Dictionary<int, ushort>
            {
                ['A'] = 610,
                ['V'] = 615,
                [' '] = 250
            },
            kerning: new Dictionary<(int Left, int Right), short>
            {
                [('A', 'V')] = -70
            },
            hintedAdvances: new Dictionary<int, IReadOnlyDictionary<int, byte>>
            {
                [20] = new Dictionary<int, byte>
                {
                    ['A'] = 12,
                    ['V'] = 13,
                    [' '] = 5
                }
            });

        using var input = new MemoryStream(fontBytes);
        var face = TrueTypeFontMetricReader.Read(input, "Tempo F0 Sans", FontStyleKey.Regular);

        face.UnitsPerEm.Should().Be(1000);
        face.Ascent.Should().Be(820);
        face.Descent.Should().Be(-210);
        face.LineGap.Should().Be(40);
        face.GetAdvanceWidth('A').Should().Be(610);
        face.GetAdvanceWidth('V').Should().Be(615);
        face.GetKerning('A', 'V').Should().Be(-70);
        face.TryGetHintedAdvanceWidth('A', 20, out var hintedWidth).Should().BeTrue();
        hintedWidth.Should().Be(12);
    }

    [Fact]
    public void BinarySerializer_RoundTripsMetricTable()
    {
        var face = new FontMetricFace(
            "Tempo F0 Sans",
            FontStyleKey.Bold,
            unitsPerEm: 1000,
            ascent: 800,
            descent: -200,
            lineGap: 0,
            missingGlyphAdvanceWidth: 500,
            advanceWidths: new Dictionary<int, ushort> { ['A'] = 700 },
            kerningPairs: new Dictionary<FontKerningPair, short> { [new('A', 'V')] = -80 },
            hintedAdvanceWidths: new Dictionary<int, IReadOnlyDictionary<int, ushort>>
            {
                [16] = new Dictionary<int, ushort> { ['A'] = 11 }
            });
        var table = new FontMetricTable([face], "Tempo F0 Sans");

        using var buffer = new MemoryStream();
        FontMetricTableBinarySerializer.Write(table, buffer);
        buffer.Position = 0;
        var restored = FontMetricTableBinarySerializer.Read(buffer);

        var restoredFace = restored.ResolveFace("Tempo F0 Sans", bold: true, italic: false);
        restored.DefaultFamilyName.Should().Be("Tempo F0 Sans");
        restoredFace.GetAdvanceWidth('A').Should().Be(700);
        restoredFace.GetKerning('A', 'V').Should().Be(-80);
        restoredFace.TryGetHintedAdvanceWidth('A', 16, out var hintedWidth).Should().BeTrue();
        hintedWidth.Should().Be(11);
    }
}
