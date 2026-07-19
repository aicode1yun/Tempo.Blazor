using System.Text.Json;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.Mcp.DocumentEditor;
using Tempo.Blazor.Mcp.Tests.Fixtures;

namespace Tempo.Blazor.Mcp.Tests;

public class DocumentEditorAuthoringToolsTests
{
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void AuthoringTools_AreRegisteredInDocumentEditorToolTypes()
    {
        TempoDocumentEditorMcp.ToolTypes.Should().Contain(typeof(DocumentEditorAuthoringTools));
    }

    // ---------------------------------------------------------------- create

    [Fact]
    public async Task Create_NewDocument_ReturnsIdTokenAndFirstBlock()
    {
        var provider = new FakeDocumentEditorProvider();

        var root = Parse(await DocumentEditorAuthoringTools.Create(provider, title: "Nájemní smlouva"));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        var id = root.GetProperty("id").GetString();
        id.Should().NotBeNullOrWhiteSpace();
        root.GetProperty("concurrencyToken").GetString().Should().NotBeNullOrWhiteSpace();
        var firstBlockId = root.GetProperty("firstBlockId").GetString();

        var saved = (await provider.LoadAsync(id!)).Document!;
        saved.Metadata.Title.Should().Be("Nájemní smlouva");
        saved.Blocks.Should().ContainSingle().Which.Id.Should().Be(firstBlockId);
    }

    [Fact]
    public async Task Create_WithExplicitIdAndLandscape_AppliesPageSetup()
    {
        var provider = new FakeDocumentEditorProvider();

        var root = Parse(await DocumentEditorAuthoringTools.Create(
            provider, documentId: "doc-created", title: "T", landscape: true));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        root.GetProperty("id").GetString().Should().Be("doc-created");
        (await provider.LoadAsync("doc-created")).Document!.PageSettings.Landscape.Should().BeTrue();
    }

    [Fact]
    public async Task Create_ExistingId_ReturnsValidationFailed()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = DocumentEditorDocument.Empty("doc-exists");
        provider.Add(doc);

