using FluentAssertions;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentCollaborationTests
{
    [Fact]
    public async Task Provider_JoinsAndLeavesSession()
    {
        var provider = new InMemoryDocumentCollaborationProvider();
        var session = await provider.JoinAsync(Join("doc-1", "client-a"));

        session.DocumentId.Should().Be("doc-1");
        await provider.LeaveAsync(session.Id);

        await provider.Invoking(item => item.BroadcastOperationBatchAsync(session.Id, new DocumentOperationBatch { DocumentId = "doc-1" }))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Provider_BroadcastsOperationBatchAndReceivesRemoteBatches()
    {
        var provider = new InMemoryDocumentCollaborationProvider();
        var session = await provider.JoinAsync(Join("doc-1", "client-a"));
        var batch = new DocumentOperationBatch
        {
            DocumentId = "doc-1",
            Operations = [DocumentOperationEngineTests.InsertText("a", 0, "x")]
        };

        var sent = await provider.BroadcastOperationBatchAsync(session.Id, batch);
        var received = await provider.GetOperationBatchesAsync("doc-1", 0);

        sent.Sequence.Should().Be(1);
        received.Should().ContainSingle(item => item.Batch.Operations.Single().Text == "x");
    }

    [Fact]
    public async Task Provider_BroadcastsAndReceivesCursors()
    {
        var provider = new InMemoryDocumentCollaborationProvider();
        var session = await provider.JoinAsync(Join("doc-1", "client-a"));

        await provider.BroadcastCursorAsync(new DocumentCollaborationCursor
        {
            DocumentId = "doc-1",
            SessionId = session.Id,
            ClientId = "client-a",
            DisplayName = "User A",
            BlockId = "a",
            Offset = 2
        });

        var cursors = await provider.GetCursorsAsync("doc-1");

        cursors.Should().ContainSingle(cursor => cursor.DisplayName == "User A" && cursor.Offset == 2);
    }

    [Fact]
    public async Task Sync_LocalEditCreatesAndSubmitsOperationBatch()
    {
        var provider = new InMemoryDocumentCollaborationProvider();
        var sync = new DocumentCollaborationSync(provider);
        var before = DocumentOperationEngineTests.CreateDocument("doc-1", "a", "Before");
        var after = DocumentOperationEngineTests.CreateDocument("doc-1", "a", "After");
        await sync.JoinAsync(before, "client-a", Author("a"));

        var batch = sync.CreateLocalEditBatch(before, after);
        var result = await sync.SubmitLocalBatchAsync(batch);

        result.IsValid.Should().BeTrue();
        sync.IsDirty.Should().BeTrue();
        DocumentOperationEngineTests.TextOf(sync.Document, "a").Should().Be("After");
        (await provider.GetOperationBatchesAsync("doc-1", 0)).Should().ContainSingle();
    }

    [Fact]
    public async Task Sync_RemoteOperationAppliesWithoutLosingLocalDirtyState()
    {
        var provider = new InMemoryDocumentCollaborationProvider();
        var sync = new DocumentCollaborationSync(provider);
        var document = DocumentOperationEngineTests.CreateDocument("doc-1", "a", "Alpha");
        await sync.JoinAsync(document, "client-a", Author("a"));
        await sync.SubmitLocalBatchAsync(new DocumentOperationBatch
        {
            DocumentId = "doc-1",
            Operations = [DocumentOperationEngineTests.InsertText("a", 5, "!")]
        });

        var remote = new DocumentCollaborationOperationBatch
        {
            Sequence = 2,
            SessionId = "remote",
            Batch = new DocumentOperationBatch
            {
                DocumentId = "doc-1",
                Operations = [DocumentOperationEngineTests.InsertText("a", 0, "Remote ")]
            }
        };

        var result = sync.ApplyRemoteBatch(remote);

        result.IsValid.Should().BeTrue();
        sync.IsDirty.Should().BeTrue();
        DocumentOperationEngineTests.TextOf(sync.Document, "a").Should().Contain("Remote");
    }

    [Fact]
    public async Task Sync_RemoteCursorIsAvailableAfterRefresh()
    {
        var provider = new InMemoryDocumentCollaborationProvider();
        var local = new DocumentCollaborationSync(provider);
        var remote = new DocumentCollaborationSync(provider);
        var document = DocumentOperationEngineTests.CreateDocument("doc-1", "a", "Alpha");
        await local.JoinAsync(document, "local", Author("local"));
        await remote.JoinAsync(document, "remote", Author("remote"));

        await remote.UpdateCursorAsync(new DocumentCollaborationCursor { DisplayName = "Remote", BlockId = "a", Offset = 1 });
        await local.UpdateCursorAsync(new DocumentCollaborationCursor { DisplayName = "Local", BlockId = "a", Offset = 0 });

        local.RemoteCursors.Should().ContainSingle(cursor => cursor.DisplayName == "Remote");
    }

    [Fact]
    public async Task Sync_ReconnectCatchesUpFromOperationLog()
    {
        var provider = new InMemoryDocumentCollaborationProvider();
        var remoteSession = await provider.JoinAsync(Join("doc-1", "remote"));
        await provider.BroadcastOperationBatchAsync(remoteSession.Id, new DocumentOperationBatch
        {
            DocumentId = "doc-1",
            Operations = [DocumentOperationEngineTests.InsertText("a", 5, "!")]
        });

        var sync = new DocumentCollaborationSync(provider);
        var document = DocumentOperationEngineTests.CreateDocument("doc-1", "a", "Alpha");
        await sync.JoinAsync(document, "local", Author("local"), lastSeenSequence: 0);

        var result = await sync.ReconnectAsync();

        result.IsValid.Should().BeTrue();
        sync.LastSeenSequence.Should().Be(1);
        DocumentOperationEngineTests.TextOf(sync.Document, "a").Should().Be("Alpha!");
    }

    private static DocumentCollaborationJoinRequest Join(string documentId, string clientId)
    {
        return new DocumentCollaborationJoinRequest
        {
            DocumentId = documentId,
            ClientId = clientId,
            Author = Author(clientId)
        };
    }

    private static DocumentEditorAuthor Author(string id)
    {
        return new DocumentEditorAuthor { Id = id, DisplayName = id };
    }
}
