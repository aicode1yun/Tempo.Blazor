using System.Collections.Concurrent;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentFormats.HeadlessLayout;
using Tempo.Reporting.Engine.Pdf;

namespace Tempo.Blazor.DocumentFormats.Tests;

/// <summary>
/// Phase 2 of the headless document runtime: ITempoDocumentLayoutService hosted in Jint. The
/// service lays out a DocumentEditorDocument with the SAME JS chain the canvas editor paints
/// with (embedded bundle) and font-accurate Skia advance tables, producing the schema v1 layout
/// snapshot JSON — the exact contract of DocumentPdfExportRequest.LayoutSnapshotJson.
/// </summary>
public class JintDocumentLayoutEngineTests
{
    private static readonly string FontPath =
        Path.Combine(AppContext.BaseDirectory, "TestData", "Fonts", "DancingScript-VariableFont_wght.ttf");

    private static IReadOnlyList<ReportPdfFontFace> CreateFonts()
        => [new ReportPdfFontFace("Dancing Script", 400, "normal", File.ReadAllBytes(FontPath))];

    private static DocumentEditorDocument CreateDocument()
    {
        var document = DocumentEditorDocument.Empty("headless-layout-doc");
        document.Theme.BodyFontFamily = "Dancing Script";
        document.Blocks =
        [
            new DocumentBlock
            {
                Type = DocumentBlockType.Heading,
                Order = 0,
                Content = new HeadingBlockContent { Level = 1, Inlines = [new TextRun { Text = "Smlouva o dílo" }] },
            },
            new DocumentBlock
            {
                Type = DocumentBlockType.Paragraph,
                Order = 1,
                Content = new ParagraphBlockContent
                {
                    Inlines =
                    [
                        new TextRun { Text = "Příliš žluťoučký kůň úpěl ďábelské ódy. " },
                        new TextRun { Text = "Tučný text", Marks = [new InlineMark { Type = InlineMarkType.Bold }] },
                    ],
                },
            },
        ];
        return document;
    }

    // ── Contract: document + fonts → schema v1 snapshot JSON ───────────────────────────────────

    [Fact]
    public void GenerateLayoutSnapshotJson_ProducesSchemaV1SnapshotWithPagesAndText()
    {
        using var service = new JintDocumentLayoutEngine();

        var json = service.GenerateLayoutSnapshotJson(CreateDocument(), fonts: CreateFonts());

        using var snapshot = JsonDocument.Parse(json);
        var root = snapshot.RootElement;
        root.GetProperty("schemaVersion").GetInt32().Should().Be(1);
        root.GetProperty("pageCount").GetInt32().Should().BeGreaterThanOrEqualTo(1);

        var pages = root.GetProperty("pages");
        pages.GetArrayLength().Should().Be(root.GetProperty("pageCount").GetInt32());
        // A4 portrait: 595.276 pt × 96/72 ≈ 793.7 px.
        pages[0].GetProperty("width").GetDouble().Should().BeApproximately(793.7, 0.5);
        pages[0].GetProperty("height").GetDouble().Should().BeApproximately(1122.5, 0.5);

        var texts = pages[0].GetProperty("commands").EnumerateArray()
            .Where(command => command.GetProperty("type").GetString() == "text")
            .ToList();
        texts.Should().NotBeEmpty("body text must reach the snapshot");
        texts.Should().Contain(
            command => command.GetProperty("text").GetString() == "Smlouva",
            "layout text is word-segmented, the heading's first word must be present");
    }

    [Fact]
    public void GenerateLayoutSnapshotJson_IsDeterministicAcrossCallsAndEngines()
    {
        using var service = new JintDocumentLayoutEngine();

        var first = service.GenerateLayoutSnapshotJson(CreateDocument(), fonts: CreateFonts());
        var second = service.GenerateLayoutSnapshotJson(CreateDocument(), fonts: CreateFonts());

        second.Should().Be(first);
    }

    [Fact]
    public void GenerateLayoutSnapshotJson_AppliesPageSetupOverride()
    {
        using var service = new JintDocumentLayoutEngine();
        var pageSetup = new DocumentPdfPageSetupOptions
        {
            PageSize = DocumentPageSize.Letter,
            Orientation = DocumentPdfPageOrientation.Landscape,
            Margins = new DocumentPageMargins { Top = 36, Right = 36, Bottom = 36, Left = 36 },
        };

        var json = service.GenerateLayoutSnapshotJson(CreateDocument(), pageSetup, CreateFonts());

        using var snapshot = JsonDocument.Parse(json);
        var page = snapshot.RootElement.GetProperty("pages")[0];
        // Letter landscape: 792 × 612 pt → 1056 × 816 px.
        page.GetProperty("width").GetDouble().Should().BeApproximately(1056, 0.5);
        page.GetProperty("height").GetDouble().Should().BeApproximately(816, 0.5);
    }

