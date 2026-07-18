using System.Text.Json;
using FluentAssertions;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;

namespace Tempo.Blazor.Tests.DocumentEditor;

/// <summary>
/// Multi-instance collaboration: two BackplaneDocumentCollaborationProvider instances sharing a
/// backplane must fan operation batches and cursors out to each other (without echo duplication),
/// so clients connected to different server instances see the same operation stream.
/// </summary>
public class DocumentCollaborationBackplaneTests
{
    [Fact]
    public async Task OperationBatch_BroadcastOnInstanceA_IsVisibleOnInstanceB()
    {
        var backplane = new InMemoryDocumentCollaborationBackplane();
        await using var instanceA = new BackplaneDocumentCollaborationProvider(backplane);
        await using var instanceB = new BackplaneDocumentCollaborationProvider(backplane);

        var sessionA = await instanceA.JoinAsync(Join("doc-1", "user-a"));
        await instanceB.JoinAsync(Join("doc-1", "user-b"));

        await instanceA.BroadcastOperationBatchAsync(sessionA.Id, Batch("op-1", "Ahoj"));

        var onB = await instanceB.GetOperationBatchesAsync("doc-1", afterSequence: 0);
        onB.Should().ContainSingle("the batch must fan out to the second instance");
        onB[0].Batch.Operations.Should().ContainSingle(operation => operation.Text == "Ahoj");

        var onA = await instanceA.GetOperationBatchesAsync("doc-1", afterSequence: 0);
        onA.Should().ContainSingle("the source instance must not ingest its own echo");
    }

    [Fact]
    public async Task Cursor_BroadcastOnInstanceA_IsVisibleOnInstanceB()
    {
        var backplane = new InMemoryDocumentCollaborationBackplane();
        await using var instanceA = new BackplaneDocumentCollaborationProvider(backplane);
        await using var instanceB = new BackplaneDocumentCollaborationProvider(backplane);

        var sessionA = await instanceA.JoinAsync(Join("doc-1", "user-a"));
        await instanceB.JoinAsync(Join("doc-1", "user-b"));

        await instanceA.BroadcastCursorAsync(new DocumentCollaborationCursor
        {
            DocumentId = "doc-1",
            SessionId = sessionA.Id,
            BlockId = "b1",
            Offset = 4,
        });

        var cursors = await instanceB.GetCursorsAsync("doc-1");
        cursors.Should().Contain(cursor => cursor.SessionId == sessionA.Id && cursor.Offset == 4);
    }

    [Fact]
    public async Task Batches_ForOtherDocuments_DoNotLeakAcrossSubscriptions()
    {
        var backplane = new InMemoryDocumentCollaborationBackplane();
        await using var instanceA = new BackplaneDocumentCollaborationProvider(backplane);
        await using var instanceB = new BackplaneDocumentCollaborationProvider(backplane);

        var sessionA = await instanceA.JoinAsync(Join("doc-1", "user-a"));
        await instanceB.JoinAsync(Join("doc-2", "user-b"));

        await instanceA.BroadcastOperationBatchAsync(sessionA.Id, Batch("op-1", "Ahoj"));

        (await instanceB.GetOperationBatchesAsync("doc-1", 0)).Should().BeEmpty(
            "instance B never joined doc-1, so it has no subscription for it");
        (await instanceB.GetOperationBatchesAsync("doc-2", 0)).Should().BeEmpty();
    }

    [Fact]
    public void BackplaneMessage_RoundTripsThroughJson()
    {
        var message = new DocumentCollaborationBackplaneMessage
        {
            DocumentId = "doc-1",
            SourceInstanceId = "instance-1",
            Batch = new DocumentCollaborationOperationBatch
            {
                Sequence = 7,
                SessionId = "session-1",
                Batch = Batch("op-1", "Žluťoučký kůň"),
            },
        };

        var json = JsonSerializer.Serialize(message, DocumentEditorJson.Options);
        var restored = JsonSerializer.Deserialize<DocumentCollaborationBackplaneMessage>(json, DocumentEditorJson.Options);

        restored!.DocumentId.Should().Be("doc-1");
        restored.SourceInstanceId.Should().Be("instance-1");
        restored.Batch!.Batch.Operations.Single().Text.Should().Be("Žluťoučký kůň");
    }

    private static DocumentCollaborationJoinRequest Join(string documentId, string userId)
        => new()
        {
            DocumentId = documentId,
            Author = new DocumentEditorAuthor { Id = userId, DisplayName = userId },
        };

    private static DocumentOperationBatch Batch(string operationId, string text)
        => new()
        {
            Id = $"batch-{operationId}",
            DocumentId = "doc-1",
            ClientId = "c1",
            Operations =
            [
                new DocumentOperation
                {
                    OperationId = operationId,
                    Type = DocumentOperationType.InsertText,
                    Target = new DocumentOperationTarget { BlockId = "b1", InlineIndex = 0, Offset = 0 },
                    Text = text,
                    Metadata = new DocumentOperationMetadata { LogicalTimestamp = 1, ClientId = "c1", AuthorId = "a1" },
                },
            ],
        };
}
