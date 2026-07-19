using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentFormats.HeadlessLayout;
using Tempo.Blazor.Mcp.DocumentEditor;
using Tempo.Blazor.Mcp.Tests.Fixtures;
using Tempo.Reporting.Engine.Pdf;

namespace Tempo.Blazor.Mcp.Tests;

public class DocumentEditorRenderToolsTests
{
    private static readonly string FontPath =
        Path.Combine(AppContext.BaseDirectory, "TestData", "Fonts", "DancingScript-VariableFont_wght.ttf");

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void RenderTools_AreRegisteredInDocumentEditorToolTypes()
    {
        TempoDocumentEditorMcp.ToolTypes.Should().Contain(typeof(DocumentEditorRenderTools));
    }

    [Fact]
    public void AddTempoDocumentEditorMcpRendering_RegistersServiceCatalogAndOptions()
    {
        var services = new ServiceCollection();
        services.AddTempoDocumentEditorMcpRendering(options =>
        {
            options.Fonts.Add(new ReportPdfFontFace("Dancing Script", 400, "normal", File.ReadAllBytes(FontPath)));
        });

        using var provider = services.BuildServiceProvider();
        provider.GetService<ITempoDocumentService>().Should().NotBeNull();
        provider.GetService<TempoDocumentMcpRenderOptions>().Should().NotBeNull();
        var catalog = provider.GetRequiredService<ITempoDocumentMcpFontCatalog>();
        catalog.Fonts.Should().Contain(f => f.Family == "Dancing Script");
    }

    [Fact]
    public void FontCatalog_AliasesDuplicateFacesUnderAliasFamily()
    {
        var options = new TempoDocumentMcpRenderOptions { IncludeSystemFontFallback = false };
        options.Fonts.Add(new ReportPdfFontFace("Dancing Script", 400, "normal", File.ReadAllBytes(FontPath)));
        options.FontAliases["Aptos"] = "Dancing Script";

        var catalog = new TempoDocumentMcpFontCatalog(options);

        catalog.Fonts.Should().Contain(f => f.Family == "Dancing Script");
        catalog.Fonts.Should().Contain(f => f.Family == "Aptos");
    }

    [Fact]
    public void FontCatalog_SystemFallback_LoadsSystemFacesWhenAvailable()
    {
        var options = new TempoDocumentMcpRenderOptions { IncludeSystemFontFallback = true };
        var catalog = new TempoDocumentMcpFontCatalog(options);

        // On Windows (Arial) and Linux CI (DejaVu) at least one system face resolves.
        catalog.Fonts.Should().NotBeEmpty();
        catalog.Fonts.Should().Contain(f => f.Family == "Arial");
    }

    // ---------------------------------------------------------------- document_render_preview

    [Fact]
    public async Task RenderPreview_ById_ReturnsPngPages()
    {
        var (provider, renderer, catalog, options) = CreateRuntime();
        var doc = BuildDocument("doc-preview", "Náhled dokumentu pro agenta.");
        provider.Add(doc);

        var root = Parse(await DocumentEditorRenderTools.RenderPreview(
            provider, renderer, catalog, options, documentId: doc.DocumentId));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        root.GetProperty("pageCount").GetInt32().Should().Be(1);
        var pages = root.GetProperty("renderedPages").EnumerateArray().ToList();
        pages.Should().HaveCount(1);
        pages[0].GetProperty("pageNumber").GetInt32().Should().Be(1);
        pages[0].GetProperty("width").GetInt32().Should().BeGreaterThan(100);
        pages[0].GetProperty("contentType").GetString().Should().Be("image/png");
        var png = Convert.FromBase64String(pages[0].GetProperty("base64").GetString()!);
        png[1].Should().Be((byte)'P');
        png[2].Should().Be((byte)'N');
        png[3].Should().Be((byte)'G');
    }

    [Fact]
    public async Task RenderPreview_InlineJson_Works()
    {
        var (provider, renderer, catalog, options) = CreateRuntime();
        var doc = BuildDocument("doc-inline", "Inline JSON náhled.");

        var root = Parse(await DocumentEditorRenderTools.RenderPreview(
            provider, renderer, catalog, options, documentJson: DocumentEditorJson.Serialize(doc)));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        root.GetProperty("renderedPages").EnumerateArray().Should().HaveCount(1);
    }

