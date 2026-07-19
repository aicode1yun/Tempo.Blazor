using System.Text;
using System.Text.Json;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentFormats.Docx;
using Tempo.Blazor.DocumentFormats.HeadlessLayout;
using Tempo.Blazor.Mcp.DocumentEditor;
using Tempo.Blazor.Mcp.Tests.Fixtures;
using Tempo.Reporting.Engine.Pdf;

namespace Tempo.Blazor.Mcp.Tests;

public class DocumentEditorDiffToolsTests
{
    private static readonly string FontPath =
        Path.Combine(AppContext.BaseDirectory, "TestData", "Fonts", "DancingScript-VariableFont_wght.ttf");

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void DiffTools_AreRegisteredInDocumentEditorToolTypes()
    {
        TempoDocumentEditorMcp.ToolTypes.Should().Contain(typeof(DocumentEditorDiffTools));
    }

    // ---------------------------------------------------------------- diff_versions

    [Fact]
    public async Task DiffVersions_VersionVsCurrent_ReturnsStructuredChanges()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildDocument("doc-diff", "Nájemné činí 15000 Kč.");
        provider.Add(doc);
        var version = await provider.CreateVersionAsync(new DocumentVersionCreateRequest
        {
            DocumentId = doc.DocumentId,
            Kind = DocumentVersionKind.Major,
            Label = "v1"
        });

        // Mutate the current state: change text + add a block.
        ((TextRun)((ParagraphBlockContent)doc.Blocks[0].Content).Inlines[0]).Text = "Nájemné činí 18000 Kč.";
        doc.Blocks.Add(new DocumentBlock
        {
            Id = "p2",
            Type = DocumentBlockType.Paragraph,
            Order = 1,
            Content = new ParagraphBlockContent { Inlines = [new TextRun { Text = "Kauce činí 36000 Kč." }] }
        });

        var root = Parse(await DocumentEditorDiffTools.DiffVersions(
            provider, doc.DocumentId, baseVersionId: version.Id));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        var summary = root.GetProperty("summary");
        summary.GetProperty("hasChanges").GetBoolean().Should().BeTrue();
        summary.GetProperty("changedBlocks").GetInt32().Should().Be(1);
        summary.GetProperty("addedBlocks").GetInt32().Should().Be(1);
        root.GetProperty("redlineAvailable").GetBoolean().Should().BeTrue();

        var changes = root.GetProperty("changes").EnumerateArray().ToList();
        var changed = changes.Single(c => c.GetProperty("kind").GetString() == "changed");
        changed.GetProperty("blockId").GetString().Should().Be("p1");
        changed.GetProperty("oldText").GetString().Should().Contain("15000");
        changed.GetProperty("newText").GetString().Should().Contain("18000");
        var segments = changed.GetProperty("textDiff").EnumerateArray().ToList();
        segments.Should().Contain(s => s.GetProperty("kind").GetString() == "added" && s.GetProperty("text").GetString()!.Contains("18000"));
        segments.Should().Contain(s => s.GetProperty("kind").GetString() == "removed" && s.GetProperty("text").GetString()!.Contains("15000"));