    // ── Fail-closed error states with diagnostics ──────────────────────────────────────────────

    [Fact]
    public void GenerateLayoutSnapshotJson_NullDocument_Throws()
    {
        using var service = new JintDocumentLayoutEngine();

        var act = () => service.GenerateLayoutSnapshotJson(null!, fonts: CreateFonts());

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GenerateLayoutSnapshotJson_WithoutFonts_FailsClosed()
    {
        using var service = new JintDocumentLayoutEngine();

        var act = () => service.GenerateLayoutSnapshotJson(CreateDocument());

        act.Should().Throw<TempoDocumentLayoutException>()
            .Which.Message.Should().Contain("font", "layout without embedded fonts cannot be WYSIWYG-accurate");
    }

    [Fact]
    public void GenerateLayoutSnapshotJson_UnknownDocumentFont_FailsClosedWithDiagnostics()
    {
        using var service = new JintDocumentLayoutEngine();
        var document = CreateDocument();
        document.Theme.BodyFontFamily = "Nonexistent Face";

        var act = () => service.GenerateLayoutSnapshotJson(document, fonts: CreateFonts());

        act.Should().Throw<TempoDocumentLayoutException>()
            .Which.UnknownFontFamilies.Should().Contain("Nonexistent Face");
    }

    [Fact]
    public void GenerateLayoutSnapshotJson_GlyphOutsideFontCoverage_FailsClosedWithDiagnostics()
    {
        using var service = new JintDocumentLayoutEngine();
        var document = CreateDocument();
        ((ParagraphBlockContent)document.Blocks[1].Content).Inlines.Add(new TextRun { Text = "漢字" });

        var act = () => service.GenerateLayoutSnapshotJson(document, fonts: CreateFonts());

        act.Should().Throw<TempoDocumentLayoutException>()
            .Which.MissingGlyphs.Should().Contain(glyph => glyph.CodePoint == 0x6f22);
    }

    // ── Engine pooling ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void SequentialCalls_ReuseOnePooledEngine()
    {
        using var service = new JintDocumentLayoutEngine();

        for (var i = 0; i < 4; i++)
        {
            service.GenerateLayoutSnapshotJson(CreateDocument(), fonts: CreateFonts());
        }

        service.CreatedEngineCount.Should().Be(1, "sequential traffic must never allocate a Jint engine per call");
    }

    [Fact]
    public void ConcurrentCalls_AllSucceedAndEngineCountStaysBoundedByParallelism()
    {
        // Retention must cover the test's parallelism: the default (processor count) is lower on
        // small CI machines, where returned engines get disposed and later waves create new ones
        // — the "engines ≤ parallelism" invariant is a property of a sufficiently-retaining pool.
        const int Threads = 8;
        using var service = new JintDocumentLayoutEngine(maxRetainedEngines: Threads);
        const int CallsPerThread = 3;
        var snapshots = new ConcurrentBag<string>();

        Parallel.For(0, Threads, new ParallelOptions { MaxDegreeOfParallelism = Threads }, _ =>
        {
            for (var call = 0; call < CallsPerThread; call++)
            {
                snapshots.Add(new Func<string>(() =>
                    // Fonts and document are rebuilt per call — the service must be safely reentrant.
                    ServiceCall()).Invoke());
            }
        });

        snapshots.Should().HaveCount(Threads * CallsPerThread);
        snapshots.Distinct().Should().HaveCount(1, "concurrent layouts of the same document must be identical");
        service.CreatedEngineCount.Should().BeLessThanOrEqualTo(Threads, "the pool must bound engines by concurrency, not by call count");
        service.CreatedEngineCount.Should().BeGreaterThan(0);

        string ServiceCall() => service.GenerateLayoutSnapshotJson(CreateDocument(), fonts: CreateFonts());
    }

    // ── DI ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void AddTempoDocumentLayout_RegistersTheSingletonService()
    {
        var services = new ServiceCollection();

        services.AddTempoDocumentLayout();

        var descriptor = services.Should().ContainSingle(
                item => item.ServiceType == typeof(ITempoDocumentLayoutService)).Subject;
        descriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);
        descriptor.ImplementationType.Should().Be(typeof(JintDocumentLayoutEngine));

        services.AddTempoDocumentLayout();
        services.Count(item => item.ServiceType == typeof(ITempoDocumentLayoutService))
            .Should().Be(1, "registration must be idempotent (TryAdd)");
    }
}