    [Fact]
    public async Task RenderPreview_PageSelection_ReturnsOnlyRequestedPages()
    {
        var (provider, renderer, catalog, options) = CreateRuntime();
        var doc = BuildMultiPageDocument("doc-pages", pageCount: 3);
        provider.Add(doc);

        var root = Parse(await DocumentEditorRenderTools.RenderPreview(
            provider, renderer, catalog, options, documentId: doc.DocumentId, pages: "2"));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        root.GetProperty("pageCount").GetInt32().Should().Be(3);
        var pages = root.GetProperty("renderedPages").EnumerateArray().ToList();
        pages.Should().HaveCount(1);
        pages[0].GetProperty("pageNumber").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task RenderPreview_MaxPagesCap_TruncatesAndReports()
    {
        var (provider, renderer, catalog, options) = CreateRuntime();
        var doc = BuildMultiPageDocument("doc-cap", pageCount: 3);
        provider.Add(doc);

        var root = Parse(await DocumentEditorRenderTools.RenderPreview(
            provider, renderer, catalog, options, documentId: doc.DocumentId, maxPages: 2));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        root.GetProperty("renderedPages").EnumerateArray().Should().HaveCount(2);
        root.GetProperty("truncated").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task RenderPreview_InvalidDpi_ReturnsValidationFailed()
    {
        var (provider, renderer, catalog, options) = CreateRuntime();
        var doc = BuildDocument("doc-dpi", "x");
        provider.Add(doc);

        var root = Parse(await DocumentEditorRenderTools.RenderPreview(
            provider, renderer, catalog, options, documentId: doc.DocumentId, dpi: 5000));

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetString().Should().Be("validation_failed");
    }

    [Fact]
    public async Task RenderPreview_NeitherIdNorJson_ReturnsValidationFailed()
    {
        var (provider, renderer, catalog, options) = CreateRuntime();

        var root = Parse(await DocumentEditorRenderTools.RenderPreview(provider, renderer, catalog, options));

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetString().Should().Be("validation_failed");
    }

    [Fact]
    public async Task RenderPreview_MissingDocument_ReturnsNotFound()
    {
        var (provider, renderer, catalog, options) = CreateRuntime();

        var root = Parse(await DocumentEditorRenderTools.RenderPreview(
            provider, renderer, catalog, options, documentId: "missing"));

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetString().Should().Be("not_found");
    }

    [Fact]
    public async Task RenderPreview_UnknownFontFamily_FailsClosedWithAgentFriendlyDiagnostics()
    {
        var (provider, renderer, catalog, options) = CreateRuntime();
        var doc = BuildDocument("doc-nofont", "Text bez fontu.");
        doc.Theme.BodyFontFamily = "Totally Unknown Font";
        provider.Add(doc);

        var root = Parse(await DocumentEditorRenderTools.RenderPreview(
            provider, renderer, catalog, options, documentId: doc.DocumentId));

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetString().Should().Be("invalid_operation");
        var message = root.GetProperty("message").GetString()!;
        message.Should().Contain("Totally Unknown Font");
        message.Should().Contain("font", "the agent must learn this is a font-catalog problem");
    }

    [Fact]
    public async Task RenderPreview_EmptyDocument_RendersSingleEmptyPage()
    {
        var (provider, renderer, catalog, options) = CreateRuntime();
        var doc = DocumentEditorDocument.Empty("doc-empty");
        doc.Theme.BodyFontFamily = "Dancing Script";
        provider.Add(doc);

        var root = Parse(await DocumentEditorRenderTools.RenderPreview(
            provider, renderer, catalog, options, documentId: doc.DocumentId));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        root.GetProperty("pageCount").GetInt32().Should().Be(1);
        root.GetProperty("renderedPages").EnumerateArray().Should().HaveCount(1);
    }

    [Fact]
    public async Task RenderPreview_NoFontsConfigured_ReturnsUnsupported()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildDocument("doc-nocatalog", "x");
        provider.Add(doc);
        var emptyOptions = new TempoDocumentMcpRenderOptions { IncludeSystemFontFallback = false };
        var emptyCatalog = new TempoDocumentMcpFontCatalog(emptyOptions);
        var renderer = new TempoDocumentService(new JintDocumentLayoutEngine());

        var root = Parse(await DocumentEditorRenderTools.RenderPreview(
            provider, renderer, emptyCatalog, emptyOptions, documentId: doc.DocumentId));

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetString().Should().Be("unsupported");
    }

    // ---------------------------------------------------------------- document_render_pdf

    [Fact]
    public async Task RenderPdf_ReturnsPdfBase64WithPageCount()
    {
        var (provider, renderer, catalog, options) = CreateRuntime();
        var doc = BuildDocument("doc-pdf", "PDF výstup pro agenta.");
        provider.Add(doc);

        var root = Parse(await DocumentEditorRenderTools.RenderPdf(
            provider, renderer, catalog, options, documentId: doc.DocumentId));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        root.GetProperty("pageCount").GetInt32().Should().Be(1);
        root.GetProperty("contentType").GetString().Should().Be("application/pdf");
        var pdf = Convert.FromBase64String(root.GetProperty("base64").GetString()!);
        Encoding.ASCII.GetString(pdf, 0, 5).Should().Be("%PDF-");
    }

    [Fact]
    public async Task RenderPdf_ExportOptionsPassthrough_AppliesForensicWatermark()
    {
        var (provider, renderer, catalog, options) = CreateRuntime();
        var doc = BuildDocument("doc-pdf-options", "Dokument s vodoznakem.");
        provider.Add(doc);
        const string exportOptions = """
            {"ForensicWatermark": {"UserName": "agent@tempo", "Timestamp": "2026-01-15T10:30:00+00:00"}}
            """;

        var root = Parse(await DocumentEditorRenderTools.RenderPdf(
            provider, renderer, catalog, options, documentId: doc.DocumentId, exportOptionsJson: exportOptions));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        root.GetProperty("forensicTimestamp").GetString().Should().Contain("2026-01-15");
    }

    [Fact]
    public async Task RenderPdf_InvalidExportOptionsJson_ReturnsValidationFailed()
    {
        var (provider, renderer, catalog, options) = CreateRuntime();
        var doc = BuildDocument("doc-pdf-badopts", "x");
        provider.Add(doc);

        var root = Parse(await DocumentEditorRenderTools.RenderPdf(
            provider, renderer, catalog, options, documentId: doc.DocumentId, exportOptionsJson: "{broken"));

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetString().Should().Be("validation_failed");
    }

    [Fact]
    public async Task RenderPdf_MissingDocument_ReturnsNotFound()
    {
        var (provider, renderer, catalog, options) = CreateRuntime();

        var root = Parse(await DocumentEditorRenderTools.RenderPdf(
            provider, renderer, catalog, options, documentId: "missing"));

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetString().Should().Be("not_found");
    }

    // ---------------------------------------------------------------- helpers

    private static (FakeDocumentEditorProvider Provider, ITempoDocumentService Renderer, ITempoDocumentMcpFontCatalog Catalog, TempoDocumentMcpRenderOptions Options) CreateRuntime()
    {
        var options = new TempoDocumentMcpRenderOptions { IncludeSystemFontFallback = false };
        options.Fonts.Add(new ReportPdfFontFace("Dancing Script", 400, "normal", File.ReadAllBytes(FontPath)));
        var catalog = new TempoDocumentMcpFontCatalog(options);
        var renderer = new TempoDocumentService(new JintDocumentLayoutEngine());
        return (new FakeDocumentEditorProvider(), renderer, catalog, options);
    }

    private static DocumentEditorDocument BuildDocument(string documentId, string text)
    {
        var doc = DocumentEditorDocument.Empty(documentId);
        doc.Theme.BodyFontFamily = "Dancing Script";
        doc.Blocks.Add(new DocumentBlock
        {
            Id = "p1",
            Type = DocumentBlockType.Paragraph,
            Order = 0,
            Content = new ParagraphBlockContent { Inlines = [new TextRun { Text = text }] }
        });
        return doc;
    }

    private static DocumentEditorDocument BuildMultiPageDocument(string documentId, int pageCount)
    {
        var doc = DocumentEditorDocument.Empty(documentId);
        doc.Theme.BodyFontFamily = "Dancing Script";
        var order = 0;
        for (var page = 0; page < pageCount; page++)
        {
            doc.Blocks.Add(new DocumentBlock
            {
                Id = $"p{page}",
                Type = DocumentBlockType.Paragraph,
                Order = order++,
                Content = new ParagraphBlockContent { Inlines = [new TextRun { Text = $"Stránka {page + 1}" }] }
            });
            if (page < pageCount - 1)
            {
                doc.Blocks.Add(new DocumentBlock
                {
                    Id = $"br{page}",
                    Type = DocumentBlockType.PageBreak,
                    Order = order++,
                    Content = new PageBreakBlockContent()
                });
            }
        }

        return doc;
    }
}
