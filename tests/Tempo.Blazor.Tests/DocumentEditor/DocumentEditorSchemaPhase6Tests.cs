using FluentAssertions;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentEditorSchemaPhase6Tests
{
    [Fact]
    public void Phase6_SchemaBuilder_BuildsImmutableBlockAndMarkRules()
    {
        var builder = new DocumentEditorSchemaBuilder();
        builder.Block("paragraph").AllowIn("body").DisallowIn("header");
        builder.Mark(InlineMarkType.Link)
            .AllowIn(DocumentInlineContext.Text, DocumentInlineContext.Token)
            .DisallowIn(DocumentInlineContext.Token)
            .AffectsReview();

        var schema = builder.Build();
        builder.Block("paragraph").AllowIn("header");

        schema.CanInsert(DocumentBlockType.Paragraph, DocumentEditorRegion.Body).Should().BeTrue();
        schema.CanInsert(DocumentBlockType.Paragraph, DocumentEditorRegion.Header).Should().BeFalse();
        schema.CanApplyMark(InlineMarkType.Link, DocumentInlineContext.Text).Should().BeTrue();
        schema.CanApplyMark(InlineMarkType.Link, DocumentInlineContext.Token).Should().BeFalse();
        schema.MarkAffectsReview(InlineMarkType.Link).Should().BeTrue();
    }

    [Fact]
    public void Phase6_DefaultSchema_RegistersExpectedBodyHeaderFooterAndTableCellRules()
    {
        var schema = DocumentEditorDefaultSchema.Create();

        schema.CanInsert(DocumentBlockType.Paragraph, DocumentEditorRegion.Header).Should().BeTrue();
        schema.CanInsert(DocumentBlockType.Paragraph, DocumentEditorRegion.Footer).Should().BeTrue();
        schema.CanInsert(DocumentBlockType.Paragraph, DocumentEditorRegion.TableCell).Should().BeTrue();
        schema.CanInsert(DocumentBlockType.Heading, DocumentEditorRegion.Body).Should().BeTrue();
        schema.CanInsert(DocumentBlockType.Heading, DocumentEditorRegion.Header).Should().BeFalse();
        schema.CanInsert(DocumentBlockType.Table, DocumentEditorRegion.Body).Should().BeTrue();
        schema.CanInsert(DocumentBlockType.Table, DocumentEditorRegion.TableCell).Should().BeFalse();
        schema.CanInsert(DocumentBlockType.PageBreak, DocumentEditorRegion.Body).Should().BeTrue();
        schema.CanInsert(DocumentBlockType.PageBreak, DocumentEditorRegion.Footer).Should().BeFalse();
        schema.CanInsert(DocumentBlockType.Image, DocumentEditorRegion.TableCell).Should().BeTrue();
        schema.CanInsert(DocumentInsertionKind.Footnote, DocumentEditorRegion.Body).Should().BeTrue();
        schema.CanInsert(DocumentInsertionKind.Footnote, DocumentEditorRegion.Header).Should().BeFalse();
        schema.CanApplyMark(InlineMarkType.Link, DocumentInlineContext.Token).Should().BeFalse();
        schema.MarkAffectsReview(InlineMarkType.Revision).Should().BeTrue();
    }

    [Fact]
    public void Phase6_InsertionPolicy_RejectsPageBreakOutsideBodyAndDefaultsImageAltText()
    {
        var policy = new DocumentInsertionPolicy();
        var pageBreak = new DocumentBlock { Type = DocumentBlockType.PageBreak, Content = new PageBreakBlockContent() };
        var image = new DocumentBlock { Type = DocumentBlockType.Image, Content = new ImageBlockContent { Url = "https://example.com/a.png", AltText = null } };

        var headerResult = policy.Apply([pageBreak], DocumentEditorRegion.Header);
        var tableCellResult = policy.Apply([image], DocumentEditorRegion.TableCell);

        headerResult.Blocks.Should().BeEmpty();
        headerResult.Warnings.Should().Contain(warning => warning.Code == "block-rejected-by-schema");
        tableCellResult.Blocks.Should().ContainSingle();
        ((ImageBlockContent)tableCellResult.Blocks[0].Content).AltText.Should().BeEmpty();
        tableCellResult.Warnings.Should().Contain(warning => warning.Code == "image-alt-text-defaulted");
    }

    [Fact]
    public void Phase6_InsertionPolicy_UnwrapsTablesPastedIntoTableCells()
    {
        var policy = new DocumentInsertionPolicy();
        var nestedTable = new DocumentBlock
        {
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
                                Blocks =
                                [
                                    new DocumentBlock
                                    {
                                        Type = DocumentBlockType.Paragraph,
                                        Content = new ParagraphBlockContent { Inlines = [new TextRun { Text = "Nested text" }] }
                                    }
                                ]
                            }
                        ]
                    }
                ]
            }
        };

        var result = policy.Apply([nestedTable], DocumentEditorRegion.TableCell);

        result.Blocks.Should().ContainSingle(block => block.Type == DocumentBlockType.Paragraph);
        result.Warnings.Should().Contain(warning => warning.Code == "table-unwrapped-in-table-cell");
    }

    [Fact]
    public void Phase6_InsertionPolicy_UnknownBlocksFallbackToParagraph()
    {
        var policy = new DocumentInsertionPolicy();
        var unknown = new DocumentBlock { Type = (DocumentBlockType)999 };

        var result = policy.Apply([unknown], DocumentEditorRegion.Body);

        result.Blocks.Should().ContainSingle(block => block.Type == DocumentBlockType.Paragraph);
        result.Warnings.Should().Contain(warning => warning.Code == "unknown-block-fallback");
    }

    [Fact]
    public void Phase6_PostFixer_AddsPlaceholdersAndMarksOrphansAndUnusedDraftAssets()
    {
        var liveBlock = new DocumentBlock { Id = "live-1", Type = DocumentBlockType.Paragraph };
        var document = new DocumentEditorDocument
        {
            Blocks =
            [
                liveBlock,
                new DocumentBlock
                {
                    Id = "table-1",
                    Type = DocumentBlockType.Table,
                    Content = new TableBlockContent
                    {
                        Rows = [new TableRowContent { Cells = [new TableCellContent { Id = "cell-1" }] }]
                    }
                }
            ],
            HeadersFooters = [new DocumentHeaderFooter { Id = "hf-1" }],
            Comments = [new DocumentComment { Id = "comment-1", Anchor = new DocumentCommentAnchor { BlockId = "missing-block" } }],
            Revisions = [new DocumentRevision { Id = "rev-1", Range = new DocumentRevisionRange { BlockId = "missing-block" } }],
            Assets = [new DocumentImageAsset { Id = "asset-1", IsLocalDraft = true }]
        };

        var result = new DocumentEditorPostFixer().Fix(document);

        ((TableBlockContent)document.Blocks[1].Content).Rows[0].Cells[0].Blocks.Should().ContainSingle(block => block.Type == DocumentBlockType.Paragraph);
        document.HeadersFooters[0].Blocks.Should().ContainSingle(block => block.Type == DocumentBlockType.Paragraph);
        document.Comments[0].Anchor.IsOrphaned.Should().BeTrue();
        document.Revisions.Should().ContainSingle(revision => revision.Id == "rev-1");
        document.Assets[0].IsUnusedDraft.Should().BeTrue();
        result.Warnings.Select(warning => warning.Code).Should().Contain([
            "empty-table-cell-placeholder",
            "empty-header-footer-placeholder",
            "orphaned-comment-anchor",
            "pending-revision-missing-range",
            "unused-image-asset-draft"
        ]);
    }
}
