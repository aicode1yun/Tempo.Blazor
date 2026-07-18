using System.Text;
using System.Text.Json;
using FluentAssertions;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentFormats.Pdf;

namespace Tempo.Blazor.DocumentFormats.Tests;

/// <summary>
/// Contract tests for the production document PDF renderer: input is the canonical document plus
/// the canvas layout snapshot (the exact commands the editor painted), output is a vector PDF with
/// a real text layer whose geometry matches the editor layout (WYSIWYG parity by construction).
/// </summary>
public class TempoDocumentPdfRendererTests
{
    private const string TinyPngDataUri =
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAFgwJ/lIdt3wAAAABJRU5ErkJggg==";

    // ── Contract: canonical document + layout snapshot → text-layer PDF ────────────────────────

    [Fact]
    public void Render_FromLayoutSnapshot_ProducesPdfWithOnePagePerSnapshotPage()
    {
        var renderer = new TempoDocumentPdfRenderer();

        var pdf = renderer.Render(CreateRequest(TwoPageSnapshotJson()));

        pdf.Should().NotBeNull();
        Encoding.ASCII.GetString(pdf, 0, 5).Should().Be("%PDF-");
        var text = Encoding.Latin1.GetString(pdf);
        ExtractMediaBoxes(text).Should().HaveCount(2, "each snapshot page becomes one PDF page");
    }

