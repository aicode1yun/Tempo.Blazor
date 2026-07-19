using System.Text;
using System.Text.Json;
using FluentAssertions;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;
using Tempo.Blazor.DocumentFormats.HeadlessLayout;
using Tempo.Blazor.DocumentFormats.Pdf;
using Tempo.Reporting.Engine.Pdf;

namespace Tempo.Blazor.DocumentFormats.Tests;

/// <summary>
/// Phase 4 of the headless document runtime: the TempoDocumentService facade. One call takes a
/// template (or plain document) plus token values and produces a WYSIWYG PDF — assembly
/// (IF/ELSE chains, repeating sections, computed expressions) → headless layout → vector PDF —
/// or per-page PNG previews at a parametrizable DPI. The clock is injectable, so assembly
/// functions (TODAY/DATEADD) and the forensic watermark timestamp are deterministic.
/// </summary>
public class TempoDocumentServiceTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 1, 15, 10, 30, 0, TimeSpan.Zero);

    private static readonly string FontPath =
        Path.Combine(AppContext.BaseDirectory, "TestData", "Fonts", "DancingScript-VariableFont_wght.ttf");

    private static IReadOnlyList<ReportPdfFontFace> CreateFonts()
        => [new ReportPdfFontFace("Dancing Script", 400, "normal", File.ReadAllBytes(FontPath))];

    private static TempoDocumentService CreateService(DateTimeOffset? now = null)
        => new(new JintDocumentLayoutEngine(), new FixedTimeProvider(now ?? FixedNow));

    // ── RenderPdfAsync: assembly → layout → PDF ────────────────────────────────────────────────

    [Fact]
    public async Task RenderPdfAsync_AssemblesTokensConditionalsRepeatsAndComputedValues()
    {
        var service = CreateService();

        var result = await service.RenderPdfAsync(new TempoDocumentRenderRequest
        {
            Document = CreateAssemblyTemplate(),
            TokenValues = CreateDataset(amount: 25000, ("Servis A", "15000"), ("Servis B", "10000")),
            Fonts = CreateFonts(),
        });

        Encoding.ASCII.GetString(result.PdfContent, 0, 5).Should().Be("%PDF-");
        result.PageCount.Should().BeGreaterThanOrEqualTo(1);
        result.LayoutSnapshotJson.Should().NotBeNullOrWhiteSpace();

        var text = SnapshotText(result.LayoutSnapshotJson);
        text.Should().Contain("Acme", "token values must be resolved into the layout");
        text.Should().Contain("ředitele", "amount 25000 must take the IF branch (> 10000)");
        text.Should().NotContain("běžném", "the ELSE branch must be dropped");
        // Layout text is word-segmented — assert single words per row.
        text.Should().Contain("Servis", "repeating section rows must be expanded");
        text.Should().Contain("15000", "the first row's price must be expanded");
        text.Should().Contain("10000", "the second row's price must be expanded");
        text.Should().Contain("Kč", "the computed currency total must be rendered");
        text.Should().Contain("2026-01-29", "DATEADD(TODAY(), 14) over the injected 2026-01-15 clock");
    }

    [Fact]
    public async Task RenderPdfAsync_ElseBranchWinsForSmallAmounts()
    {
        var service = CreateService();

        var result = await service.RenderPdfAsync(new TempoDocumentRenderRequest
        {
            Document = CreateAssemblyTemplate(),
            TokenValues = CreateDataset(amount: 5000, ("Servis C", "5000")),
            Fonts = CreateFonts(),
        });

        var text = SnapshotText(result.LayoutSnapshotJson);
        text.Should().Contain("běžném", "amount 5000 must take the ELSE branch");
        text.Should().NotContain("ředitele");
    }

    [Fact]
    public async Task RenderPdfAsync_WithoutTokenValues_RendersThePlainDocument()
    {
        var service = CreateService();
        var document = DocumentEditorDocument.Empty("plain-doc");
        document.Theme.BodyFontFamily = "Dancing Script";
        document.Blocks =
        [
            new DocumentBlock
            {
                Type = DocumentBlockType.Paragraph,
                Order = 0,
                Content = new ParagraphBlockContent { Inlines = [new TextRun { Text = "Prostý dokument bez šablony." }] },
            },
        ];

        var result = await service.RenderPdfAsync(new TempoDocumentRenderRequest
        {
            Document = document,
            Fonts = CreateFonts(),
        });

        SnapshotText(result.LayoutSnapshotJson).Should().Contain("Prostý");
        result.PageCount.Should().Be(1);
    }

    [Fact]
    public async Task RenderPdfAsync_InjectedClock_DrivesAssemblyDateFunctions()
    {
        var januaryResult = await CreateService(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)).RenderPdfAsync(CreateDueDateRequest());
        var juneResult = await CreateService(new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero)).RenderPdfAsync(CreateDueDateRequest());

        SnapshotText(januaryResult.LayoutSnapshotJson).Should().NotBe(
            SnapshotText(juneResult.LayoutSnapshotJson),
            "DATEADD(TODAY(), 14) must follow the injected clock");

        static TempoDocumentRenderRequest CreateDueDateRequest() => new()
        {
            Document = CreateAssemblyTemplate(),
            TokenValues = CreateDataset(amount: 25000, ("Servis A", "1000")),
            Fonts = CreateFonts(),
        };
    }

    [Fact]
    public async Task RenderPdfAsync_ForensicWatermark_UsesInjectedClockWhenTimestampMissing()
    {
        var service = CreateService();

        var result = await service.RenderPdfAsync(new TempoDocumentRenderRequest
        {
            Document = CreateAssemblyTemplate(),
            TokenValues = CreateDataset(amount: 25000, ("Servis A", "1000")),
            Fonts = CreateFonts(),
            Options = new DocumentPdfExportOptions
            {
                ForensicWatermark = new DocumentPdfForensicWatermarkOptions { UserName = "Auditor", IpAddress = "10.0.0.7" },
            },
        });

        // The forensic stamp is applied on the way to the PDF snapshot — verify through the
        // renderer's own translation of the facade result.
        var renderer = new TempoDocumentPdfRenderer(new TempoDocumentPdfRendererOptions { Fonts = CreateFonts() });
        var snapshot = renderer.BuildReportSnapshot(new DocumentPdfExportRequest
        {
            Document = DocumentEditorDocument.Empty(),
            LayoutSnapshotJson = result.LayoutSnapshotJson,
            Options = new DocumentPdfExportOptions
            {
                ForensicWatermark = new DocumentPdfForensicWatermarkOptions
                {
                    UserName = "Auditor",
                    IpAddress = "10.0.0.7",
                    Timestamp = FixedNow,
                },
            },
        });
        var stamp = snapshot.Pages[0].Commands.Select(command => command.Text).FirstOrDefault(text => text?.Contains("Auditor") == true);

        stamp.Should().NotBeNull();
        Encoding.ASCII.GetString(result.PdfContent, 0, 5).Should().Be("%PDF-");
        result.ForensicTimestamp.Should().Be(FixedNow, "a missing forensic timestamp must be stamped from the injected clock");
    }

    // ── RenderPageImagesAsync: per-page PNG at parametrizable DPI ──────────────────────────────

    [Fact]
    public async Task RenderPageImagesAsync_ProducesOnePngPerPageAtRequestedDpi()
    {
        var service = CreateService();
        var request = new TempoDocumentRenderRequest
        {
            Document = CreateAssemblyTemplate(),
            TokenValues = CreateDataset(amount: 25000, ("Servis A", "15000")),
            Fonts = CreateFonts(),
        };

        var baseline = await service.RenderPageImagesAsync(request);
        var highDpi = await service.RenderPageImagesAsync(request, dpi: 192);

        baseline.Should().NotBeEmpty();
        baseline.Should().HaveCount(highDpi.Count);
        foreach (var (image, index) in baseline.Select((image, index) => (image, index)))
        {
            image.PageIndex.Should().Be(index);
            image.Png[..8].Should().Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, "output must be PNG");
            highDpi[index].Width.Should().Be(image.Width * 2, "192 dpi doubles the 96 dpi raster");
            highDpi[index].Height.Should().Be(image.Height * 2);
        }
    }

    [Fact]
    public async Task RenderPageImagesAsync_InvalidDpi_Throws()
    {
        var service = CreateService();
        var act = () => service.RenderPageImagesAsync(new TempoDocumentRenderRequest
        {
            Document = CreateAssemblyTemplate(),
            TokenValues = CreateDataset(amount: 25000, ("Servis A", "1000")),
            Fonts = CreateFonts(),
        }, dpi: 0);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    // ── DI ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void AddTempoDocumentServices_RegistersFacadeAndLayoutSingletons()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();

        TempoDocumentServiceCollectionExtensions.AddTempoDocumentServices(services);

        services.Should().ContainSingle(item => item.ServiceType == typeof(ITempoDocumentService));
        services.Should().ContainSingle(item => item.ServiceType == typeof(ITempoDocumentLayoutService));

        TempoDocumentServiceCollectionExtensions.AddTempoDocumentServices(services);
        services.Count(item => item.ServiceType == typeof(ITempoDocumentService)).Should().Be(1, "registration is idempotent");
    }

    // ── Fixtures ───────────────────────────────────────────────────────────────────────────────

    private static string SnapshotText(string layoutSnapshotJson)
    {
        using var snapshot = JsonDocument.Parse(layoutSnapshotJson);
        var builder = new StringBuilder();
        foreach (var page in snapshot.RootElement.GetProperty("pages").EnumerateArray())
        {
            foreach (var command in page.GetProperty("commands").EnumerateArray())
            {
                if (command.GetProperty("type").GetString() == "text" && command.TryGetProperty("text", out var text))
                {
                    builder.Append(text.GetString()).Append(' ');
                }
            }
        }

        return builder.ToString();
    }

    private static Dictionary<string, DocumentTokenValue> CreateDataset(int amount, params (string Name, string Price)[] items)
        => new()
        {
            ["contract.client"] = DocumentTokenValue.Resolved("contract.client", "Acme s.r.o."),
            ["contract.amount"] = DocumentTokenValue.Resolved("contract.amount", amount.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ["items"] = new DocumentTokenValue
            {
                Key = "items",
                HasValue = true,
                Rows = items.Select(item => new Dictionary<string, string?>
                {
                    ["name"] = item.Name,
                    ["price"] = item.Price,
                }).ToList(),
            },
        };

    // Mirrors the demo assembly contract template: IF/ELSE over contract.amount, a repeating
    // items section, a computed currency total and a DATEADD due date.
    private static DocumentEditorDocument CreateAssemblyTemplate()
    {
        var document = DocumentEditorDocument.Empty("facade-assembly-template");
        document.Theme.BodyFontFamily = "Dancing Script";
        var sectionId = document.Sections[0].Id;

        DocumentBlock Paragraph(string id, params InlineContent[] inlines) => new()
        {
            Id = id,
            SectionId = sectionId,
            Type = DocumentBlockType.Paragraph,
            Content = new ParagraphBlockContent { Inlines = inlines.ToList() },
        };

        document.Blocks =
        [
            new DocumentBlock
            {
                Id = "facade-heading",
                SectionId = sectionId,
                Type = DocumentBlockType.Heading,
                Order = 1,
                Content = new HeadingBlockContent { Level = 1, Inlines = [new TextRun { Text = "Smlouva o dílo" }] },
            },
            Paragraph(
                "facade-client",
                new TextRun { Text = "Objednatel: " },
                new TokenRun { Key = "contract.client", DisplayName = "Objednatel" }),
            new DocumentBlock
            {
                Id = "facade-if",
                SectionId = sectionId,
                Type = DocumentBlockType.ContentControl,
                Order = 3,
                Content = new ContentControlBlockContent
                {
                    Control = DocumentAssemblyMetadata.CreateConditionalBlock("if", "contract.amount > 10000", "facade-approval"),
                    Blocks = [Paragraph("facade-if-clause", new TextRun { Text = "Smlouva podléhá schválení ředitele." })],
                },
            },
            new DocumentBlock
            {
                Id = "facade-else",
                SectionId = sectionId,
                Type = DocumentBlockType.ContentControl,
                Order = 4,
                Content = new ContentControlBlockContent
                {
                    Control = DocumentAssemblyMetadata.CreateConditionalBlock("else", null, "facade-approval"),
                    Blocks = [Paragraph("facade-else-clause", new TextRun { Text = "Smlouvu schvaluje vedoucí v běžném režimu." })],
                },
            },
            new DocumentBlock
            {
                Id = "facade-items",
                SectionId = sectionId,
                Type = DocumentBlockType.ContentControl,
                Order = 5,
                Content = new ContentControlBlockContent
                {
                    Control = DocumentAssemblyMetadata.CreateRepeatingSection("items"),
                    Blocks =
                    [
                        Paragraph(
                            "facade-item-row",
                            new TextRun { Text = "Položka: " },
                            new TokenRun { Key = "name", DisplayName = "Položka" },
                            new TextRun { Text = " za " },
                            new TokenRun { Key = "price", DisplayName = "Cena" },
                            new TextRun { Text = " Kč" }),
                    ],
                },
            },
            Paragraph(
                "facade-total",
                new TextRun { Text = "Cena celkem: " },
                new TokenRun { Key = "contract.total", DisplayName = "Celkem", Expression = "CURRENCY(SUM(items, 'price'), 'cs-CZ', 'CZK')" }),
            Paragraph(
                "facade-due",
                new TextRun { Text = "Splatnost: " },
                new TokenRun { Key = "contract.due", DisplayName = "Splatnost", Expression = "DATEADD(TODAY(), 14)" }),
        ];

        return document;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
