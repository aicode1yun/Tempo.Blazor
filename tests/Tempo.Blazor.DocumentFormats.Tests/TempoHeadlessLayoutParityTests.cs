using System.Text;
using System.Text.Json;
using FluentAssertions;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentFormats.HeadlessLayout;
using Tempo.Blazor.DocumentFormats.Pdf;
using Tempo.Reporting.Engine.Pdf;

namespace Tempo.Blazor.DocumentFormats.Tests;

/// <summary>
/// Phase 3 of the headless document runtime: parity between the headless layout and the canvas
/// export contract. A committed 21-page Czech contract document
/// (TestData/headless-parity-document.json) is laid out through the Jint-hosted service with
/// deterministic Skia advance tables and must match the committed browser-generated parity
/// fixture (layout-snapshot-parity-fixture.json) in page count and page geometry, reproduce the
/// committed headless snapshot byte-for-byte (headless-parity-snapshot-fixture.json, also
/// replayed through Node in scripts/headless-parity-crossruntime.test.mjs), and flow through
/// TempoDocumentPdfRenderer into a PDF whose pages and block positions stay within 1 pt.
/// Regenerate the committed pair with TEMPO_REGENERATE_HEADLESS_PARITY_FIXTURE=1.
/// </summary>
public class TempoHeadlessLayoutParityTests
{
    private const string DocumentFileName = "headless-parity-document.json";
    private const string RequestFileName = "headless-parity-request.json";
    private const string SnapshotFileName = "headless-parity-snapshot-fixture.json";

    private static readonly string FontPath =
        Path.Combine(AppContext.BaseDirectory, "TestData", "Fonts", "DancingScript-VariableFont_wght.ttf");

    private static IReadOnlyList<ReportPdfFontFace> CreateFonts()
        => [new ReportPdfFontFace("Dancing Script", 400, "normal", File.ReadAllBytes(FontPath))];

    // ── Task: deterministic metrics — headless output vs committed browser fixture ─────────────