        var root = Parse(await DocumentEditorAuthoringTools.Create(provider, documentId: "doc-exists"));

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetString().Should().Be("validation_failed");
        root.GetProperty("message").GetString().Should().Contain("doc-exists");
    }

    [Fact]
    public async Task Create_InvalidPageSettingsJson_ReturnsValidationFailed()
    {
        var provider = new FakeDocumentEditorProvider();

        var root = Parse(await DocumentEditorAuthoringTools.Create(
            provider, pageSettingsJson: "{not json"));

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetString().Should().Be("validation_failed");
    }

    // ---------------------------------------------------------------- import

    [Fact]
    public async Task Import_Markdown_CreatesDocumentWithBlocks()
    {
        var provider = new FakeDocumentEditorProvider();
        const string markdown = "# Smlouva\n\nPrvní odstavec.\n\n- bod jedna\n- bod dva\n";

        var root = Parse(await DocumentEditorAuthoringTools.Import(
            provider, "markdown", markdown, title: "Smlouva"));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        var id = root.GetProperty("id").GetString()!;
        var saved = (await provider.LoadAsync(id)).Document!;
        saved.Blocks.Should().HaveCountGreaterThan(2);
        saved.Blocks[0].Content.Should().BeOfType<HeadingBlockContent>();
        saved.Blocks.Select(b => b.Content).OfType<ListBlockContent>().Should().HaveCount(2);
    }

    [Fact]
    public async Task Import_Markdown_ReplacesExistingDocumentWithToken()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = DocumentEditorDocument.Empty("doc-replace");
        doc.Blocks.Add(new DocumentBlock
        {
            Id = "old",
            Type = DocumentBlockType.Paragraph,
            Content = new ParagraphBlockContent { Inlines = [new TextRun { Text = "Starý obsah" }] }
        });
        provider.Add(doc);

        var root = Parse(await DocumentEditorAuthoringTools.Import(
            provider, "markdown", "Nový obsah.", documentId: "doc-replace", expectedConcurrencyToken: "v1"));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        root.GetProperty("concurrencyToken").GetString().Should().Be("v2");
        var saved = (await provider.LoadAsync("doc-replace")).Document!;
        saved.Blocks.Select(b => b.Id).Should().NotContain("old");
    }

    [Fact]
    public async Task Import_StaleToken_ReturnsConflict()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = DocumentEditorDocument.Empty("doc-import-conflict");
        provider.Add(doc);

        var root = Parse(await DocumentEditorAuthoringTools.Import(
            provider, "markdown", "x", documentId: "doc-import-conflict", expectedConcurrencyToken: "stale"));

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetString().Should().Be("conflict");
    }

    [Fact]
    public async Task Import_Html_CreatesDocument()
    {
        var provider = new FakeDocumentEditorProvider();

        var root = Parse(await DocumentEditorAuthoringTools.Import(
            provider, "html", "<h2>Nadpis</h2><p>Odstavec <strong>tučně</strong>.</p>"));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        var id = root.GetProperty("id").GetString()!;
        var saved = (await provider.LoadAsync(id)).Document!;
        saved.Blocks[0].Content.Should().BeOfType<HeadingBlockContent>();
    }

    [Fact]
    public async Task Import_UnknownFormat_ReturnsInvalidOperationListingFormats()
    {
        var provider = new FakeDocumentEditorProvider();

        var root = Parse(await DocumentEditorAuthoringTools.Import(provider, "pdf", "x"));

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetString().Should().Be("invalid_operation");
        root.GetProperty("message").GetString().Should().Contain("markdown");
    }

    [Fact]
    public async Task Import_Docx_InvalidBase64_ReturnsValidationFailed()
    {
        var provider = new FakeDocumentEditorProvider();

        var root = Parse(await DocumentEditorAuthoringTools.Import(provider, "docx", "not-base64!!"));

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetString().Should().Be("validation_failed");
    }

    [Fact]
    public async Task Import_Docx_GarbageBytes_ReturnsValidationFailed()
    {
        var provider = new FakeDocumentEditorProvider();
        var garbage = Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8]);

        var root = Parse(await DocumentEditorAuthoringTools.Import(provider, "docx", garbage));

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetString().Should().Be("validation_failed");
    }

    // ---------------------------------------------------------------- export

    [Fact]
    public async Task Export_Markdown_ReturnsContent()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildDocument("doc-export-md");
        provider.Add(doc);

        var root = Parse(await DocumentEditorAuthoringTools.Export(provider, doc.DocumentId, "markdown"));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        root.GetProperty("content").GetString().Should().Contain("# Nadpis").And.Contain("Odstavec smlouvy.");
        root.GetProperty("contentType").GetString().Should().Contain("markdown");
    }

    [Fact]
    public async Task Export_Html_ReturnsMarkup()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildDocument("doc-export-html");
        provider.Add(doc);

        var root = Parse(await DocumentEditorAuthoringTools.Export(provider, doc.DocumentId, "html"));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        root.GetProperty("content").GetString().Should().Contain("<h1").And.Contain("Odstavec smlouvy.");
    }

    [Fact]
    public async Task Export_Docx_ReturnsBase64WithContentType()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildDocument("doc-export-docx");
        provider.Add(doc);

        var root = Parse(await DocumentEditorAuthoringTools.Export(provider, doc.DocumentId, "docx"));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        var base64 = root.GetProperty("contentBase64").GetString();
        base64.Should().NotBeNullOrWhiteSpace();
        var bytes = Convert.FromBase64String(base64!);
        bytes.Length.Should().BeGreaterThan(100);
        // DOCX packages are ZIP archives — PK signature.
        bytes[0].Should().Be(0x50);
        bytes[1].Should().Be(0x4B);
        root.GetProperty("contentType").GetString().Should().Contain("wordprocessingml");
    }

    [Fact]
    public async Task Export_MissingDocument_ReturnsNotFound()
    {
        var provider = new FakeDocumentEditorProvider();

        var root = Parse(await DocumentEditorAuthoringTools.Export(provider, "missing", "markdown"));

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetString().Should().Be("not_found");
    }

    [Fact]
    public async Task Export_UnknownFormat_ReturnsInvalidOperation()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildDocument("doc-export-bad");
        provider.Add(doc);

        var root = Parse(await DocumentEditorAuthoringTools.Export(provider, doc.DocumentId, "pdf"));

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetString().Should().Be("invalid_operation");
    }

    // ---------------------------------------------------------------- round-trips

    [Fact]
    public async Task RoundTrip_Markdown_IsContentStable()
    {
        var provider = new FakeDocumentEditorProvider();
        const string markdown = "# Smlouva o nájmu\n\nPronajímatel a nájemce sjednávají nájem bytu.\n\n- nájemné 15 000 Kč\n- kauce 30 000 Kč\n\n> Podpisem strany stvrzují souhlas.\n";

        var first = Parse(await DocumentEditorAuthoringTools.Import(provider, "markdown", markdown));
        first.GetProperty("success").GetBoolean().Should().BeTrue();
        var firstId = first.GetProperty("id").GetString()!;

        var firstExport = Parse(await DocumentEditorAuthoringTools.Export(provider, firstId, "markdown"))
            .GetProperty("content").GetString()!;

        var second = Parse(await DocumentEditorAuthoringTools.Import(provider, "markdown", firstExport));
        var secondId = second.GetProperty("id").GetString()!;
        var secondExport = Parse(await DocumentEditorAuthoringTools.Export(provider, secondId, "markdown"))
            .GetProperty("content").GetString()!;

        secondExport.Should().Be(firstExport);
        firstExport.Should().Contain("# Smlouva o nájmu").And.Contain("15 000");
    }

    [Fact]
    public async Task RoundTrip_Docx_PreservesText()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildDocument("doc-docx-rt");
        provider.Add(doc);

        var exported = Parse(await DocumentEditorAuthoringTools.Export(provider, doc.DocumentId, "docx"))
            .GetProperty("contentBase64").GetString()!;

        var imported = Parse(await DocumentEditorAuthoringTools.Import(provider, "docx", exported));
        imported.GetProperty("success").GetBoolean().Should().BeTrue();
        var importedId = imported.GetProperty("id").GetString()!;
        var saved = (await provider.LoadAsync(importedId)).Document!;
        var texts = saved.Blocks
            .Select(b => b.Content)
            .OfType<ParagraphBlockContent>()
            .SelectMany(p => p.Inlines.OfType<TextRun>())
            .Select(r => r.Text);
        string.Concat(texts).Should().Contain("Odstavec smlouvy.");
    }

    // ---------------------------------------------------------------- helpers

    private static DocumentEditorDocument BuildDocument(string documentId)
    {
        var doc = DocumentEditorDocument.Empty(documentId);
        doc.Metadata.Title = documentId;
        doc.Blocks.Add(new DocumentBlock
        {
            Id = "h1",
            Type = DocumentBlockType.Heading,
            Order = 0,
            Content = new HeadingBlockContent { Level = 1, Inlines = [new TextRun { Text = "Nadpis" }] }
        });
        doc.Blocks.Add(new DocumentBlock
        {
            Id = "p1",
            Type = DocumentBlockType.Paragraph,
            Order = 1,
            Content = new ParagraphBlockContent { Inlines = [new TextRun { Text = "Odstavec smlouvy." }] }
        });
        return doc;
    }
}
