using System.Text.Json;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.Mcp.DocumentEditor;
using Tempo.Blazor.Mcp.Tests.Fixtures;

namespace Tempo.Blazor.Mcp.Tests;

public class DocumentEditorToolsTests
{
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public async Task GetDocument_ReturnsDocumentAndConcurrencyToken()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildDocument("doc-1", "Hello");
        provider.Add(doc);

        var root = Parse(await DocumentEditorDocumentTools.GetDocument(provider, doc.DocumentId));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        root.GetProperty("concurrencyToken").GetString().Should().Be("v1");
        root.GetProperty("document").GetProperty("DocumentId").GetString().Should().Be("doc-1");
    }

    [Fact]
    public async Task GetJson_ReturnsRawSnapshot()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildDocument("doc-json", "Hello");
        provider.Add(doc);

        var root = Parse(await DocumentEditorDocumentTools.GetJson(provider, doc.DocumentId));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        root.GetProperty("jsonSnapshot").GetString().Should().Contain("doc-json");
    }

    [Fact]
    public async Task SaveDocument_ValidDocument_PersistsAndReturnsNewToken()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildDocument("doc-save", "Hello");
        provider.Add(doc);
        doc.Metadata.Title = "Saved";

        var root = Parse(await DocumentEditorDocumentTools.SaveDocument(
            provider,
            doc.DocumentId,
            DocumentEditorJson.Serialize(doc),
            expectedConcurrencyToken: "v1"));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        root.GetProperty("concurrencyToken").GetString().Should().Be("v2");
        (await provider.LoadAsync(doc.DocumentId)).Document!.Metadata.Title.Should().Be("Saved");
    }

    [Fact]
    public async Task SaveDocument_AppliesPostFixerBeforeValidationAndSave()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = DocumentEditorDocument.Empty("doc-postfix");
        doc.Blocks.Add(new DocumentBlock
        {
            Id = "table-1",
            Type = DocumentBlockType.Table,
            Content = new TableBlockContent
            {
                Rows =
                [
                    new TableRowContent
                    {
                        Cells = [new TableCellContent { Id = "cell-1" }]
                    }
                ]
            }
        });

        var root = Parse(await DocumentEditorDocumentTools.SaveDocument(
            provider,
            doc.DocumentId,
            DocumentEditorJson.Serialize(doc),
            force: true));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        root.GetProperty("postFixWarnings").EnumerateArray()
            .Select(e => e.GetProperty("code").GetString())
            .Should().Contain("empty-table-cell-placeholder");

        var saved = (await provider.LoadAsync(doc.DocumentId)).Document!;
        var table = (TableBlockContent)saved.Blocks[0].Content;
        table.Rows[0].Cells[0].Blocks.Should().ContainSingle();
    }

    [Fact]
    public async Task SaveDocument_StaleToken_ReturnsConflict()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildDocument("doc-conflict", "Hello");
        provider.Add(doc);

        var root = Parse(await DocumentEditorDocumentTools.SaveDocument(
            provider,
            doc.DocumentId,
            DocumentEditorJson.Serialize(doc),
            expectedConcurrencyToken: "stale"));

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetString().Should().Be("conflict");
    }

    [Fact]
    public void ValidateDocument_DuplicateBlockId_ReturnsValidationError()
    {
        var doc = BuildDocument("doc-validate", "Hello");
        doc.Blocks.Add(new DocumentBlock
        {
            Id = "p1",
            Type = DocumentBlockType.Paragraph,
            Content = new ParagraphBlockContent()
        });

        var root = Parse(DocumentEditorAnalysisTools.ValidateDocument(DocumentEditorJson.Serialize(doc)));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        root.GetProperty("valid").GetBoolean().Should().BeFalse();
        root.GetProperty("validationErrors").EnumerateArray()
            .Select(e => e.GetString())
            .Should().Contain(e => e!.Contains("duplicate block id"));
    }

    [Fact]
    public async Task GetOutline_ReturnsHeadingBlocks()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = DocumentEditorDocument.Empty("doc-outline");
        doc.Blocks.Add(new DocumentBlock
        {
            Id = "h1",
            Type = DocumentBlockType.Heading,
            Content = new HeadingBlockContent
            {
                Level = 2,
                Inlines = [new TextRun { Text = "Overview" }]
            }
        });
        provider.Add(doc);

        var root = Parse(await DocumentEditorAnalysisTools.GetOutline(provider, doc.DocumentId));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        root.GetProperty("outline").EnumerateArray().Should().ContainSingle()
            .Which.GetProperty("text").GetString().Should().Be("Overview");
    }

    [Fact]
    public async Task SearchText_ReturnsMatches()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildDocument("doc-search", "Hello search world");
        provider.Add(doc);

        var root = Parse(await DocumentEditorAnalysisTools.SearchText(provider, doc.DocumentId, "search"));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        root.GetProperty("totalCount").GetInt32().Should().Be(1);
        root.GetProperty("results")[0].GetProperty("preview").GetString().Should().Contain("search");
    }

    [Fact]
    public async Task ApplyOperations_UsesDocumentOperationApplierAndPersists()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildDocument("doc-ops", "Hello");
        provider.Add(doc);
        var batch = new DocumentOperationBatch
        {
            DocumentId = doc.DocumentId,
            Operations =
            [
                new DocumentOperation
                {
                    Type = DocumentOperationType.InsertText,
                    Target = new DocumentOperationTarget { BlockId = "p1", InlineIndex = 0, Offset = 5 },
                    Text = " world"
                }
            ]
        };

        var root = Parse(await DocumentEditorOperationTools.ApplyOperations(
            provider,
            doc.DocumentId,
            JsonSerializer.Serialize(batch, DocumentEditorJson.Options),
            expectedConcurrencyToken: "v1"));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        root.GetProperty("applied").GetInt32().Should().Be(1);

        var saved = (await provider.LoadAsync(doc.DocumentId)).Document!;
        var paragraph = (ParagraphBlockContent)saved.Blocks[0].Content;
        ((TextRun)paragraph.Inlines[0]).Text.Should().Be("Hello world");
    }

    [Fact]
    public async Task ApplyOperations_RawCanvasRelayBatch_ReturnsUnsupported()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildDocument("doc-canvas-relay", "Hello");
        provider.Add(doc);
        var batch = new DocumentOperationBatch
        {
            DocumentId = doc.DocumentId,
            CanvasOperationBatchJson = """{"operations":[{"type":"insertText"}]}"""
        };

        var root = Parse(await DocumentEditorOperationTools.ApplyOperations(
            provider,
            doc.DocumentId,
            JsonSerializer.Serialize(batch, DocumentEditorJson.Options),
            expectedConcurrencyToken: "v1"));

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetString().Should().Be("unsupported");
    }

    [Fact]
    public async Task GetVersionsAndRestoreVersion_RoundTrip()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildDocument("doc-version", "Original");
        provider.Add(doc);
        var version = await provider.CreateVersionAsync(new DocumentVersionCreateRequest
        {
            DocumentId = doc.DocumentId,
            Kind = DocumentVersionKind.Major,
            Label = "1.0"
        });

        var versionsRoot = Parse(await DocumentEditorDocumentTools.GetVersions(provider, doc.DocumentId));
        versionsRoot.GetProperty("versions").EnumerateArray().Should().ContainSingle();

        var changed = BuildDocument("doc-version", "Changed");
        await DocumentEditorDocumentTools.SaveDocument(provider, doc.DocumentId, DocumentEditorJson.Serialize(changed), force: true);

        var restoreRoot = Parse(await DocumentEditorDocumentTools.RestoreVersion(provider, doc.DocumentId, version.Id, force: true));

        restoreRoot.GetProperty("success").GetBoolean().Should().BeTrue();
        var saved = (await provider.LoadAsync(doc.DocumentId)).Document!;
        var paragraph = (ParagraphBlockContent)saved.Blocks[0].Content;
        ((TextRun)paragraph.Inlines[0]).Text.Should().Be("Original");
    }

    private static DocumentEditorDocument BuildDocument(string documentId, string text)
    {
        var doc = DocumentEditorDocument.Empty(documentId);
        doc.Metadata.Title = documentId;
        doc.Blocks.Add(new DocumentBlock
        {
            Id = "p1",
            Type = DocumentBlockType.Paragraph,
            Order = 0,
            Content = new ParagraphBlockContent
            {
                Inlines = [new TextRun { Text = text }]
            }
        });
        return doc;
    }
}
