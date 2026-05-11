using FluentAssertions;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Tests.Models.DocumentEditor;

public class DocumentEditorAdvancedFormatTests
{
    [Fact]
    public void Notes_SupportFootnotesEndnotesReferencesBodiesAndSectionNumbering()
    {
        var footnote = new DocumentNote
        {
            Id = "note-1",
            Type = DocumentNoteType.Footnote,
            SectionId = "section-1",
            ReferenceIds = ["ref-1", "ref-2"],
            Blocks =
            [
                new DocumentBlock { Type = DocumentBlockType.Paragraph, Content = new ParagraphBlockContent() }
            ]
        };

        var endnoteReference = new DocumentNoteReferenceRun
        {
            NoteId = "note-2",
            NoteType = DocumentNoteType.Endnote,
            DisplayMarker = "i"
        };

        var numbering = new DocumentNoteNumbering
        {
            Style = "lowerRoman",
            StartAt = 2,
            RestartEachSection = true
        };

        footnote.ReferenceIds.Should().HaveCount(2);
        footnote.Blocks.Should().ContainSingle();
        endnoteReference.NoteType.Should().Be(DocumentNoteType.Endnote);
        numbering.Style.Should().Be("lowerRoman");
        numbering.StartAt.Should().Be(2);
    }

    [Fact]
    public void HeadersFooters_SupportPrimaryFirstPageEvenOddAndRichContent()
    {
        var header = new DocumentHeaderFooter
        {
            Id = "header-1",
            Type = DocumentHeaderFooterType.Header,
            Scope = DocumentHeaderFooterScope.FirstPage,
            SectionId = "section-1",
            Blocks =
            [
                new DocumentBlock { Type = DocumentBlockType.Paragraph, Content = new ParagraphBlockContent() },
                new DocumentBlock { Type = DocumentBlockType.Image, Content = new ImageBlockContent { Source = DocumentImageSource.Asset, AssetId = "logo" } }
            ]
        };

        var footer = new DocumentHeaderFooter
        {
            Type = DocumentHeaderFooterType.Footer,
            Scope = DocumentHeaderFooterScope.EvenPages
        };

        header.Type.Should().Be(DocumentHeaderFooterType.Header);
        header.Scope.Should().Be(DocumentHeaderFooterScope.FirstPage);
        header.Blocks.Should().Contain(block => block.Type == DocumentBlockType.Image);
        footer.Type.Should().Be(DocumentHeaderFooterType.Footer);
        footer.Scope.Should().Be(DocumentHeaderFooterScope.EvenPages);
    }

    [Fact]
    public void Revisions_SupportInsertDeleteFormattingMoveAuthorTimestampAcceptReject()
    {
        var insertion = NewRevision(DocumentRevisionType.Insertion, DocumentRevisionAction.Pending);
        var deletion = NewRevision(DocumentRevisionType.Deletion, DocumentRevisionAction.Rejected);
        var formatting = NewRevision(DocumentRevisionType.Formatting, DocumentRevisionAction.Accepted);
        var move = NewRevision(DocumentRevisionType.Move, DocumentRevisionAction.Pending);
        move.Range.SourceBlockId = "old-block";

        insertion.Author.Id.Should().Be("author-1");
        insertion.CreatedAt.Should().BeAfter(DateTimeOffset.UtcNow.AddMinutes(-1));
        deletion.Action.Should().Be(DocumentRevisionAction.Rejected);
        formatting.Action.Should().Be(DocumentRevisionAction.Accepted);
        move.Range.SourceBlockId.Should().Be("old-block");
    }

    [Fact]
    public void FloatingLayout_SupportsWordLikeAnchorsRelativePositionWrappingAndZOrder()
    {
        var inlineLayout = new DocumentFloatingLayout
        {
            Inline = true,
            WrapMode = DocumentWrapMode.Inline
        };

        var floatingLayout = new DocumentFloatingLayout
        {
            Inline = false,
            HorizontalRelativeTo = DocumentRelativePosition.Page,
            VerticalRelativeTo = DocumentRelativePosition.Paragraph,
            X = 42,
            Y = 24,
            WrapMode = DocumentWrapMode.Square,
            ZIndex = 5
        };

        var preservedTight = new DocumentFloatingLayout
        {
            Inline = false,
            WrapMode = DocumentWrapMode.Tight,
            PreservedWrapMode = "tight"
        };

        var anchor = new DocumentAnchor
        {
            Type = DocumentAnchorType.FloatingObject,
            BlockId = "paragraph-1",
            FloatingLayout = floatingLayout
        };

        inlineLayout.Inline.Should().BeTrue();
        anchor.FloatingLayout!.HorizontalRelativeTo.Should().Be(DocumentRelativePosition.Page);
        anchor.FloatingLayout.VerticalRelativeTo.Should().Be(DocumentRelativePosition.Paragraph);
        anchor.FloatingLayout.WrapMode.Should().Be(DocumentWrapMode.Square);
        anchor.FloatingLayout.ZIndex.Should().Be(5);
        preservedTight.PreservedWrapMode.Should().Be("tight");
    }

    private static DocumentRevision NewRevision(DocumentRevisionType type, DocumentRevisionAction action)
    {
        return new DocumentRevision
        {
            Type = type,
            Action = action,
            Author = new DocumentRevisionAuthor { Id = "author-1", DisplayName = "Author" },
            Range = new DocumentRevisionRange
            {
                BlockId = "block-1",
                StartInlineIndex = 0,
                StartOffset = 1,
                EndInlineIndex = 0,
                EndOffset = 3
            }
        };
    }
}
