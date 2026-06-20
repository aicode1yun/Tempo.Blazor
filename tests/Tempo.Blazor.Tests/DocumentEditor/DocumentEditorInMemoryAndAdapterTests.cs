using FluentAssertions;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;
using Tempo.Blazor.NotionEditor.Enums;

namespace Tempo.Blazor.Tests.DocumentEditor;

public class DocumentEditorInMemoryAndAdapterTests
{
    [Fact]
    public async Task InMemoryProvider_SeedsDocumentsAndPersistsChanges()
    {
        var provider = new InMemoryDocumentEditorProvider();
        var empty = provider.SeedEmptyDocument("empty");
        var contract = provider.SeedContractDocument("contract");
        var filing = provider.SeedFilingDocument("filing");
        var loaded = await provider.LoadAsync("contract");
        loaded.Document!.Metadata.Title = "Updated contract";

        var saved = await provider.SaveAsync(new DocumentEditorSaveRequest
        {
            DocumentId = "contract",
            Document = loaded.Document,
            BaseConcurrencyToken = loaded.ConcurrencyToken
        });
        var reloaded = await provider.LoadAsync("contract");

        empty.Blocks.Should().BeEmpty();
        contract.Blocks.Should().NotBeEmpty();
        filing.Blocks.Should().NotBeEmpty();
        saved.Success.Should().BeTrue();
        reloaded.Document!.Metadata.Title.Should().Be("Updated contract");
    }

    [Fact]
    public async Task InMemoryProvider_PersistsVersioningAndCommentThread()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-1");

        await provider.CreateCommentAsync("doc-1", new DocumentComment
        {
            Id = "comment-1",
            Entries =
            [
                new DocumentCommentEntry
                {
                    Text = "Thread",
                    Author = new DocumentEditorAuthor { Id = "author-1", DisplayName = "Author" }
                }
            ]
        });
        await provider.CreateVersionAsync(new DocumentVersionCreateRequest
        {
            DocumentId = "doc-1",
            Kind = DocumentVersionKind.Minor,
            Author = new DocumentEditorAuthor { Id = "author-1", DisplayName = "Author" }
        });

        var comments = await provider.GetCommentsAsync("doc-1");
        var versions = await provider.GetVersionsAsync("doc-1");

        comments.Should().ContainSingle(item => item.Id == "comment-1");
        versions.Should().ContainSingle();
    }

    [Fact]
    public void NotionAdapter_ConvertsDocumentBlocksToNotionBlocks()
    {
        var document = DocumentEditorDocument.Empty("doc-1");
        document.Blocks.Add(new DocumentBlock
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = DocumentBlockType.Heading,
            Order = 1,
            Content = new HeadingBlockContent
            {
                Level = 2,
                Inlines = [new TextRun { Text = "Heading" }]
            }
        });
        document.Blocks.Add(new DocumentBlock
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = DocumentBlockType.Paragraph,
            Order = 2,
            Content = new ParagraphBlockContent
            {
                Inlines =
                [
                    new TextRun
                    {
                        Text = "Paragraph",
                        Marks =
                        [
                            new InlineMark
                            {
                                Type = InlineMarkType.CommentAnchor,
                                CommentAnchor = new CommentAnchorMarkData { CommentId = "comment-1" }
                            }
                        ]
                    }
                ]
            }
        });

        var adapter = new DocumentEditorNotionAdapter();
        var blocks = adapter.ToNotionBlocks(document, Guid.NewGuid());

        blocks.Should().HaveCount(2);
        blocks[0].Type.Should().Be(BlockType.Heading2);
        blocks[1].Type.Should().Be(BlockType.Paragraph);
    }

    [Fact]
    public void NotionAdapter_ConvertsNotionBlocksBackAndPreservesCommentAnchors()
    {
        var document = DocumentEditorDocument.Empty("doc-1");
        document.Blocks.Add(new DocumentBlock
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = DocumentBlockType.Paragraph,
            Order = 1,
            Content = new ParagraphBlockContent
            {
                Inlines =
                [
                    new TextRun
                    {
                        Text = "Paragraph",
                        Marks =
                        [
                            new InlineMark
                            {
                                Type = InlineMarkType.CommentAnchor,
                                CommentAnchor = new CommentAnchorMarkData { CommentId = "comment-1" }
                            }
                        ]
                    }
                ]
            }
        });

        var adapter = new DocumentEditorNotionAdapter();
        var notionBlocks = adapter.ToNotionBlocks(document, Guid.NewGuid());
        var converted = adapter.FromNotionBlocks("doc-2", notionBlocks);

        converted.DocumentId.Should().Be("doc-2");
        converted.Blocks.Should().ContainSingle(block => block.Type == DocumentBlockType.Paragraph);
        converted.Comments.Should().ContainSingle(comment =>
            comment.Id == "comment-1" &&
            comment.Anchor.Type == DocumentCommentAnchorType.Block);
    }
}
