using FluentAssertions;
using System.Text.Json;
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
    public async Task Provider_BroadcastStoresProtocolVersion()
    {
        var provider = new InMemoryDocumentCollaborationProvider();
        var session = await provider.JoinAsync(Join("doc-1", "client-a"));
        var operation = DocumentOperationEngineTests.InsertText("a", 0, "x");
        operation.OperationId = "operation-1";
        var batch = new DocumentOperationBatch
        {
            DocumentId = "doc-1",
            ProtocolVersion = DocumentOperationBatch.CurrentProtocolVersion,
            Operations = [operation]
        };

        var sent = await provider.BroadcastOperationBatchAsync(session.Id, batch);
        var received = await provider.GetOperationBatchesAsync("doc-1", 0);

        sent.Batch.ProtocolVersion.Should().Be(DocumentOperationBatch.CurrentProtocolVersion);
        received.Should().ContainSingle(item =>
            item.SessionId == session.Id
            && item.Batch.ProtocolVersion == DocumentOperationBatch.CurrentProtocolVersion
            && item.Batch.Operations.Single().OperationId == "operation-1");
    }

    [Fact]
    public async Task Provider_AdvancesSequenceFloorFromJoiningClient()
    {
        var provider = new InMemoryDocumentCollaborationProvider();
        var session = await provider.JoinAsync(new DocumentCollaborationJoinRequest
        {
            DocumentId = "doc-1",
            ClientId = "client-a",
            Author = Author("author-a"),
            LastSeenSequence = 42
        });
        var operation = DocumentOperationEngineTests.InsertText("a", 0, "x");
        operation.OperationId = "operation-1";

        var sent = await provider.BroadcastOperationBatchAsync(session.Id, new DocumentOperationBatch
        {
            DocumentId = "doc-1",
            Operations = [operation]
        });

        sent.Sequence.Should().Be(43);
    }

    [Fact]
    public async Task Provider_RejectsUnsupportedHigherProtocolVersion()
    {
        var provider = new InMemoryDocumentCollaborationProvider();
        var session = await provider.JoinAsync(Join("doc-1", "client-a"));
        var operation = DocumentOperationEngineTests.InsertText("a", 0, "x");
        operation.OperationId = "operation-1";
        var batch = new DocumentOperationBatch
        {
            DocumentId = "doc-1",
            ProtocolVersion = DocumentOperationBatch.CurrentProtocolVersion + 1,
            Operations = [operation]
        };

        await provider.Invoking(item => item.BroadcastOperationBatchAsync(session.Id, batch))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Unsupported collaboration protocol version*");
    }

    [Fact]
    public async Task Sync_SubmitLocalBatchReturnsValidationErrorForUnsupportedHigherProtocolVersion()
    {
        var provider = new InMemoryDocumentCollaborationProvider();
        var sync = new DocumentCollaborationSync(provider);
        var document = DocumentOperationEngineTests.CreateDocument("doc-1", "a", "Alpha");
        await sync.JoinAsync(document, "client-a", Author("author-a"));
        var operation = DocumentOperationEngineTests.InsertText("a", 5, "!");
        operation.OperationId = "operation-1";
        var batch = new DocumentOperationBatch
        {
            DocumentId = "doc-1",
            ProtocolVersion = DocumentOperationBatch.CurrentProtocolVersion + 1,
            Operations = [operation]
        };

        var result = await sync.SubmitLocalBatchAsync(batch);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.Contains("Unsupported collaboration protocol version", StringComparison.Ordinal));
        DocumentOperationEngineTests.TextOf(sync.Document, "a").Should().Be("Alpha");
    }

    [Fact]
    public void Protocol_UpgradesLegacyTextOnlyBatch()
    {
        var legacy = JsonSerializer.Deserialize<DocumentOperationBatch>(
            """
            {
              "Id": "legacy-batch",
              "DocumentId": "doc-1",
              "ProtocolVersion": 0,
              "Operations": [
                {
                  "OperationId": "legacy-operation",
                  "SchemaVersion": 1,
                  "Type": 7,
                  "Target": { "BlockId": "a" },
                  "AttributeName": "text",
                  "AttributeValueJson": "\"Legacy text\""
                }
              ]
            }
            """,
            DocumentEditorJson.Options)!;

        var result = DocumentOperationBatchProtocol.Normalize(legacy);

        result.IsValid.Should().BeTrue();
        legacy.ProtocolVersion.Should().Be(DocumentOperationBatch.CurrentProtocolVersion);
        legacy.Operations.Should().ContainSingle(operation =>
            operation.OperationId == "legacy-operation"
            && operation.Type == DocumentOperationType.SetBlockAttribute
            && operation.AttributeName == "text");
    }

    [Fact]
    public async Task SignalRWrapper_PushesRemoteOperationBatch()
    {
        var provider = new SignalRDocumentCollaborationProvider(new InMemoryDocumentCollaborationProvider());
        var received = new List<DocumentCollaborationOperationBatch>();
        var cursors = new List<DocumentCollaborationCursor>();
        provider.RemoteOperationBatchReceived += (batch, _) =>
        {
            received.Add(batch);
            return Task.CompletedTask;
        };
        provider.RemoteCursorReceived += (cursor, _) =>
        {
            cursors.Add(cursor);
            return Task.CompletedTask;
        };
        var remote = new DocumentCollaborationOperationBatch
        {
            Sequence = 2,
            SessionId = "remote-session",
            Batch = new DocumentOperationBatch
            {
                DocumentId = "doc-1",
                Operations = [DocumentOperationEngineTests.InsertText("a", 0, "Remote ")]
            }
        };

        await provider.ReceiveRemoteOperationBatchAsync(remote);
        await provider.ReceiveRemoteCursorAsync(new DocumentCollaborationCursor
        {
            DocumentId = "doc-1",
            SessionId = "remote-session",
            DisplayName = "Remote",
            BlockId = "a",
            Offset = 1
        });

        received.Should().ContainSingle(batch =>
            batch.Sequence == 2
            && batch.SessionId == "remote-session"
            && batch.Batch.Operations.Single().Text == "Remote ");
        cursors.Should().ContainSingle(cursor => cursor.DisplayName == "Remote" && cursor.Offset == 1);
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
    public async Task Sync_RealtimeCursorUpdateDoesNotPollCursors()
    {
        var transport = new CountingCollaborationProvider();
        var provider = new SignalRDocumentCollaborationProvider(transport);
        var sync = new DocumentCollaborationSync(provider);
        var document = DocumentOperationEngineTests.CreateDocument("doc-1", "a", "Alpha");
        await sync.JoinAsync(document, "client-a", Author("client-a"));

        await sync.UpdateCursorAsync(new DocumentCollaborationCursor
        {
            DisplayName = "Client A",
            BlockId = "a",
            Offset = 1
        });

        transport.GetCursorsCalls.Should().Be(0);
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
    public async Task Sync_SubmitLocalBatchStampsOriginSessionAndMissingOperationIds()
    {
        var provider = new InMemoryDocumentCollaborationProvider();
        var sync = new DocumentCollaborationSync(provider);
        var document = DocumentOperationEngineTests.CreateDocument("doc-1", "a", "Alpha");
        await sync.JoinAsync(document, "client-a", Author("author-a"));
        var operation = DocumentOperationEngineTests.InsertText("a", 5, "!");
        operation.OperationId = string.Empty;
        operation.Metadata = new DocumentOperationMetadata();
        var batch = new DocumentOperationBatch
        {
            DocumentId = "doc-1",
            Operations = [operation]
        };

        var result = await sync.SubmitLocalBatchAsync(batch);

        result.IsValid.Should().BeTrue();
        operation.OperationId.Should().NotBeNullOrWhiteSpace();
        operation.Metadata.OriginSessionId.Should().Be(sync.Session!.Id);
        operation.Metadata.ClientId.Should().Be("client-a");
        operation.Metadata.AuthorId.Should().Be("author-a");
    }

    [Fact]
    public void Sync_CreateLocalEditBatch_TextChangeUsesStructuredBlockUpdate()
    {
        var sync = new DocumentCollaborationSync(new InMemoryDocumentCollaborationProvider());
        var before = DocumentOperationEngineTests.CreateDocument("doc-1", "a", "Before");
        var after = DocumentOperationEngineTests.CreateDocument("doc-1", "a", "After");

        var batch = sync.CreateLocalEditBatch(before, after);

        var operation = batch.Operations.Should().ContainSingle().Subject;
        operation.Type.Should().Be(DocumentOperationType.UpdateBlock);
        operation.AttributeName.Should().BeNull();
        operation.AttributeValueJson.Should().BeNull();
        operation.Block.Should().NotBeNull();
        DocumentOperationEngineTests.TextOf(new DocumentEditorDocument
        {
            DocumentId = "doc-1",
            Blocks = [operation.Block!]
        }, "a").Should().Be("After");
    }

    [Fact]
    public async Task Sync_CreateLocalPatchBatch_MapsWysiwygInsertTextWithoutSnapshotDiff()
    {
        var provider = new InMemoryDocumentCollaborationProvider();
        var sync = new DocumentCollaborationSync(provider);
        var before = DocumentOperationEngineTests.CreateDocument("doc-1", "a", "Alpha");
        ((ParagraphBlockContent)before.Blocks[0].Content).Inlines[0].Id = "inline-a";
        await sync.JoinAsync(before, "client-a", Author("author-a"));
        var patch = new WysiwygPatch
        {
            Type = "InsertText",
            Data = "!",
            TransactionId = "tx-1",
            Selection = new WysiwygSelectionSnapshot
            {
                AnchorBlockId = "a",
                AnchorInlineId = "inline-a",
                AnchorOffset = 5,
                FocusBlockId = "a",
                FocusInlineId = "inline-a",
                FocusOffset = 5,
                IsCollapsed = true
            }
        };

        var batch = sync.CreateLocalPatchBatch(before, patch);

        var operation = batch.Operations.Should().ContainSingle().Subject;
        operation.Type.Should().Be(DocumentOperationType.InsertText);
        operation.AttributeName.Should().BeNull();
        operation.AttributeValueJson.Should().BeNull();
        operation.Target.InlineId.Should().Be("inline-a");
        operation.Target.Offset.Should().Be(5);
        operation.Target.Length.Should().Be(1);
        operation.Metadata.TransactionId.Should().Be("tx-1");
        operation.Metadata.OriginSessionId.Should().Be(sync.Session!.Id);
    }

    [Fact]
    public async Task Sync_CreateLocalPatchBatch_StampsBatchTransactionIdentityAndCursorAfter()
    {
        var provider = new InMemoryDocumentCollaborationProvider();
        var sync = new DocumentCollaborationSync(provider);
        var before = DocumentOperationEngineTests.CreateDocument("doc-1", "a", "Alpha");
        ((ParagraphBlockContent)before.Blocks[0].Content).Inlines[0].Id = "inline-a";
        await sync.JoinAsync(before, "client-a", Author("author-a"));

        var first = sync.CreateLocalPatchBatch(before, new WysiwygPatch
        {
            Type = "InsertText",
            Data = "!",
            TransactionId = "tx-1",
            Selection = new WysiwygSelectionSnapshot
            {
                AnchorBlockId = "a",
                AnchorInlineId = "inline-a",
                AnchorOffset = 5,
                FocusBlockId = "a",
                FocusInlineId = "inline-a",
                FocusOffset = 5,
                IsCollapsed = true
            },
            AfterSelection = new WysiwygSelectionSnapshot
            {
                AnchorBlockId = "a",
                AnchorInlineId = "inline-a",
                AnchorOffset = 6,
                FocusBlockId = "a",
                FocusInlineId = "inline-a",
                FocusOffset = 6,
                IsCollapsed = true
            }
        });
        var second = sync.CreateLocalPatchBatch(before, new WysiwygPatch
        {
            Type = "InsertText",
            Data = "?",
            TransactionId = "tx-2",
            Selection = new WysiwygSelectionSnapshot
            {
                AnchorBlockId = "a",
                AnchorInlineId = "inline-a",
                AnchorOffset = 6,
                IsCollapsed = true
            }
        });

        first.ClientId.Should().Be("client-a");
        first.TransactionId.Should().Be("tx-1");
        first.LocalSequence.Should().Be(1);
        first.SelectionAfter.Should().NotBeNull();
        first.SelectionAfter!.AnchorOffset.Should().Be(6);
        second.LocalSequence.Should().Be(2);
        second.TransactionId.Should().Be("tx-2");
    }

    [Fact]
    public async Task Sync_CreateLocalPatchBatch_MapsWysiwygToggleMarkWithoutSnapshotDiff()
    {
        var provider = new InMemoryDocumentCollaborationProvider();
        var sync = new DocumentCollaborationSync(provider);
        var before = DocumentOperationEngineTests.CreateDocument("doc-1", "a", "Alpha");
        ((ParagraphBlockContent)before.Blocks[0].Content).Inlines[0].Id = "inline-a";
        await sync.JoinAsync(before, "client-a", Author("author-a"));
        var patch = new WysiwygPatch
        {
            Type = "ToggleMark",
            MarkType = "Underline",
            Selection = new WysiwygSelectionSnapshot
            {
                AnchorBlockId = "a",
                AnchorInlineId = "inline-a",
                AnchorOffset = 1,
                FocusBlockId = "a",
                FocusInlineId = "inline-a",
                FocusOffset = 4,
                IsCollapsed = false
            }
        };

        var batch = sync.CreateLocalPatchBatch(before, patch);

        var operation = batch.Operations.Should().ContainSingle().Subject;
        operation.Type.Should().Be(DocumentOperationType.AddInlineMark);
        operation.Mark!.Type.Should().Be(InlineMarkType.Underline);
        operation.AttributeName.Should().BeNull();
        operation.AttributeValueJson.Should().BeNull();
        operation.Target.InlineId.Should().Be("inline-a");
        operation.Target.Offset.Should().Be(1);
        operation.Target.Length.Should().Be(3);
        operation.Metadata.OriginSessionId.Should().Be(sync.Session!.Id);
    }

    [Fact]
    public async Task Sync_CreateLocalPatchBatch_MapsTrackedInsertionToCreateRevision()
    {
        var provider = new InMemoryDocumentCollaborationProvider();
        var sync = new DocumentCollaborationSync(provider);
        var before = DocumentOperationEngineTests.CreateDocument("doc-1", "a", "Alpha");
        ((ParagraphBlockContent)before.Blocks[0].Content).Inlines[0].Id = "inline-a";
        await sync.JoinAsync(before, "client-a", Author("author-a"));

        var batch = sync.CreateLocalPatchBatch(before, new WysiwygPatch
        {
            Type = "InsertText",
            Data = "Draft ",
            RevisionId = "rev-insert",
            RevisionType = "Insertion",
            Selection = new WysiwygSelectionSnapshot
            {
                AnchorBlockId = "a",
                AnchorInlineId = "inline-a",
                AnchorOffset = 0,
                IsCollapsed = true
            }
        });

        var operation = batch.Operations.Should().ContainSingle().Subject;
        operation.Type.Should().Be(DocumentOperationType.CreateRevision);
        operation.Revision!.Type.Should().Be(DocumentRevisionType.Insertion);
        operation.Metadata.OriginSessionId.Should().Be(sync.Session!.Id);
        operation.Metadata.RevisionId.Should().Be("rev-insert");
    }

    [Fact]
    public async Task Sync_CreateLocalEditBatch_MapsAcceptedRevisionToAcceptRevisionOperation()
    {
        var provider = new InMemoryDocumentCollaborationProvider();
        var sync = new DocumentCollaborationSync(provider);
        var before = DocumentOperationEngineTests.CreateDocument("doc-1", "a", "Draft Alpha");
        before.Revisions = [Revision("rev-1", DocumentRevisionType.Insertion, DocumentRevisionAction.Pending, "a")];
        var after = Clone(before);
        after.Revisions[0].Action = DocumentRevisionAction.Accepted;
        await sync.JoinAsync(before, "client-a", Author("author-a"));

        var batch = sync.CreateLocalEditBatch(before, after);

        var operation = batch.Operations.Should().ContainSingle().Subject;
        operation.Type.Should().Be(DocumentOperationType.AcceptRevision);
        operation.Revision!.Id.Should().Be("rev-1");
        operation.Metadata.RevisionId.Should().Be("rev-1");
    }

    [Fact]
    public async Task Sync_ReconnectExposesRemoteRevisionOperationsForWysiwygPatch()
    {
        var provider = new InMemoryDocumentCollaborationProvider();
        var sync = new DocumentCollaborationSync(provider);
        var document = DocumentOperationEngineTests.CreateDocument("doc-1", "a", "Alpha");
        ((ParagraphBlockContent)document.Blocks[0].Content).Inlines[0].Id = "inline-a";
        await sync.JoinAsync(document, "client-a", Author("a"));
        var remoteSession = await provider.JoinAsync(Join("doc-1", "client-b"));
        await provider.BroadcastOperationBatchAsync(remoteSession.Id, new DocumentOperationBatch
        {
            DocumentId = "doc-1",
            Operations =
            [
                new DocumentOperation
                {
                    Type = DocumentOperationType.CreateRevision,
                    Target = new DocumentOperationTarget
                    {
                        BlockId = "a",
                        InlineId = "inline-a",
                        InlineIndex = 0,
                        Offset = 0,
                        Length = 7
                    },
                    Text = "Remote ",
                    Revision = Revision("rev-remote", DocumentRevisionType.Insertion, DocumentRevisionAction.Pending, "a", "Remote ")
                }
            ]
        });

        var result = await sync.ReconnectAsync();

        result.IsValid.Should().BeTrue();
        sync.LastAppliedRemoteOperations.Should().ContainSingle(operation =>
            operation.Type == DocumentOperationType.CreateRevision
            && operation.Revision!.Id == "rev-remote");
        sync.Document.Revisions.Should().ContainSingle(revision => revision.Id == "rev-remote");
    }

    [Fact]
    public void Sync_CreateLocalEditBatch_FormattingOnlyChangeUsesStructuredBlockUpdate()
    {
        var sync = new DocumentCollaborationSync(new InMemoryDocumentCollaborationProvider());
        var before = DocumentOperationEngineTests.CreateDocument("doc-1", "a", "Styled text");
        var after = DocumentOperationEngineTests.CreateDocument("doc-1", "a", "Styled text");
        var inlines = ((ParagraphBlockContent)after.Blocks[0].Content).Inlines;
        inlines[0].Marks.Add(new InlineMark { Type = InlineMarkType.Bold });

        var batch = sync.CreateLocalEditBatch(before, after);

        var operation = batch.Operations.Should().ContainSingle().Subject;
        operation.Type.Should().Be(DocumentOperationType.UpdateBlock);
        operation.AttributeName.Should().BeNull();
        operation.Block.Should().NotBeNull();
        var marks = ((ParagraphBlockContent)operation.Block!.Content).Inlines.OfType<TextRun>().Single().Marks;
        marks.Should().ContainSingle(mark => mark.Type == InlineMarkType.Bold);
    }

    [Fact]
    public void Sync_CreateLocalEditBatch_ImageUpdateUsesStructuredBlockUpdate()
    {
        var sync = new DocumentCollaborationSync(new InMemoryDocumentCollaborationProvider());
        var before = CreateImageDocument("doc-1", "image-1", "Before alt");
        var after = CreateImageDocument("doc-1", "image-1", "After alt");

        var batch = sync.CreateLocalEditBatch(before, after);

        var operation = batch.Operations.Should().ContainSingle().Subject;
        operation.Type.Should().Be(DocumentOperationType.UpdateBlock);
        operation.AttributeName.Should().BeNull();
        operation.Block.Should().NotBeNull();
        var image = operation.Block!.Content.Should().BeOfType<ImageBlockContent>().Subject;
        image.AltText.Should().Be("After alt");
        image.Url.Should().Be("https://example.test/image.png");
        image.Size.Width.Should().Be(320);
    }

    [Fact]
    public async Task Sync_LocalEditBatch_RoundtripPreservesInlineMarks()
    {
        var provider = new InMemoryDocumentCollaborationProvider();
        var local = new DocumentCollaborationSync(provider);
        var remote = new DocumentCollaborationSync(provider);
        var before = DocumentOperationEngineTests.CreateDocument("doc-1", "a", "Styled text");
        var after = Clone(before);
        ((ParagraphBlockContent)after.Blocks[0].Content).Inlines.OfType<TextRun>().Single()
            .Marks.Add(new InlineMark { Type = InlineMarkType.Italic });
        await local.JoinAsync(before, "client-a", Author("a"));
        await remote.JoinAsync(before, "client-b", Author("b"));

        var batch = local.CreateLocalEditBatch(before, after);
        var submit = await local.SubmitLocalBatchAsync(batch);
        var reconnect = await remote.ReconnectAsync();

        submit.IsValid.Should().BeTrue();
        reconnect.IsValid.Should().BeTrue();
        var marks = DocumentOperationEngineTests.InlinesOf(remote.Document, "a")
            .OfType<TextRun>()
            .Single()
            .Marks;
        marks.Should().ContainSingle(mark => mark.Type == InlineMarkType.Italic);
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
    public async Task Sync_ReconnectExposesRemoteMarkOperationsForWysiwygPatch()
    {
        var provider = new InMemoryDocumentCollaborationProvider();
        var sync = new DocumentCollaborationSync(provider);
        var document = DocumentOperationEngineTests.CreateDocument("doc-1", "a", "Alpha");
        ((ParagraphBlockContent)document.Blocks[0].Content).Inlines[0].Id = "inline-a";
        await sync.JoinAsync(document, "client-a", Author("a"));
        var remoteSession = await provider.JoinAsync(Join("doc-1", "client-b"));
        await provider.BroadcastOperationBatchAsync(remoteSession.Id, new DocumentOperationBatch
        {
            DocumentId = "doc-1",
            Operations =
            [
                DocumentOperationEngineTests.AddInlineMark("a", "inline-a", 0, 5, InlineMarkType.Bold, clientId: "client-b")
            ]
        });

        var result = await sync.ReconnectAsync();

        result.IsValid.Should().BeTrue();
        sync.LastAppliedRemoteOperations.Should().ContainSingle(operation =>
            operation.Type == DocumentOperationType.AddInlineMark
            && operation.Mark!.Type == InlineMarkType.Bold);
    }

    [Fact]
    public async Task Sync_ApplyRemoteBatchIsIdempotentByOperationId()
    {
        var provider = new InMemoryDocumentCollaborationProvider();
        var sync = new DocumentCollaborationSync(provider);
        var document = DocumentOperationEngineTests.CreateDocument("doc-1", "a", "Alpha");
        var operation = DocumentOperationEngineTests.InsertText("a", 5, "!");
        await sync.JoinAsync(document, "client-a", Author("a"));
        var remote = new DocumentCollaborationOperationBatch
        {
            Sequence = 1,
            SessionId = "remote",
            Batch = new DocumentOperationBatch
            {
                DocumentId = "doc-1",
                Operations = [operation]
            }
        };

        sync.ApplyRemoteBatch(remote).IsValid.Should().BeTrue();
        sync.ApplyRemoteBatch(remote).IsValid.Should().BeTrue();

        DocumentOperationEngineTests.TextOf(sync.Document, "a").Should().Be("Alpha!");
    }

    [Fact]
    public async Task Sync_ReconnectIgnoresOwnSessionEcho()
    {
        var provider = new InMemoryDocumentCollaborationProvider();
        var sync = new DocumentCollaborationSync(provider);
        var document = DocumentOperationEngineTests.CreateDocument("doc-1", "a", "Alpha");
        var session = await sync.JoinAsync(document, "client-a", Author("a"), lastSeenSequence: 0);
        await provider.BroadcastOperationBatchAsync(session.Id, new DocumentOperationBatch
        {
            DocumentId = "doc-1",
            Operations = [DocumentOperationEngineTests.InsertText("a", 5, "!")]
        });

        var reconnect = await sync.ReconnectAsync();

        reconnect.IsValid.Should().BeTrue();
        session.Id.Should().Be(sync.Session!.Id);
        DocumentOperationEngineTests.TextOf(sync.Document, "a").Should().Be("Alpha");
        sync.LastSeenSequence.Should().Be(1);
    }

    [Fact]
    public async Task Sync_ReconnectDoesNotIgnoreSameClientIdFromDifferentSession()
    {
        var provider = new InMemoryDocumentCollaborationProvider();
        var local = new DocumentCollaborationSync(provider);
        var remote = new DocumentCollaborationSync(provider);
        var document = DocumentOperationEngineTests.CreateDocument("doc-1", "a", "Alpha");
        await local.JoinAsync(document, "shared-client", Author("local"), lastSeenSequence: 0);
        await remote.JoinAsync(document, "shared-client", Author("remote"), lastSeenSequence: 0);
        await remote.SubmitLocalBatchAsync(new DocumentOperationBatch
        {
            DocumentId = "doc-1",
            Operations = [DocumentOperationEngineTests.InsertText("a", 5, "!")]
        });

        var result = await local.ReconnectAsync();

        result.IsValid.Should().BeTrue();
        DocumentOperationEngineTests.TextOf(local.Document, "a").Should().Be("Alpha!");
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

    private sealed class CountingCollaborationProvider : InMemoryDocumentCollaborationProvider
    {
        public int GetCursorsCalls { get; private set; }

        public override Task<IReadOnlyList<DocumentCollaborationCursor>> GetCursorsAsync(
            string documentId,
            CancellationToken cancellationToken = default)
        {
            GetCursorsCalls++;
            return base.GetCursorsAsync(documentId, cancellationToken);
        }
    }

    private static DocumentEditorDocument CreateImageDocument(string documentId, string blockId, string altText)
    {
        var document = DocumentEditorDocument.Empty(documentId);
        document.Blocks =
        [
            new DocumentBlock
            {
                Id = blockId,
                Type = DocumentBlockType.Image,
                Content = new ImageBlockContent
                {
                    Source = DocumentImageSource.Url,
                    Url = "https://example.test/image.png",
                    AltText = altText,
                    Size = new DocumentImageSize { Width = 320, Height = 180 }
                }
            }
        ];
        return document;
    }

    private static DocumentRevision Revision(
        string id,
        DocumentRevisionType type,
        DocumentRevisionAction action,
        string blockId,
        string payload = "Draft ")
        => new()
        {
            Id = id,
            Type = type,
            Action = action,
            Range = new DocumentRevisionRange
            {
                BlockId = blockId,
                StartInlineIndex = 0,
                EndInlineIndex = 0,
                StartOffset = 0,
                EndOffset = payload.Length
            },
            PayloadJson = payload
        };

    private static T Clone<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, DocumentEditorJson.Options);
        return JsonSerializer.Deserialize<T>(json, DocumentEditorJson.Options)!;
    }
}