    [Fact]
    public void Render_ConvertsCssPixelPageSizeToPdfPoints()
    {
        var renderer = new TempoDocumentPdfRenderer();

        var pdf = renderer.Render(CreateRequest(TwoPageSnapshotJson()));

        // 794×1123 CSS px at 0.75 pt/px = 595.5×842.25 pt (A4). Tolerance < 1 pt.
        var text = Encoding.Latin1.GetString(pdf);
        var boxes = ExtractMediaBoxes(text);
        foreach (var box in boxes)
        {
            var parts = box.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            double.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture).Should().BeApproximately(595.5, 1.0);
            double.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture).Should().BeApproximately(842.25, 1.0);
        }
    }

    [Fact]
    public void Render_EmbedsTextLayer_NotJustRaster()
    {
        var renderer = new TempoDocumentPdfRenderer();

        var pdf = renderer.Render(CreateRequest(TwoPageSnapshotJson()));

        var text = Encoding.Latin1.GetString(pdf);
        text.Should().Contain("/Font", "text commands must produce a PDF text layer with font resources");
    }

    [Fact]
    public void Render_WithoutLayoutSnapshot_ThrowsExplicitContractViolation()
    {
        var renderer = new TempoDocumentPdfRenderer();
        var request = CreateRequest(layoutSnapshotJson: null);

        var act = () => renderer.Render(request);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*LayoutSnapshotJson*", "the renderer contract requires the canvas layout snapshot");
    }

    // ── Parity: translated geometry matches the snapshot within < 1 pt ─────────────────────────

    [Fact]
    public void Translate_KeepsPageCountAndCommandPositionsWithinOnePoint()
    {
        var snapshotJson = TwoPageSnapshotJson();
        var source = JsonDocument.Parse(snapshotJson);

        var translated = TempoDocumentPdfRenderer.TranslateLayoutSnapshot(snapshotJson);

        var sourcePages = source.RootElement.GetProperty("pages").EnumerateArray().ToList();
        translated.Pages.Should().HaveCount(sourcePages.Count);
        for (var pageIndex = 0; pageIndex < sourcePages.Count; pageIndex++)
        {
            var sourceCommands = sourcePages[pageIndex].GetProperty("commands").EnumerateArray().ToList();
            var page = translated.Pages[pageIndex];
            page.Width.Should().Be(sourcePages[pageIndex].GetProperty("width").GetDouble());
            page.Commands.Should().HaveCount(sourceCommands.Count);
            for (var i = 0; i < sourceCommands.Count; i++)
            {
                // Same CSS-pixel space on both sides; 1 px = 0.75 pt so equality here guarantees < 1 pt.
                page.Commands[i].X.Should().BeApproximately(sourceCommands[i].GetProperty("x").GetDouble(), 1.0 / 0.75);
                if (sourceCommands[i].GetProperty("type").GetString() == "text")
                {
                    // Text draws at its baseline — that is the position that must survive translation.
                    page.Commands[i].Baseline.Should().Be(sourceCommands[i].GetProperty("baseline").GetDouble());
                }
                else
                {
                    page.Commands[i].Y.Should().BeApproximately(sourceCommands[i].GetProperty("y").GetDouble(), 1.0 / 0.75);
                }
            }
        }
    }

    [Fact]
    public void Translate_MapsCommandVocabulary()
    {
        var translated = TempoDocumentPdfRenderer.TranslateLayoutSnapshot(TwoPageSnapshotJson());

        var commands = translated.Pages.SelectMany(page => page.Commands).ToList();
        commands.Should().Contain(command =>
            command.Type == Tempo.Reporting.Engine.Snapshot.ReportSnapshotCommandType.TextRun
            && command.Text == "Smlouva o dílo — příliš žluťoučký kůň"
            && command.FontWeight == "700");
        commands.Should().Contain(command =>
            command.Type == Tempo.Reporting.Engine.Snapshot.ReportSnapshotCommandType.Rectangle
            && command.Stroke == "#94a3b8");
        commands.Should().Contain(command =>
            command.Type == Tempo.Reporting.Engine.Snapshot.ReportSnapshotCommandType.Image
            && command.Source == TinyPngDataUri);
        commands.Should().Contain(command =>
            command.Type == Tempo.Reporting.Engine.Snapshot.ReportSnapshotCommandType.Line);

        var textRun = commands.First(command => command.Text == "Smlouva o dílo — příliš žluťoučký kůň");
        textRun.Baseline.Should().Be(120);
        textRun.FontFamily.Should().Be("Aptos", "the first family of the CSS stack is used");
    }

    // ── Parity against a snapshot produced by the real canvas engine exporter ──────────────────

    [Fact]
    public void Render_RealEngineFixture_KeepsPageCountAndGeometryParity()
    {
        // Fixture generated by buildLayoutSnapshotExport (the actual canvas engine layout +
        // display-list pipeline) with deterministic font metrics — see TestData/README note.
        var snapshotJson = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", "layout-snapshot-parity-fixture.json"));
        using var source = JsonDocument.Parse(snapshotJson);
        var expectedPageCount = source.RootElement.GetProperty("pageCount").GetInt32();
        var expectedFirstPageTextCount = source.RootElement.GetProperty("pages")[0]
            .GetProperty("commands").EnumerateArray().Count(command => command.GetProperty("type").GetString() == "text");

        var translated = TempoDocumentPdfRenderer.TranslateLayoutSnapshot(snapshotJson);
        var pdf = new TempoDocumentPdfRenderer().Render(CreateRequest(snapshotJson));

        translated.Pages.Should().HaveCount(expectedPageCount, "page breaking must be inherited from the editor layout");
        translated.Pages[0].Commands
            .Count(command => command.Type == Tempo.Reporting.Engine.Snapshot.ReportSnapshotCommandType.TextRun)
            .Should().Be(expectedFirstPageTextCount, "no text command may be dropped");

        var text = Encoding.Latin1.GetString(pdf);
        var boxes = ExtractMediaBoxes(text);
        boxes.Should().HaveCount(expectedPageCount);
        foreach (var box in boxes)
        {
            var parts = box.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            double.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture).Should().BeApproximately(595.5, 1.0);
            double.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture).Should().BeApproximately(842.25, 1.0);
        }
    }

    // ── Fixtures ────────────────────────────────────────────────────────────────────────────────

    private static DocumentPdfExportRequest CreateRequest(string? layoutSnapshotJson)
        => new()
        {
            DocumentId = "pdf-contract-doc",
            FileName = "pdf-contract-doc",
            Document = DocumentEditorDocument.Empty(),
            LayoutSnapshotJson = layoutSnapshotJson,
        };

    private static string TwoPageSnapshotJson()
        => JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            pageCount = 2,
            pages = new object[]
            {
                new
                {
                    index = 0,
                    width = 794.0,
                    height = 1123.0,
                    commands = new object[]
                    {
                        new
                        {
                            id = "title",
                            type = "text",
                            x = 96.0,
                            y = 100.0,
                            width = 320.0,
                            height = 26.0,
                            baseline = 120.0,
                            text = "Smlouva o dílo — příliš žluťoučký kůň",
                            fontFamily = "Aptos, Arial, sans-serif",
                            fontSize = 21.0,
                            fontWeight = "700",
                            fontStyle = "normal",
                            fill = "#111827",
                        },
                        new
                        {
                            id = "table",
                            type = "rect",
                            x = 96.0,
                            y = 200.0,
                            width = 400.0,
                            height = 120.0,
                            stroke = "#94a3b8",
                            strokeWidth = 1.0,
                        },
                        new
                        {
                            id = "leader",
                            type = "line",
                            x = 96.0,
                            y = 340.0,
                            width = 200.0,
                            height = 0.0,
                            stroke = "#334155",
                            strokeWidth = 0.75,
                        },
                    },
                },
                new
                {
                    index = 1,
                    width = 794.0,
                    height = 1123.0,
                    commands = new object[]
                    {
                        new
                        {
                            id = "body",
                            type = "text",
                            x = 96.0,
                            y = 96.0,
                            width = 500.0,
                            height = 18.0,
                            baseline = 110.0,
                            text = "Pokračování na druhé straně.",
                            fontFamily = "Aptos, Arial, sans-serif",
                            fontSize = 14.0,
                            fontWeight = "400",
                            fontStyle = "normal",
                            fill = "#111827",
                        },
                        new
                        {
                            id = "logo",
                            type = "image",
                            x = 96.0,
                            y = 150.0,
                            width = 120.0,
                            height = 60.0,
                            source = TinyPngDataUri,
                        },
                    },
                },
            },
        });

    private static List<string> ExtractMediaBoxes(string pdfText)
    {
        var boxes = new List<string>();
        var index = 0;
        while ((index = pdfText.IndexOf("/MediaBox [", index, StringComparison.Ordinal)) >= 0)
        {
            var start = index + "/MediaBox [".Length;
            var end = pdfText.IndexOf(']', start);
            boxes.Add(pdfText[start..end].Trim());
            index = end;
        }

        return boxes;
    }
}
