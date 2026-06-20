using FluentAssertions;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Tests.Models.DocumentEditor;

public class DocumentEditorBlockAndReviewTests
{
    [Fact]
    public void Blocks_SupportParagraphHeadingListsQuotesImagesAndPageBreaks()
    {
        var blocks = new List<DocumentBlock>
        {
            new() { Type = DocumentBlockType.Heading, Order = 20, Content = new HeadingBlockContent { Level = 2 } },
            new() { Type = DocumentBlockType.Paragraph, Order = 10, Content = new ParagraphBlockContent() },
            new() { Type = DocumentBlockType.List, Order = 30, Content = new ListBlockContent { Ordered = false } },
            new() { Type = DocumentBlockType.List, Order = 40, Content = new ListBlockContent { Ordered = true, StartNumber = 3 } },
            new() { Type = DocumentBlockType.Quote, Order = 50, Content = new QuoteBlockContent() },
            new() { Type = DocumentBlockType.Image, Order = 60, Content = new ImageBlockContent { Source = DocumentImageSource.Url, Url = "https://example.test/a.png" } },
            new() { Type = DocumentBlockType.PageBreak, Order = 70, Content = new PageBreakBlockContent() }
        };

        blocks.OrderBy(block => block.Order).Select(block => block.Type).Should().Equal(
            DocumentBlockType.Paragraph,
            DocumentBlockType.Heading,
            DocumentBlockType.List,
            DocumentBlockType.List,
            DocumentBlockType.Quote,
            DocumentBlockType.Image,
            DocumentBlockType.PageBreak);

        ((ListBlockContent)blocks[3].Content).Ordered.Should().BeTrue();
        ((ImageBlockContent)blocks[5].Content).Url.Should().Be("https://example.test/a.png");
    }

    [Fact]
    public void Tables_SupportMergedCellsWithRowAndColumnSpans()
    {
        var table = new TableBlockContent
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
                            ColumnSpan = 2,
                            RowSpan = 3,
                            Merge = new TableCellMerge { IsOrigin = true }
                        },
                        new TableCellContent
                        {
                            Id = "cell-2",
                            Merge = new TableCellMerge { IsOrigin = false, OriginCellId = "cell-1" }
                        }
                    ]
                }
            ]
        };

        table.Rows[0].Cells[0].ColumnSpan.Should().Be(2);
        table.Rows[0].Cells[0].RowSpan.Should().Be(3);
        table.Rows[0].Cells[1].Merge.OriginCellId.Should().Be("cell-1");

        var span = new TableCellSpan { Columns = 4, Rows = 2 };
        span.Columns.Should().Be(4);
        span.Rows.Should().Be(2);
    }

    [Fact]
    public void Images_SupportUrlProviderAssetsClipboardDraftsSizingAndValidation()
    {
        var urlImage = new ImageBlockContent
        {
            Source = DocumentImageSource.Url,
            Url = "https://example.test/logo.png",
            AltText = "Logo",
            Caption = "Company logo",
            Size = new DocumentImageSize { Width = 240, Height = 120, LockAspectRatio = true }
        };

        var assetImage = new ImageBlockContent
        {
            Source = DocumentImageSource.Asset,
            AssetId = "asset-1"
        };

        var clipboard = new DocumentClipboardImage
        {
            LocalAssetId = "local-1",
            ContentType = "image/png",
            Bytes = [1, 2, 3]
        };

        var validation = new DocumentImageValidationOptions
        {
            AllowedContentTypes = ["image/png"],
            MaxFileSizeBytes = 3
        };

        urlImage.AltText.Should().Be("Logo");
        urlImage.Caption.Should().Be("Company logo");
        urlImage.Size.LockAspectRatio.Should().BeTrue();
        assetImage.AssetId.Should().Be("asset-1");
        clipboard.Bytes.Should().HaveCount(3);
        validation.IsAllowed("image/png", 3).Should().BeTrue();
        validation.IsAllowed("image/jpeg", 3).Should().BeFalse();
        validation.IsAllowed("image/png", 4).Should().BeFalse();
    }

    [Fact]
    public void Comments_SupportBlockTextRangeImportedThreadsResolvedAndExternalAuthors()
    {
        var comment = new DocumentComment
        {
            Id = "comment-1",
            Anchor = new DocumentCommentAnchor
            {
                Type = DocumentCommentAnchorType.TextRange,
                BlockId = "block-1",
                StartInlineIndex = 0,
                StartOffset = 2,
                EndInlineIndex = 0,
                EndOffset = 7
            },
            Visibility = DocumentCommentVisibility.External,
            Entries =
            [
                new DocumentCommentEntry
                {
                    Author = new DocumentEditorAuthor { Id = "client-1", DisplayName = "Client" },
                    IsExternalAuthor = true,
                    Text = "Please review."
                },
                new DocumentCommentEntry
                {
                    Author = new DocumentEditorAuthor { Id = "lawyer-1", DisplayName = "Lawyer" },
                    Text = "Resolved."
                }
            ],
            Status = DocumentCommentStatus.Resolved,
            ResolvedBy = new DocumentEditorAuthor { Id = "lawyer-1", DisplayName = "Lawyer" }
        };

        var importedDocx = new DocumentComment
        {
            Anchor = new DocumentCommentAnchor { Type = DocumentCommentAnchorType.ImportedDocx, ExternalAnchorId = "docx-42" },
            SourceFormat = "docx",
            ExternalId = "42"
        };

        var importedOdt = new DocumentComment
        {
            Anchor = new DocumentCommentAnchor { Type = DocumentCommentAnchorType.ImportedOdt, ExternalAnchorId = "odt-a1" },
            SourceFormat = "odt"
        };

        comment.Entries.Should().HaveCount(2);
        comment.Entries[0].IsExternalAuthor.Should().BeTrue();
        comment.Status.Should().Be(DocumentCommentStatus.Resolved);
        importedDocx.Anchor.Type.Should().Be(DocumentCommentAnchorType.ImportedDocx);
        importedOdt.Anchor.Type.Should().Be(DocumentCommentAnchorType.ImportedOdt);
    }
}