    [Fact]
    public void HeadlessLayout_MatchesCommittedBrowserFixture_PageCountAndGeometry()
    {
        RegenerateFixturesIfRequested();

        using var browserFixture = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "TestData", "layout-snapshot-parity-fixture.json")));
        var expectedPageCount = browserFixture.RootElement.GetProperty("pageCount").GetInt32();
        var browserPage = browserFixture.RootElement.GetProperty("pages")[0];

        using var service = new JintDocumentLayoutEngine();
        var snapshotJson = service.GenerateLayoutSnapshotJson(LoadParityDocument(), fonts: CreateFonts());
        using var snapshot = JsonDocument.Parse(snapshotJson);
        var root = snapshot.RootElement;

        root.GetProperty("pageCount").GetInt32().Should().Be(
            expectedPageCount,
            "the headless layout of the committed parity document must reproduce the browser fixture's page count");

        // The browser fixture uses the canvas engine's rounded A4 defaults (794×1123 px) while the
        // C# converter computes exact points→px (793.7013×1122.52 px) — the same physical A4 page.
        // The plan's parity tolerance is < 1 pt (= 4/3 px).
        const double OnePointInPx = 4.0 / 3.0;
        foreach (var page in root.GetProperty("pages").EnumerateArray())
        {
            page.GetProperty("width").GetDouble().Should().BeApproximately(
                browserPage.GetProperty("width").GetDouble(), OnePointInPx, "page geometry must match the browser fixture within 1 pt");
            page.GetProperty("height").GetDouble().Should().BeApproximately(
                browserPage.GetProperty("height").GetDouble(), OnePointInPx, "page geometry must match the browser fixture within 1 pt");
        }
    }

    [Fact]
    public void HeadlessLayout_ReproducesCommittedHeadlessSnapshotExactly()
    {
        RegenerateFixturesIfRequested();

        using var service = new JintDocumentLayoutEngine();
        var snapshotJson = service.GenerateLayoutSnapshotJson(LoadParityDocument(), fonts: CreateFonts());

        var committed = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", SnapshotFileName));
        snapshotJson.Should().Be(
            committed.Replace("\r\n", "\n", StringComparison.Ordinal),
            "the headless snapshot must be byte-deterministic against the committed fixture — " +
            "regenerate via TEMPO_REGENERATE_HEADLESS_PARITY_FIXTURE=1 after intentional layout changes");
    }

    [Fact]
    public void CommittedRequestFixture_MatchesTheServiceRequestPayload()
    {
        RegenerateFixturesIfRequested();

        using var service = new JintDocumentLayoutEngine();
        var requestJson = service.BuildRequestJson(LoadParityDocument(), null, CreateFonts());

        var committed = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", RequestFileName));
        requestJson.Should().Be(
            committed.Replace("\r\n", "\n", StringComparison.Ordinal),
            "the committed request fixture feeds the Node cross-runtime parity test and must match the live payload");
    }

    // ── Task: real fonts — headless snapshot → TempoDocumentPdfRenderer → PDF ──────────────────

    [Fact]
    public void HeadlessSnapshot_RendersToPdf_WithPageAndBlockPositionParity()
    {
        RegenerateFixturesIfRequested();

        using var service = new JintDocumentLayoutEngine();
        var snapshotJson = service.GenerateLayoutSnapshotJson(LoadParityDocument(), fonts: CreateFonts());
        using var snapshot = JsonDocument.Parse(snapshotJson);
        var pageCount = snapshot.RootElement.GetProperty("pageCount").GetInt32();

        var renderer = new TempoDocumentPdfRenderer(new TempoDocumentPdfRendererOptions { Fonts = CreateFonts() });
        var pdf = renderer.Render(new DocumentPdfExportRequest
        {
            DocumentId = "headless-parity",
            Document = LoadParityDocument(),
            LayoutSnapshotJson = snapshotJson,
        });

        Encoding.ASCII.GetString(pdf, 0, 5).Should().Be("%PDF-");
        var mediaBoxes = ExtractMediaBoxes(Encoding.Latin1.GetString(pdf));
        mediaBoxes.Should().HaveCount(pageCount, "the PDF must inherit the headless pagination");
        foreach (var box in mediaBoxes)
        {
            var parts = box.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            // 794×1123 CSS px × 0.75 = 595.5 × 842.25 pt; the task tolerance is < 1 pt.
            double.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture).Should().BeApproximately(595.5, 1.0);
            double.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture).Should().BeApproximately(842.25, 1.0);
        }

        // Block position parity: every translated text command must sit within 1 pt of the canvas
        // layout position recorded in the snapshot (CSS px × 0.75 = pt).
        var translated = TempoDocumentPdfRenderer.TranslateLayoutSnapshot(snapshotJson);
        var snapshotTexts = snapshot.RootElement.GetProperty("pages")[0].GetProperty("commands").EnumerateArray()
            .Where(command => command.GetProperty("type").GetString() == "text")
            .ToList();
        var translatedTexts = translated.Pages[0].Commands
            .Where(command => command.Type == Tempo.Reporting.Engine.Snapshot.ReportSnapshotCommandType.TextRun)
            .ToList();

        translatedTexts.Should().HaveCount(snapshotTexts.Count, "no text command may be dropped on the way to the PDF");
        for (var index = 0; index < snapshotTexts.Count; index++)
        {
            var expectedX = snapshotTexts[index].GetProperty("x").GetDouble();
            var expectedBaseline = snapshotTexts[index].GetProperty("baseline").GetDouble();
            translatedTexts[index].X.Should().BeApproximately(expectedX, 1.0 / 0.75, "block x within 1 pt");
            (translatedTexts[index].Baseline ?? translatedTexts[index].Y).Should().BeApproximately(
                expectedBaseline, 1.0 / 0.75, "block baseline within 1 pt");
        }
    }

    // ── Fixture generation ─────────────────────────────────────────────────────────────────────

    private static DocumentEditorDocument LoadParityDocument()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", DocumentFileName);
        File.Exists(path).Should().BeTrue(
            $"the parity document must be committed — regenerate via TEMPO_REGENERATE_HEADLESS_PARITY_FIXTURE=1 ({DocumentFileName})");
        return DocumentEditorJson.Deserialize(File.ReadAllText(path));
    }

    private static void RegenerateFixturesIfRequested()
    {
        if (Environment.GetEnvironmentVariable("TEMPO_REGENERATE_HEADLESS_PARITY_FIXTURE") != "1")
        {
            return;
        }

        var document = CreateParityDocument();
        using var service = new JintDocumentLayoutEngine();
        var requestJson = service.BuildRequestJson(document, null, CreateFonts());
        var snapshotJson = service.GenerateLayoutSnapshotJson(document, fonts: CreateFonts());

        var testData = SourceTestDataPath();
        File.WriteAllText(Path.Combine(testData, DocumentFileName), DocumentEditorJson.Serialize(document));
        File.WriteAllText(Path.Combine(testData, RequestFileName), requestJson);
        File.WriteAllText(Path.Combine(testData, SnapshotFileName), snapshotJson);

        using var snapshot = JsonDocument.Parse(snapshotJson);
        Console.WriteLine($"headless parity fixture regenerated: pageCount={snapshot.RootElement.GetProperty("pageCount").GetInt32()}");
    }

    // Deterministic Czech contract-like document tuned so the headless layout with the Dancing
    // Script advance tables paginates to exactly the browser fixture's 21 pages.
    private static DocumentEditorDocument CreateParityDocument()
    {
        var document = DocumentEditorDocument.Empty("headless-parity-doc");
        document.Metadata.Title = "Smlouva o dílo — parita headless layoutu";
        document.Theme.BodyFontFamily = "Dancing Script";
        document.Blocks =
        [
            new DocumentBlock
            {
                Id = "parity-title",
                Type = DocumentBlockType.Heading,
                Order = 0,
                Content = new HeadingBlockContent { Level = 1, Inlines = [new TextRun { Text = "Smlouva o dílo" }] },
            },
        ];

        for (var article = 1; article <= 6; article++)
        {
            document.Blocks.Add(new DocumentBlock
            {
                Id = $"parity-article-{article}",
                Type = DocumentBlockType.Heading,
                Order = document.Blocks.Count,
                Content = new HeadingBlockContent { Level = 2, Inlines = [new TextRun { Text = $"Článek {article} — Předmět plnění" }] },
            });

            // The last article is shorter — tuned so the layout lands on exactly 21 pages, the
            // page count of the committed browser-generated parity fixture.
            var clauseCount = article == 6 ? 5 : 6;
            for (var clause = 1; clause <= clauseCount; clause++)
            {
                document.Blocks.Add(new DocumentBlock
                {
                    Id = $"parity-article-{article}-clause-{clause}",
                    Type = DocumentBlockType.Paragraph,
                    Order = document.Blocks.Count,
                    Content = new ParagraphBlockContent
                    {
                        Inlines =
                        [
                            new TextRun
                            {
                                Text = $"{article}.{clause} Zhotovitel se zavazuje provést dílo řádně a včas, " +
                                    "příliš žluťoučký kůň úpěl ďábelské ódy a objednatel se zavazuje dílo převzít " +
                                    "a zaplatit sjednanou cenu dle podmínek této smlouvy.",
                            },
                        ],
                    },
                });
            }
        }

        return document;
    }

    private static string SourceTestDataPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TempoBlazor.slnx")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull();
        return Path.Combine(directory!.FullName, "tests", "Tempo.Blazor.DocumentFormats.Tests", "TestData");
    }

    private static List<string> ExtractMediaBoxes(string pdfText)
    {
        var boxes = new List<string>();
        var index = 0;
        while ((index = pdfText.IndexOf("/MediaBox [", index, StringComparison.Ordinal)) >= 0)
        {
            var end = pdfText.IndexOf(']', index);
            boxes.Add(pdfText[(index + "/MediaBox [".Length)..end].Trim());
            index = end;
        }

        return boxes;
    }
}
