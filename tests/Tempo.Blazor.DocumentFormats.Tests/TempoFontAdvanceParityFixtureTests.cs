using System.Text;
using System.Text.Json;
using FluentAssertions;
using SkiaSharp;
using Tempo.Blazor.DocumentFormats.HeadlessLayout;
using Tempo.Reporting.Engine.Pdf;

namespace Tempo.Blazor.DocumentFormats.Tests;

/// <summary>
/// Generates and pins the JS↔C# measurement parity fixture
/// (TestData/font-advance-parity-fixture.json). The fixture carries the extracted advance table
/// for the committed Dancing Script font plus sample texts with expected widths computed by the
/// exact double-precision formula the JS measurer uses
/// (<c>units × fontSize / unitsPerEm + letterSpacing × (n−1)</c>). The Node lane
/// (scripts/font-advance-parity.test.mjs) replays the samples through the real JS measurer and
/// asserts ZERO deviation. Regenerate with TEMPO_REGENERATE_FONT_PARITY_FIXTURE=1.
/// </summary>
public class TempoFontAdvanceParityFixtureTests
{
    private const string FixtureFileName = "font-advance-parity-fixture.json";

    private static readonly (string Text, double FontSize, double LetterSpacing)[] Samples =
    [
        ("Příliš žluťoučký kůň úpěl ďábelské ódy", 16, 0),
        ("Wave forms & lines — 0123456789", 11, 0),
        ("Prostrkaný text s diakritikou: ěščřžýáíé", 14, 1.5),
        ("y, W. i", 24, 0.25),
    ];

    [Fact]
    public void CommittedFixture_MatchesFreshExtraction()
    {
        var fresh = BuildFixtureJson();

        if (Environment.GetEnvironmentVariable("TEMPO_REGENERATE_FONT_PARITY_FIXTURE") == "1")
        {
            File.WriteAllText(SourceFixturePath(), fresh);
        }

        var committedPath = Path.Combine(AppContext.BaseDirectory, "TestData", FixtureFileName);
        File.Exists(committedPath).Should().BeTrue(
            $"the parity fixture must be committed — regenerate via TEMPO_REGENERATE_FONT_PARITY_FIXTURE=1 ({FixtureFileName})");

        var committed = File.ReadAllText(committedPath).Replace("\r\n", "\n", StringComparison.Ordinal);
        if (OperatingSystem.IsWindows())
        {
            // The fixture is generated on Windows — byte determinism holds per platform.
            committed.Should().Be(
                fresh.Replace("\r\n", "\n", StringComparison.Ordinal),
                "the committed parity fixture must match a fresh extraction from the committed font bytes");
            return;
        }

        // Other platforms: Skia's scaler backend (FreeType vs DirectWrite) differs by ~1e-5 font
        // units per advance — compare structurally with a tight tolerance. The Node lane stays
        // exact everywhere because it replays the COMMITTED table against the COMMITTED
        // expectations (internally consistent by construction).
        using var freshDocument = JsonDocument.Parse(fresh);
        using var committedDocument = JsonDocument.Parse(committed);
        var freshFace = freshDocument.RootElement.GetProperty("table").GetProperty("faces")[0];
        var committedFace = committedDocument.RootElement.GetProperty("table").GetProperty("faces")[0];
        freshFace.GetProperty("unitsPerEm").GetInt32().Should().Be(committedFace.GetProperty("unitsPerEm").GetInt32());
        var freshAdvances = freshFace.GetProperty("advances");
        foreach (var advance in committedFace.GetProperty("advances").EnumerateObject())
        {
            freshAdvances.TryGetProperty(advance.Name, out var freshValue).Should().BeTrue(
                $"code point {advance.Name} must be covered on every platform");
            freshValue.GetDouble().Should().BeApproximately(
                advance.Value.GetDouble(), 0.05, $"advance for code point {advance.Name} must match within scaler noise");
        }
    }

    [Fact]
    public void FixtureSamples_CoverDiacriticsAndLetterSpacing()
    {
        using var document = JsonDocument.Parse(BuildFixtureJson());
        var samples = document.RootElement.GetProperty("samples");

        samples.GetArrayLength().Should().Be(Samples.Length);
        samples.EnumerateArray().Should().Contain(
            sample => sample.GetProperty("letterSpacing").GetDouble() > 0,
            "letter-spacing must be exercised");
        samples.EnumerateArray().Should().Contain(
            sample => sample.GetProperty("text").GetString()!.Contains('ř'),
            "Czech diacritics must be exercised");
    }

    [Fact]
    public void FixtureExpectedUnits_AgreeWithSkFontMeasureTextUpToFloatNoise()
    {
        var face = TempoFontAdvanceTableExtractor.Shared.ExtractFace(LoadFontFace());
        var bytes = LoadFontFace().Bytes;
        using var data = SKData.CreateCopy(bytes);
        using var typeface = SKTypeface.FromData(data);
        using var font = new SKFont(typeface!, face.UnitsPerEm)
        {
            Hinting = SKFontHinting.None,
            LinearMetrics = true,
            Subpixel = true,
        };

        foreach (var (text, _, _) in Samples)
        {
            var units = SumAdvances(face, text);
            units.Should().BeApproximately(
                font.MeasureText(text),
                0.001 * text.Length,
                $"fixture unit sum for \"{text}\" must match SKFont.MeasureText up to float accumulation noise");
        }
    }

    private static ReportPdfFontFace LoadFontFace()
        => new(
            "Dancing Script",
            400,
            "normal",
            File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "TestData", "Fonts", "DancingScript-VariableFont_wght.ttf")));

    private static double SumAdvances(TempoFontAdvanceFace face, string text)
    {
        var units = 0d;
        foreach (var rune in text.EnumerateRunes())
        {
            face.Advances.Should().ContainKey(rune.Value, $"sample glyph '{rune}' must exist in the font");
            units += face.Advances[rune.Value];
        }

        return units;
    }

    private static string BuildFixtureJson()
    {
        var reportFace = LoadFontFace();
        var extractor = TempoFontAdvanceTableExtractor.Shared;
        var face = extractor.ExtractFace(reportFace);
        var tableJson = extractor.BuildAdvanceTablesJson([reportFace]);

        var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", 1);
            writer.WritePropertyName("table");
            writer.WriteRawValue(tableJson);
            writer.WriteStartArray("samples");
            foreach (var (text, fontSize, letterSpacing) in Samples)
            {
                var units = SumAdvances(face, text);
                var glyphCount = text.EnumerateRunes().Count();

                // EXACTLY the JS measurer's double formula: context width = units × size / upem,
                // then the service adds letterSpacing × (n−1). Same IEEE-754 ops → the Node lane
                // asserts bit-identical equality (zero deviation).
                var expectedWidth = units * fontSize / face.UnitsPerEm + letterSpacing * (glyphCount - 1);

                writer.WriteStartObject();
                writer.WriteString("text", text);
                writer.WriteString("fontFamily", face.Family);
                writer.WriteNumber("fontSize", fontSize);
                writer.WriteNumber("letterSpacing", letterSpacing);
                writer.WriteNumber("expectedUnits", units);
                writer.WriteNumber("expectedWidth", expectedWidth);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static string SourceFixturePath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TempoBlazor.slnx")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull("the repo root (TempoBlazor.slnx) must be reachable from the test output directory");
        return Path.Combine(
            directory!.FullName,
            "tests", "Tempo.Blazor.DocumentFormats.Tests", "TestData", FixtureFileName);
    }
}