        changes.Should().Contain(c => c.GetProperty("kind").GetString() == "added");
    }

    [Fact]
    public async Task DiffVersions_TwoVersions_ComparesSnapshots()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildDocument("doc-diff-two", "Verze jedna.");
        provider.Add(doc);
        var v1 = await provider.CreateVersionAsync(new DocumentVersionCreateRequest
        {
            DocumentId = doc.DocumentId,
            Kind = DocumentVersionKind.Major,
            Label = "v1"
        });

        ((TextRun)((ParagraphBlockContent)doc.Blocks[0].Content).Inlines[0]).Text = "Verze dva.";
        // Versions snapshot the SAVED state — persist the mutation before versioning it.
        var save = await provider.SaveAsync(new DocumentEditorSaveRequest
        {
            DocumentId = doc.DocumentId,
            Document = doc,
            JsonSnapshot = DocumentEditorJson.Serialize(doc),
            ConcurrencyMode = DocumentEditorConcurrencyMode.Force,
            NormalizeJson = true
        });
        save.Success.Should().BeTrue();
        var v2 = await provider.CreateVersionAsync(new DocumentVersionCreateRequest
        {
            DocumentId = doc.DocumentId,
            Kind = DocumentVersionKind.Major,
            Label = "v2"
        });

        var root = Parse(await DocumentEditorDiffTools.DiffVersions(
            provider, doc.DocumentId, baseVersionId: v1.Id, compareVersionId: v2.Id));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        root.GetProperty("summary").GetProperty("changedBlocks").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task DiffVersions_IdenticalVersions_ReturnsEmptyDiffWithoutRedlineOffer()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildDocument("doc-diff-same", "Beze změny.");
        provider.Add(doc);
        var version = await provider.CreateVersionAsync(new DocumentVersionCreateRequest
        {
            DocumentId = doc.DocumentId,
            Kind = DocumentVersionKind.Major,
            Label = "v1"
        });

        var root = Parse(await DocumentEditorDiffTools.DiffVersions(
            provider, doc.DocumentId, baseVersionId: version.Id));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        root.GetProperty("summary").GetProperty("hasChanges").GetBoolean().Should().BeFalse();
        root.GetProperty("changes").EnumerateArray().Should().BeEmpty();
        root.GetProperty("redlineAvailable").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task DiffVersions_MissingVersion_ReturnsNotFound()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildDocument("doc-diff-nover", "Text.");
        provider.Add(doc);

        var root = Parse(await DocumentEditorDiffTools.DiffVersions(
            provider, doc.DocumentId, baseVersionId: "missing-version"));

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetString().Should().Be("not_found");
    }

    [Fact]
    public async Task DiffVersions_MissingDocument_ReturnsNotFound()
    {
        var provider = new FakeDocumentEditorProvider();

        var root = Parse(await DocumentEditorDiffTools.DiffVersions(provider, "missing", "v1"));

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetString().Should().Be("not_found");
    }

    // ---------------------------------------------------------------- export_redline

    [Fact]
    public async Task ExportRedline_Docx_ProducesTrackedChangesReadableByImporter()
    {
        var (provider, renderer, catalog, options) = CreateRuntime();
        var doc = BuildDocument("doc-redline-docx", "Původní znění odstavce.");
        provider.Add(doc);
        var version = await provider.CreateVersionAsync(new DocumentVersionCreateRequest
        {
            DocumentId = doc.DocumentId,
            Kind = DocumentVersionKind.Major,
            Label = "v1"
        });
        ((TextRun)((ParagraphBlockContent)doc.Blocks[0].Content).Inlines[0]).Text = "Nové znění odstavce.";

        var root = Parse(await DocumentEditorDiffTools.ExportRedline(
            provider, renderer, catalog, options, doc.DocumentId, baseVersionId: version.Id,
            format: "docx", authorName: "MCP Agent"));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        root.GetProperty("contentType").GetString().Should().Contain("wordprocessingml");
        var bytes = Convert.FromBase64String(root.GetProperty("contentBase64").GetString()!);
        bytes[0].Should().Be(0x50);
        bytes[1].Should().Be(0x4B);

        // The DOCX importer reads w:ins/w:del back as revisions — proves real tracked changes.
        using var stream = new MemoryStream(bytes);
        var imported = await new DocumentDocxImporter().ImportAsync(stream);
        imported.Document.Revisions.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ExportRedline_Pdf_RendersWithReviewMarkup()
    {
        var (provider, renderer, catalog, options) = CreateRuntime();
        var doc = BuildDocument("doc-redline-pdf", "Původní text smlouvy.");
        provider.Add(doc);
        var version = await provider.CreateVersionAsync(new DocumentVersionCreateRequest
        {
            DocumentId = doc.DocumentId,
            Kind = DocumentVersionKind.Major,
            Label = "v1"
        });
        ((TextRun)((ParagraphBlockContent)doc.Blocks[0].Content).Inlines[0]).Text = "Upravený text smlouvy.";

        var root = Parse(await DocumentEditorDiffTools.ExportRedline(
            provider, renderer, catalog, options, doc.DocumentId, baseVersionId: version.Id, format: "pdf"));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        root.GetProperty("contentType").GetString().Should().Be("application/pdf");
        var pdf = Convert.FromBase64String(root.GetProperty("contentBase64").GetString()!);
        Encoding.ASCII.GetString(pdf, 0, 5).Should().Be("%PDF-");
        root.GetProperty("pageCount").GetInt32().Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task ExportRedline_IdenticalVersions_ReturnsInvalidOperation()
    {
        var (provider, renderer, catalog, options) = CreateRuntime();
        var doc = BuildDocument("doc-redline-same", "Beze změny.");
        provider.Add(doc);
        var version = await provider.CreateVersionAsync(new DocumentVersionCreateRequest
        {
            DocumentId = doc.DocumentId,
            Kind = DocumentVersionKind.Major,
            Label = "v1"
        });

        var root = Parse(await DocumentEditorDiffTools.ExportRedline(
            provider, renderer, catalog, options, doc.DocumentId, baseVersionId: version.Id, format: "docx"));

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetString().Should().Be("invalid_operation");
        root.GetProperty("message").GetString().Should().Contain("identical", "agent must learn there is nothing to redline");
    }

    [Fact]
    public async Task ExportRedline_UnknownFormat_ReturnsInvalidOperation()
    {
        var (provider, renderer, catalog, options) = CreateRuntime();
        var doc = BuildDocument("doc-redline-fmt", "Text.");
        provider.Add(doc);
        var version = await provider.CreateVersionAsync(new DocumentVersionCreateRequest
        {
            DocumentId = doc.DocumentId,
            Kind = DocumentVersionKind.Major,
            Label = "v1"
        });

        var root = Parse(await DocumentEditorDiffTools.ExportRedline(
            provider, renderer, catalog, options, doc.DocumentId, baseVersionId: version.Id, format: "html"));

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetString().Should().Be("invalid_operation");
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
        doc.Metadata.Title = documentId;
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
}
