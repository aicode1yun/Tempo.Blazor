using System.Text.Json;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.Mcp.DocumentEditor;
using Tempo.Blazor.Mcp.Tests.Fixtures;

namespace Tempo.Blazor.Mcp.Tests;

public class DocumentEditorSemanticTextToolsTests
{
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void SemanticTextTools_AreRegisteredInDocumentEditorToolTypes()
    {
        TempoDocumentEditorMcp.ToolTypes.Should().Contain(typeof(DocumentEditorSemanticTextTools));
    }

    // ---------------------------------------------------------------- insert_text

    [Fact]
    public async Task InsertText_InsertsAtPlainTextOffset()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildDocument("doc-ins", "Hello world");
        provider.Add(doc);

        var root = Parse(await DocumentEditorSemanticTextTools.InsertText(
            provider, doc.DocumentId, "p1", 5, " brave", expectedConcurrencyToken: "v1"));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        root.GetProperty("concurrencyToken").GetString().Should().Be("v2");
        PlainText(await Load(provider, doc.DocumentId), "p1").Should().Be("Hello brave world");
    }

    [Fact]
    public async Task InsertText_MultiRunWithToken_MapsPlainOffsetSkippingTokens()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildMultiRunDocument("doc-ins-token");
        provider.Add(doc);

        // Plain text is "Dear , your rent is due." — insert name after "Dear " (offset 5).
        var root = Parse(await DocumentEditorSemanticTextTools.InsertText(
            provider, doc.DocumentId, "p1", 5, "Sir", expectedConcurrencyToken: "v1"));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        var block = (await Load(provider, doc.DocumentId)).Blocks[0];
        var inlines = ((ParagraphBlockContent)block.Content).Inlines;
        ((TextRun)inlines[0]).Text.Should().Be("Dear Sir");
        inlines[1].Should().BeOfType<TokenRun>();
        ((TextRun)inlines[2]).Text.Should().Be(", your ");
        ((TextRun)inlines[3]).Text.Should().Be("rent is due.");
    }

    [Fact]
    public async Task InsertText_IntoEmptyParagraph_AppendsTextRun()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = DocumentEditorDocument.Empty("doc-ins-empty");
        doc.Blocks.Add(new DocumentBlock
        {
            Id = "p1",
            Type = DocumentBlockType.Paragraph,
            Content = new ParagraphBlockContent()
        });
        provider.Add(doc);

        var root = Parse(await DocumentEditorSemanticTextTools.InsertText(
            provider, doc.DocumentId, "p1", 0, "Hello"));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        PlainText(await Load(provider, doc.DocumentId), "p1").Should().Be("Hello");
    }

    [Fact]
    public async Task InsertText_TableCellTarget_Works()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildTableDocument("doc-ins-cell");
        provider.Add(doc);

        var root = Parse(await DocumentEditorSemanticTextTools.InsertText(
            provider, doc.DocumentId, "nested-p", 4, "!", tableCellId: "cell-1"));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        var table = (TableBlockContent)(await Load(provider, doc.DocumentId)).Blocks[0].Content;
        var nested = (ParagraphBlockContent)table.Rows[0].Cells[0].Blocks[0].Content;
        ((TextRun)nested.Inlines[0]).Text.Should().Be("Rent!");
    }

    [Fact]
    public async Task InsertText_ContentControlChild_Works()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = DocumentEditorDocument.Empty("doc-ins-cc");
        doc.Blocks.Add(new DocumentBlock
        {
            Id = "cc-1",
            Type = DocumentBlockType.ContentControl,
            Content = new ContentControlBlockContent
            {
                Control = Tempo.Blazor.DocumentEditor.Services.DocumentAssemblyMetadata.CreateRepeatingSection("items"),
                Blocks =
                [
                    new DocumentBlock
                    {
                        Id = "cc-row",
                        Type = DocumentBlockType.Paragraph,
                        Content = new ParagraphBlockContent { Inlines = [new TextRun { Text = "Položka" }] }
                    }
                ]
            }
        });
        provider.Add(doc);

        var root = Parse(await DocumentEditorSemanticTextTools.InsertText(
            provider, doc.DocumentId, "cc-row", 7, " faktury"));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        var control = (ContentControlBlockContent)(await Load(provider, doc.DocumentId)).Blocks[0].Content;
        ((ParagraphBlockContent)control.Blocks[0].Content).Inlines.OfType<TextRun>()
            .Select(run => run.Text).Should().ContainSingle().Which.Should().Be("Položka faktury");
    }

    [Fact]
    public async Task FormatRange_ContentControlChild_Works()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = DocumentEditorDocument.Empty("doc-fmt-cc");
        doc.Blocks.Add(new DocumentBlock
        {
            Id = "cc-1",
            Type = DocumentBlockType.ContentControl,
            Content = new ContentControlBlockContent
            {
                Control = Tempo.Blazor.DocumentEditor.Services.DocumentAssemblyMetadata.CreateConditionalBlock("if", "x > 1", "g1"),
                Blocks =
                [
                    new DocumentBlock
                    {
                        Id = "cc-p",
                        Type = DocumentBlockType.Paragraph,
                        Content = new ParagraphBlockContent { Inlines = [new TextRun { Text = "Podmíněný text" }] }
                    }
                ]
            }
        });
        provider.Add(doc);

        var root = Parse(await DocumentEditorSemanticTextTools.FormatRange(
            provider, doc.DocumentId, "cc-p", 0, 9, "bold"));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        var control = (ContentControlBlockContent)(await Load(provider, doc.DocumentId)).Blocks[0].Content;
        var bold = ((ParagraphBlockContent)control.Blocks[0].Content).Inlines.OfType<TextRun>()
            .Single(run => run.Marks.Any(mark => mark.Type == InlineMarkType.Bold));
        bold.Text.Should().Be("Podmíněný");
    }

    [Fact]
    public async Task InsertText_OffsetBeyondText_ReturnsValidationFailedWithTextLength()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildDocument("doc-ins-range", "Hello");
        provider.Add(doc);

        var root = Parse(await DocumentEditorSemanticTextTools.InsertText(
            provider, doc.DocumentId, "p1", 99, "x"));

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetString().Should().Be("validation_failed");
        root.GetProperty("message").GetString().Should().Contain("5");
    }

    [Fact]
    public async Task InsertText_MissingBlock_ReturnsNotFoundWithHint()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildDocument("doc-ins-missing", "Hello");
        provider.Add(doc);

        var root = Parse(await DocumentEditorSemanticTextTools.InsertText(
            provider, doc.DocumentId, "nope", 0, "x"));

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetString().Should().Be("not_found");
        root.GetProperty("message").GetString().Should().Contain("document_editor_describe_document");
    }

    [Fact]
    public async Task InsertText_StaleToken_ReturnsConflict()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildDocument("doc-ins-conflict", "Hello");
        provider.Add(doc);

        var root = Parse(await DocumentEditorSemanticTextTools.InsertText(
            provider, doc.DocumentId, "p1", 0, "x", expectedConcurrencyToken: "stale"));

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetString().Should().Be("conflict");
    }

    [Fact]
    public async Task InsertText_MissingDocument_ReturnsNotFound()
    {
        var provider = new FakeDocumentEditorProvider();

        var root = Parse(await DocumentEditorSemanticTextTools.InsertText(
            provider, "missing", "p1", 0, "x"));

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetString().Should().Be("not_found");
    }

    // ---------------------------------------------------------------- delete_text

    [Fact]
    public async Task DeleteText_SpansMultipleRuns_DeletesAllSegmentsAndKeepsToken()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildMultiRunDocument("doc-del-multi");
        provider.Add(doc);

        // Plain: "Dear , your rent is due." — delete ", your rent" (offset 5..16).
        var root = Parse(await DocumentEditorSemanticTextTools.DeleteText(
            provider, doc.DocumentId, "p1", 5, 11));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        var block = (await Load(provider, doc.DocumentId)).Blocks[0];
        var inlines = ((ParagraphBlockContent)block.Content).Inlines;
        inlines.OfType<TokenRun>().Should().ContainSingle();
        PlainText(await Load(provider, doc.DocumentId), "p1").Should().Be("Dear  is due.");
    }

    [Fact]
    public async Task DeleteText_RangeOutOfBounds_ReturnsValidationFailed()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildDocument("doc-del-range", "Hello");
        provider.Add(doc);

        var root = Parse(await DocumentEditorSemanticTextTools.DeleteText(
            provider, doc.DocumentId, "p1", 3, 10));

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetString().Should().Be("validation_failed");
    }

    [Fact]
    public async Task DeleteText_ZeroLength_ReturnsValidationFailed()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildDocument("doc-del-zero", "Hello");
        provider.Add(doc);

        var root = Parse(await DocumentEditorSemanticTextTools.DeleteText(
            provider, doc.DocumentId, "p1", 0, 0));

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetString().Should().Be("validation_failed");
    }

    // ---------------------------------------------------------------- replace_text

    [Fact]
    public async Task ReplaceText_ReplacesAcrossRuns()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildMultiRunDocument("doc-repl-multi");
        provider.Add(doc);

        // Plain: "Dear , your rent is due." — replace "rent is due" (offset 12, length 11).
        var root = Parse(await DocumentEditorSemanticTextTools.ReplaceText(
            provider, doc.DocumentId, "p1", 12, 11, "payment is overdue"));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        PlainText(await Load(provider, doc.DocumentId), "p1").Should().Be("Dear , your payment is overdue.");
    }

    [Fact]
    public async Task ReplaceText_ZeroLength_ActsAsInsert()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildDocument("doc-repl-zero", "Hello world");
        provider.Add(doc);

        var root = Parse(await DocumentEditorSemanticTextTools.ReplaceText(
            provider, doc.DocumentId, "p1", 5, 0, ","));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        PlainText(await Load(provider, doc.DocumentId), "p1").Should().Be("Hello, world");
    }

    [Fact]
    public async Task ReplaceText_TableCellTarget_Works()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildTableDocument("doc-repl-cell");
        provider.Add(doc);

        var root = Parse(await DocumentEditorSemanticTextTools.ReplaceText(
            provider, doc.DocumentId, "nested-p", 0, 4, "Deposit", tableCellId: "cell-1"));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        var table = (TableBlockContent)(await Load(provider, doc.DocumentId)).Blocks[0].Content;
        var nested = (ParagraphBlockContent)table.Rows[0].Cells[0].Blocks[0].Content;
        ((TextRun)nested.Inlines[0]).Text.Should().Be("Deposit");
    }

    // ---------------------------------------------------------------- format_range

    [Fact]
    public async Task FormatRange_AddsBoldToPartialRange_SplittingRuns()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildDocument("doc-fmt-bold", "Hello world");
        provider.Add(doc);

        var root = Parse(await DocumentEditorSemanticTextTools.FormatRange(
            provider, doc.DocumentId, "p1", 6, 5, "bold"));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        var inlines = ((ParagraphBlockContent)(await Load(provider, doc.DocumentId)).Blocks[0].Content).Inlines;
        var bold = inlines.OfType<TextRun>().Single(r => r.Marks.Any(m => m.Type == InlineMarkType.Bold));
        bold.Text.Should().Be("world");
        inlines.OfType<TextRun>().First().Text.Should().Be("Hello ");
    }

    [Fact]
    public async Task FormatRange_RangeAcrossToken_AlsoMarksToken()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildMultiRunDocument("doc-fmt-token");
        provider.Add(doc);

        // Plain: "Dear , your rent is due." — bold "Dear , your" (offset 0, length 11) spans the token.
        var root = Parse(await DocumentEditorSemanticTextTools.FormatRange(
            provider, doc.DocumentId, "p1", 0, 11, "bold"));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        var inlines = ((ParagraphBlockContent)(await Load(provider, doc.DocumentId)).Blocks[0].Content).Inlines;
        inlines.OfType<TokenRun>().Single().Marks.Should().Contain(m => m.Type == InlineMarkType.Bold);
    }

    [Fact]
    public async Task FormatRange_RemoveBold_RemovesMarkFromRange()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = DocumentEditorDocument.Empty("doc-fmt-remove");
        doc.Blocks.Add(new DocumentBlock
        {
            Id = "p1",
            Type = DocumentBlockType.Paragraph,
            Content = new ParagraphBlockContent
            {
                Inlines = [new TextRun { Text = "Hello world", Marks = [new InlineMark { Type = InlineMarkType.Bold }] }]
            }
        });
        provider.Add(doc);

        var root = Parse(await DocumentEditorSemanticTextTools.FormatRange(
            provider, doc.DocumentId, "p1", 0, 5, "bold", action: "remove"));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        var inlines = ((ParagraphBlockContent)(await Load(provider, doc.DocumentId)).Blocks[0].Content).Inlines;
        var plain = inlines.OfType<TextRun>().Single(r => r.Marks.Count == 0);
        plain.Text.Should().Be("Hello");
        var stillBold = inlines.OfType<TextRun>().Single(r => r.Marks.Any(m => m.Type == InlineMarkType.Bold));
        stillBold.Text.Should().Be(" world");
    }

    [Fact]
    public async Task FormatRange_Link_AddsHref()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildDocument("doc-fmt-link", "Visit our site today");
        provider.Add(doc);

        var root = Parse(await DocumentEditorSemanticTextTools.FormatRange(
            provider, doc.DocumentId, "p1", 6, 8, "link", value: "https://example.com"));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        var inlines = ((ParagraphBlockContent)(await Load(provider, doc.DocumentId)).Blocks[0].Content).Inlines;
        var linked = inlines.OfType<TextRun>().Single(r => r.Marks.Any(m => m.Type == InlineMarkType.Link));
        linked.Text.Should().Be("our site");
        linked.Marks.Single(m => m.Type == InlineMarkType.Link).Link!.Href.Should().Be("https://example.com");
    }

    [Fact]
    public async Task FormatRange_HighlightWithValue_SetsValue()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildDocument("doc-fmt-highlight", "Important text");
        provider.Add(doc);

        var root = Parse(await DocumentEditorSemanticTextTools.FormatRange(
            provider, doc.DocumentId, "p1", 0, 9, "highlight", value: "#ffff00"));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        var inlines = ((ParagraphBlockContent)(await Load(provider, doc.DocumentId)).Blocks[0].Content).Inlines;
        var marked = inlines.OfType<TextRun>().Single(r => r.Marks.Any(m => m.Type == InlineMarkType.Highlight));
        marked.Marks.Single(m => m.Type == InlineMarkType.Highlight).Value.Should().Be("#ffff00");
    }

    [Fact]
    public async Task FormatRange_ValueCarryingMarkWithoutValue_ReturnsValidationFailed()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildDocument("doc-fmt-noval", "Hello");
        provider.Add(doc);

        var root = Parse(await DocumentEditorSemanticTextTools.FormatRange(
            provider, doc.DocumentId, "p1", 0, 5, "link"));

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetString().Should().Be("validation_failed");
    }

    [Fact]
    public async Task FormatRange_UnknownMark_ReturnsInvalidOperationListingSupportedMarks()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildDocument("doc-fmt-unknown", "Hello");
        provider.Add(doc);

        var root = Parse(await DocumentEditorSemanticTextTools.FormatRange(
            provider, doc.DocumentId, "p1", 0, 5, "sparkle"));

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetString().Should().Be("invalid_operation");
        root.GetProperty("message").GetString().Should().Contain("bold");
    }

    [Fact]
    public async Task FormatRange_SemanticMark_ReturnsInvalidOperation()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildDocument("doc-fmt-semantic", "Hello");
        provider.Add(doc);

        var root = Parse(await DocumentEditorSemanticTextTools.FormatRange(
            provider, doc.DocumentId, "p1", 0, 5, "commentAnchor"));

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetString().Should().Be("invalid_operation");
    }

    [Fact]
    public async Task FormatRange_RangeOutOfBounds_ReturnsValidationFailed()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildDocument("doc-fmt-range", "Hello");
        provider.Add(doc);

        var root = Parse(await DocumentEditorSemanticTextTools.FormatRange(
            provider, doc.DocumentId, "p1", 2, 10, "bold"));

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetString().Should().Be("validation_failed");
    }

    [Fact]
    public async Task FormatRange_TableCellTarget_Works()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildTableDocument("doc-fmt-cell");
        provider.Add(doc);

        var root = Parse(await DocumentEditorSemanticTextTools.FormatRange(
            provider, doc.DocumentId, "nested-p", 0, 4, "italic", tableCellId: "cell-1"));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        var table = (TableBlockContent)(await Load(provider, doc.DocumentId)).Blocks[0].Content;
        var nested = (ParagraphBlockContent)table.Rows[0].Cells[0].Blocks[0].Content;
        nested.Inlines.OfType<TextRun>().Single().Marks.Should().Contain(m => m.Type == InlineMarkType.Italic);
    }

    // ---------------------------------------------------------------- set_heading

    [Fact]
    public async Task SetHeading_ConvertsParagraphPreservingText()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildDocument("doc-head", "Chapter one");
        provider.Add(doc);

        var root = Parse(await DocumentEditorSemanticTextTools.SetHeading(
            provider, doc.DocumentId, "p1", 2));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        var block = (await Load(provider, doc.DocumentId)).Blocks[0];
        block.Type.Should().Be(DocumentBlockType.Heading);
        var heading = (HeadingBlockContent)block.Content;
        heading.Level.Should().Be(2);
        ((TextRun)heading.Inlines[0]).Text.Should().Be("Chapter one");
    }

    [Fact]
    public async Task SetHeading_InvalidLevel_ReturnsValidationFailed()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildDocument("doc-head-bad", "Chapter");
        provider.Add(doc);

        var root = Parse(await DocumentEditorSemanticTextTools.SetHeading(
            provider, doc.DocumentId, "p1", 9));

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetString().Should().Be("validation_failed");
    }

    // ---------------------------------------------------------------- set_paragraph_properties

    [Fact]
    public async Task SetParagraphProperties_PatchesAlignmentSpacingAndIndent()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildDocument("doc-para", "Text");
        provider.Add(doc);

        var root = Parse(await DocumentEditorSemanticTextTools.SetParagraphProperties(
            provider, doc.DocumentId, "p1",
            alignment: "center", lineSpacing: 1.5, spacingBefore: 6, spacingAfter: 12, leftIndent: 36));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        var props = (await Load(provider, doc.DocumentId)).Blocks[0].ParagraphProperties;
        props.Alignment.Should().Be(DocumentTextAlignment.Center);
        props.LineSpacing.Should().Be(1.5);
        props.SpacingBefore.Should().Be(6);
        props.SpacingAfter.Should().Be(12);
        props.LeftIndent.Should().Be(36);
    }

    [Fact]
    public async Task SetParagraphProperties_NoValues_ReturnsValidationFailed()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildDocument("doc-para-empty", "Text");
        provider.Add(doc);

        var root = Parse(await DocumentEditorSemanticTextTools.SetParagraphProperties(
            provider, doc.DocumentId, "p1"));

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetString().Should().Be("validation_failed");
    }

    [Fact]
    public async Task SetParagraphProperties_InvalidAlignment_ReturnsValidationFailedListingValues()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildDocument("doc-para-align", "Text");
        provider.Add(doc);

        var root = Parse(await DocumentEditorSemanticTextTools.SetParagraphProperties(
            provider, doc.DocumentId, "p1", alignment: "diagonal"));

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetString().Should().Be("validation_failed");
        root.GetProperty("message").GetString().Should().Contain("justify");
    }

    [Fact]
    public async Task SetParagraphProperties_StaleToken_ReturnsConflict()
    {
        var provider = new FakeDocumentEditorProvider();
        var doc = BuildDocument("doc-para-conflict", "Text");
        provider.Add(doc);

        var root = Parse(await DocumentEditorSemanticTextTools.SetParagraphProperties(
            provider, doc.DocumentId, "p1", alignment: "center", expectedConcurrencyToken: "stale"));

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetString().Should().Be("conflict");
    }

    // ---------------------------------------------------------------- helpers

    private static async Task<DocumentEditorDocument> Load(FakeDocumentEditorProvider provider, string documentId)
        => (await provider.LoadAsync(documentId)).Document!;

    private static string PlainText(DocumentEditorDocument document, string blockId)
    {
        var block = document.Blocks.First(b => b.Id == blockId);
        var inlines = block.Content switch
        {
            ParagraphBlockContent p => p.Inlines,
            HeadingBlockContent h => h.Inlines,
            _ => []
        };
        return string.Concat(inlines.OfType<TextRun>().Select(r => r.Text));
    }

    private static DocumentEditorDocument BuildDocument(string documentId, string text)
    {
        var doc = DocumentEditorDocument.Empty(documentId);
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

    /// <summary>Plain text: "Dear , your rent is due." (token contributes no plain text).</summary>
    private static DocumentEditorDocument BuildMultiRunDocument(string documentId)
    {
        var doc = DocumentEditorDocument.Empty(documentId);
        doc.Blocks.Add(new DocumentBlock
        {
            Id = "p1",
            Type = DocumentBlockType.Paragraph,
            Content = new ParagraphBlockContent
            {
                Inlines =
                [
                    new TextRun { Text = "Dear " },
                    new TokenRun { Key = "tenant.name", DisplayName = "Jméno nájemce" },
                    new TextRun { Text = ", your " },
                    new TextRun { Text = "rent is due." }
                ]
            }
        });
        return doc;
    }

    private static DocumentEditorDocument BuildTableDocument(string documentId)
    {
        var doc = DocumentEditorDocument.Empty(documentId);
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
                        Cells =
                        [
                            new TableCellContent
                            {
                                Id = "cell-1",
                                Blocks =
                                [
                                    new DocumentBlock
                                    {
                                        Id = "nested-p",
                                        Type = DocumentBlockType.Paragraph,
                                        Content = new ParagraphBlockContent
                                        {
                                            Inlines = [new TextRun { Text = "Rent" }]
                                        }
                                    }
                                ]
                            }
                        ]
                    }
                ]
            }
        });
        return doc;
    }
}
