using System.Text.Json;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.Mcp.DocumentEditor;
using Tempo.Blazor.Mcp.Tests.Fixtures;

namespace Tempo.Blazor.Mcp.Tests;

public class DocumentEditorBlockToolsTests
{
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void BlockTools_AreRegisteredInDocumentEditorToolTypes()
    {
        TempoDocumentEditorMcp.ToolTypes.Should().Contain(typeof(DocumentEditorBlockTools));
    }

    // ---------------------------------------------------------------- insert_block

    [Fact]
    public async Task InsertBlock_Paragraph_AppendsAtBodyEndByDefault()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildDocument("doc-insb");
        provider.Add(doc);

        var root = Parse(await DocumentEditorBlockTools.InsertBlock(
            provider, doc.DocumentId, "paragraph", "Nový odstavec", expectedConcurrencyToken: "v1"));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        var newBlockId = root.GetProperty("blockId").GetString();
        newBlockId.Should().NotBeNullOrWhiteSpace();

        var saved = await Load(provider, doc.DocumentId);
        saved.Blocks.Should().HaveCount(3);
        saved.Blocks[^1].Id.Should().Be(newBlockId);
        ((ParagraphBlockContent)saved.Blocks[^1].Content).Inlines.OfType<TextRun>().Single().Text.Should().Be("Nový odstavec");
    }

    [Fact]
    public async Task InsertBlock_HeadingWithOrder_InsertsSortedByOrderValue()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildDocument("doc-insb-order");
        provider.Add(doc);

        var root = Parse(await DocumentEditorBlockTools.InsertBlock(
            provider, doc.DocumentId, "heading", "Mezi-nadpis", order: 0.5, headingLevel: 3));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        var saved = await Load(provider, doc.DocumentId);
        saved.Blocks.Should().HaveCount(3);
        saved.Blocks[1].Content.Should().BeOfType<HeadingBlockContent>();
        ((HeadingBlockContent)saved.Blocks[1].Content).Level.Should().Be(3);
    }

    [Fact]
    public async Task InsertBlock_OrderedList_IntoTableCellAtIndex()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildTableDocument("doc-insb-cell");
        provider.Add(doc);

        var root = Parse(await DocumentEditorBlockTools.InsertBlock(
            provider, doc.DocumentId, "list", "Položka", tableCellId: "cell-1", order: 0, ordered: true));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        var table = (TableBlockContent)(await Load(provider, doc.DocumentId)).Blocks[0].Content;
        var cellBlocks = table.Rows[0].Cells[0].Blocks;
        cellBlocks.Should().HaveCount(2);
        cellBlocks[0].Content.Should().BeOfType<ListBlockContent>();
        ((ListBlockContent)cellBlocks[0].Content).Ordered.Should().BeTrue();
    }

    [Fact]
    public async Task InsertBlock_Quote_IntoTableCellAppendsByDefault()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildTableDocument("doc-insb-cell-append");
        provider.Add(doc);

        var root = Parse(await DocumentEditorBlockTools.InsertBlock(
            provider, doc.DocumentId, "quote", "Citace", tableCellId: "cell-1"));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        var table = (TableBlockContent)(await Load(provider, doc.DocumentId)).Blocks[0].Content;
        var cellBlocks = table.Rows[0].Cells[0].Blocks;
        cellBlocks[^1].Content.Should().BeOfType<QuoteBlockContent>();
    }

    [Fact]
    public async Task InsertBlock_UnsupportedType_ReturnsInvalidOperationListingTypes()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildDocument("doc-insb-badtype");
        provider.Add(doc);

        var root = Parse(await DocumentEditorBlockTools.InsertBlock(
            provider, doc.DocumentId, "table", "x"));

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetString().Should().Be("invalid_operation");
        root.GetProperty("message").GetString().Should().Contain("paragraph");
    }

    [Fact]
    public async Task InsertBlock_ExplicitDuplicateBlockId_ReturnsValidationFailed()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildDocument("doc-insb-dup");
        provider.Add(doc);

        var root = Parse(await DocumentEditorBlockTools.InsertBlock(
            provider, doc.DocumentId, "paragraph", "x", blockId: "p1"));

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetString().Should().Be("validation_failed");
        root.GetProperty("message").GetString().Should().Contain("p1");
    }

    [Fact]
    public async Task InsertBlock_MissingTableCell_ReturnsNotFound()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildDocument("doc-insb-nocell");
        provider.Add(doc);

        var root = Parse(await DocumentEditorBlockTools.InsertBlock(
            provider, doc.DocumentId, "paragraph", "x", tableCellId: "no-such-cell"));

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetString().Should().Be("not_found");
    }

    // ---------------------------------------------------------------- delete_block

    [Fact]
    public async Task DeleteBlock_RemovesBodyBlock()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildDocument("doc-delb");
        provider.Add(doc);

        var root = Parse(await DocumentEditorBlockTools.DeleteBlock(provider, doc.DocumentId, "p1"));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        (await Load(provider, doc.DocumentId)).Blocks.Select(b => b.Id).Should().NotContain("p1");
    }

    [Fact]
    public async Task DeleteBlock_Nested_RemovesFromTableCell()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildTableDocument("doc-delb-cell");
        provider.Add(doc);

        var root = Parse(await DocumentEditorBlockTools.DeleteBlock(
            provider, doc.DocumentId, "nested-p", tableCellId: "cell-1"));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        var table = (TableBlockContent)(await Load(provider, doc.DocumentId)).Blocks[0].Content;
        // The post-fixer keeps table cells non-empty with a placeholder paragraph.
        table.Rows[0].Cells[0].Blocks.Select(b => b.Id).Should().NotContain("nested-p");
        root.GetProperty("postFixWarnings").EnumerateArray()
            .Select(e => e.GetProperty("code").GetString())
            .Should().Contain("empty-table-cell-placeholder");
    }

    [Fact]
    public async Task DeleteBlock_Missing_ReturnsNotFound()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildDocument("doc-delb-missing");
        provider.Add(doc);

        var root = Parse(await DocumentEditorBlockTools.DeleteBlock(provider, doc.DocumentId, "nope"));

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetString().Should().Be("not_found");
    }

    // ---------------------------------------------------------------- move_block

    [Fact]
    public async Task MoveBlock_Body_UsesOrderValueWithMovedBlockWinningTies()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildDocument("doc-move");
        provider.Add(doc);

        var root = Parse(await DocumentEditorBlockTools.MoveBlock(provider, doc.DocumentId, "p2", 0));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        (await Load(provider, doc.DocumentId)).Blocks[0].Id.Should().Be("p2");
    }

    [Fact]
    public async Task MoveBlock_TableCell_UsesIndexSemantics()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildTableDocument("doc-move-cell", secondCellParagraph: true);
        provider.Add(doc);

        var root = Parse(await DocumentEditorBlockTools.MoveBlock(
            provider, doc.DocumentId, "nested-p2", 0, tableCellId: "cell-1"));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        var table = (TableBlockContent)(await Load(provider, doc.DocumentId)).Blocks[0].Content;
        table.Rows[0].Cells[0].Blocks[0].Id.Should().Be("nested-p2");
    }

    [Fact]
    public async Task MoveBlock_Missing_ReturnsNotFound()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildDocument("doc-move-missing");
        provider.Add(doc);

        var root = Parse(await DocumentEditorBlockTools.MoveBlock(provider, doc.DocumentId, "nope", 1));

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetString().Should().Be("not_found");
    }

    // ---------------------------------------------------------------- update_block

    [Fact]
    public async Task UpdateBlock_ReplacesWholeBlockPayload()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildDocument("doc-updb");
        provider.Add(doc);
        var replacement = new DocumentBlock
        {
            Id = "p1",
            Type = DocumentBlockType.Heading,
            Content = new HeadingBlockContent
            {
                Level = 2,
                Inlines = [new TextRun { Text = "Nahrazený nadpis" }]
            }
        };

        var root = Parse(await DocumentEditorBlockTools.UpdateBlock(
            provider, doc.DocumentId, "p1", JsonSerializer.Serialize(replacement, DocumentEditorJson.Options)));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        var saved = await Load(provider, doc.DocumentId);
        var block = saved.Blocks.Single(b => b.Id == "p1");
        block.Content.Should().BeOfType<HeadingBlockContent>();
        ((HeadingBlockContent)block.Content).Level.Should().Be(2);
    }

    [Fact]
    public async Task UpdateBlock_BlockJsonIdMismatch_IsForcedToTargetBlockId()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildDocument("doc-updb-id");
        provider.Add(doc);
        var replacement = new DocumentBlock
        {
            Id = "different-id",
            Type = DocumentBlockType.Paragraph,
            Content = new ParagraphBlockContent { Inlines = [new TextRun { Text = "Nový obsah" }] }
        };

        var root = Parse(await DocumentEditorBlockTools.UpdateBlock(
            provider, doc.DocumentId, "p1", JsonSerializer.Serialize(replacement, DocumentEditorJson.Options)));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        var saved = await Load(provider, doc.DocumentId);
        saved.Blocks.Select(b => b.Id).Should().Contain("p1").And.NotContain("different-id");
        ((ParagraphBlockContent)saved.Blocks.Single(b => b.Id == "p1").Content)
            .Inlines.OfType<TextRun>().Single().Text.Should().Be("Nový obsah");
    }

    [Fact]
    public async Task UpdateBlock_InvalidJson_ReturnsValidationFailed()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildDocument("doc-updb-badjson");
        provider.Add(doc);

        var root = Parse(await DocumentEditorBlockTools.UpdateBlock(
            provider, doc.DocumentId, "p1", "{not json"));

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetString().Should().Be("validation_failed");
    }

    [Fact]
    public async Task UpdateBlock_MissingBlock_ReturnsNotFound()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildDocument("doc-updb-missing");
        provider.Add(doc);

        var root = Parse(await DocumentEditorBlockTools.UpdateBlock(
            provider, doc.DocumentId, "nope", JsonSerializer.Serialize(new DocumentBlock { Id = "nope" }, DocumentEditorJson.Options)));

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetString().Should().Be("not_found");
    }

    // ---------------------------------------------------------------- set_table_cell_text

    [Fact]
    public async Task SetTableCellText_ReplacesCellParagraphText()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildTableDocument("doc-cell-text");
        provider.Add(doc);

        var root = Parse(await DocumentEditorBlockTools.SetTableCellText(
            provider, doc.DocumentId, "t1", "cell-1", "15 000 Kč"));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        var table = (TableBlockContent)(await Load(provider, doc.DocumentId)).Blocks[0].Content;
        var paragraph = (ParagraphBlockContent)table.Rows[0].Cells[0].Blocks[0].Content;
        paragraph.Inlines.OfType<TextRun>().Single().Text.Should().Be("15 000 Kč");
    }

    [Fact]
    public async Task SetTableCellText_MissingCell_ReturnsNotFound()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildTableDocument("doc-cell-text-missing");
        provider.Add(doc);

        var root = Parse(await DocumentEditorBlockTools.SetTableCellText(
            provider, doc.DocumentId, "t1", "no-such-cell", "x"));

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetString().Should().Be("not_found");
    }

    [Fact]
    public async Task SetTableCellText_TargetNotTable_ReturnsInvalidOperation()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildDocument("doc-cell-text-nottable");
        provider.Add(doc);

        var root = Parse(await DocumentEditorBlockTools.SetTableCellText(
            provider, doc.DocumentId, "p1", "cell-1", "x"));

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetString().Should().Be("invalid_operation");
    }

    [Fact]
    public async Task SetTableCellText_StaleToken_ReturnsConflict()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildTableDocument("doc-cell-text-conflict");
        provider.Add(doc);

        var root = Parse(await DocumentEditorBlockTools.SetTableCellText(
            provider, doc.DocumentId, "t1", "cell-1", "x", expectedConcurrencyToken: "stale"));

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetString().Should().Be("conflict");
    }

    // ---------------------------------------------------------------- helpers

    private static async Task<DocumentEditorDocument> Load(FakeDocumentEditorProvider provider, string documentId)
        => (await provider.LoadAsync(documentId)).Document!;

    private static DocumentEditorDocument BuildDocument(string documentId)
    {
        var doc = DocumentEditorDocument.Empty(documentId);
        doc.Blocks.Add(new DocumentBlock
        {
            Id = "p1",
            Type = DocumentBlockType.Paragraph,
            Order = 0,
            Content = new ParagraphBlockContent { Inlines = [new TextRun { Text = "První" }] }
        });
        doc.Blocks.Add(new DocumentBlock
        {
            Id = "p2",
            Type = DocumentBlockType.Paragraph,
            Order = 1,
            Content = new ParagraphBlockContent { Inlines = [new TextRun { Text = "Druhý" }] }
        });
        return doc;
    }

    private static DocumentEditorDocument BuildTableDocument(string documentId, bool secondCellParagraph = false)
    {
        var doc = DocumentEditorDocument.Empty(documentId);
        var cellBlocks = new List<DocumentBlock>
        {
            new()
            {
                Id = "nested-p",
                Type = DocumentBlockType.Paragraph,
                Content = new ParagraphBlockContent { Inlines = [new TextRun { Text = "Nájem" }] }
            }
        };
        if (secondCellParagraph)
        {
            cellBlocks.Add(new DocumentBlock
            {
                Id = "nested-p2",
                Type = DocumentBlockType.Paragraph,
                Content = new ParagraphBlockContent { Inlines = [new TextRun { Text = "Kauce" }] }
            });
        }

        doc.Blocks.Add(new DocumentBlock
        {
            Id = "t1",
            Type = DocumentBlockType.Table,
            Content = new TableBlockContent
            {
                Rows =
                [
                    new TableRowContent
                    {
                        Cells = [new TableCellContent { Id = "cell-1", Blocks = cellBlocks }]
                    }
                ]
            }
        });
        return doc;
    }
}
