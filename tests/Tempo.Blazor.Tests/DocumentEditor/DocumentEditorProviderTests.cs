using FluentAssertions;
using Tempo.Blazor.DocumentEditor.Interfaces;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;

namespace Tempo.Blazor.Tests.DocumentEditor;

public class DocumentEditorProviderTests
{
    [Fact]
    public async Task Provider_LoadsDocumentAndRawJsonById()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedEmptyDocument("doc-1");

        var result = await provider.LoadAsync("doc-1");
        var rawJson = await provider.LoadJsonAsync("doc-1");

        result.Found.Should().BeTrue();
        result.Document!.DocumentId.Should().Be("doc-1");
        result.JsonSnapshot.Should().Contain("\"DocumentId\":\"doc-1\"");
        result.ConcurrencyToken.Should().NotBeNullOrWhiteSpace();
        rawJson.Should().Be(result.JsonSnapshot);
    }

    [Fact]
    public async Task Provider_SavesMaterializedDocumentAndReturnsNewConcurrencyToken()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedEmptyDocument("doc-1");
        var loaded = await provider.LoadAsync("doc-1");
        loaded.Document!.Blocks.Add(new DocumentBlock
        {
            Type = DocumentBlockType.Paragraph,
            Content = new ParagraphBlockContent { Inlines = [new TextRun { Text = "Hello" }] }
        });

        var saved = await provider.SaveAsync(new DocumentEditorSaveRequest
        {
            DocumentId = "doc-1",
            Document = loaded.Document,
            BaseConcurrencyToken = loaded.ConcurrencyToken
        });

        saved.Success.Should().BeTrue();
        saved.Conflict.Should().BeFalse();
        saved.ConcurrencyToken.Should().NotBe(loaded.ConcurrencyToken);
        saved.Document!.Blocks.Should().ContainSingle();
    }

    [Fact]
    public async Task Provider_SavesNormalizedRawJsonAndRejectsInvalidConcurrencyToken()
    {
        var provider = new InMemoryDocumentEditorProvider();
        var document = provider.SeedEmptyDocument("doc-1");
        var rawJson = DocumentEditorJson.Serialize(document);

        var conflict = await provider.SaveAsync(new DocumentEditorSaveRequest
        {
            DocumentId = "doc-1",
            JsonSnapshot = rawJson,
            BaseConcurrencyToken = "stale-token"
        });

        var loaded = await provider.LoadAsync("doc-1");
        var saved = await provider.SaveAsync(new DocumentEditorSaveRequest
        {
            DocumentId = "doc-1",
            JsonSnapshot = rawJson,
            BaseConcurrencyToken = loaded.ConcurrencyToken,
            NormalizeJson = true
        });

        conflict.Conflict.Should().BeTrue();
        saved.Success.Should().BeTrue();
        saved.JsonSnapshot.Should().Be(DocumentEditorJson.Normalize(rawJson));
    }

    [Fact]
    public async Task Provider_CreatesVersionsAndLoadsVersionHistory()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-1");

        var version = await provider.CreateVersionAsync(new DocumentVersionCreateRequest
        {
            DocumentId = "doc-1",
            Kind = DocumentVersionKind.Major,
            Description = "Approved",
            Author = new DocumentEditorAuthor { Id = "author-1", DisplayName = "Author" }
        });

        var versions = await provider.GetVersionsAsync("doc-1");

        version.Kind.Should().Be(DocumentVersionKind.Major);
        version.Snapshot.Hash.Should().HaveLength(64);
        versions.Should().ContainSingle(item => item.Id == version.Id);
    }

    [Fact]
    public async Task Provider_CreatesLoadsAndResolvesComments()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedEmptyDocument("doc-1");

        var created = await provider.CreateCommentAsync("doc-1", new DocumentComment
        {
            Id = "comment-1",
            Anchor = new DocumentCommentAnchor { Type = DocumentCommentAnchorType.Block, BlockId = "block-1" },
            Entries =
            [
                new DocumentCommentEntry
                {
                    Author = new DocumentEditorAuthor { Id = "author-1", DisplayName = "Author" },
                    Text = "Please review."
                }
            ]
        });

        var comments = await provider.GetCommentsAsync("doc-1");
        var resolved = await provider.ResolveCommentAsync(
            "doc-1",
            created.Id,
            new DocumentEditorAuthor { Id = "author-2", DisplayName = "Reviewer" });

        comments.Should().ContainSingle(item => item.Id == "comment-1");
        resolved.Status.Should().Be(DocumentCommentStatus.Resolved);
        resolved.ResolvedBy!.Id.Should().Be("author-2");
    }

    [Fact]
    public async Task AuditSink_RecordsEvents()
    {
        IDocumentAuditSink provider = new InMemoryDocumentEditorProvider();

        await provider.RecordAsync(new DocumentEditorAuditEvent
        {
            DocumentId = "doc-1",
            Action = DocumentEditorAuditAction.Open
        });

        ((InMemoryDocumentEditorProvider)provider).AuditEvents.Should().ContainSingle(item =>
            item.Action == DocumentEditorAuditAction.Open);
    }
}
