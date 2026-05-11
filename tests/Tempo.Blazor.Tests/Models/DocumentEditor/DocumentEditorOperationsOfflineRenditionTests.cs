using FluentAssertions;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Tests.Models.DocumentEditor;

public class DocumentEditorOperationsOfflineRenditionTests
{
    [Fact]
    public void Versions_StoreKindDescriptionAuthorAndStableSnapshotHash()
    {
        var snapshot = new DocumentVersionSnapshot
        {
            DocumentId = "doc-1",
            Json = "{\"schemaVersion\":1,\"documentId\":\"doc-1\"}"
        };

        var hash = DocumentVersionHashHelper.ComputeSnapshotHash(snapshot);
        snapshot.Hash = hash;

        var version = new DocumentVersion
        {
            DocumentId = "doc-1",
            Kind = DocumentVersionKind.Major,
            Description = "Approved contract",
            Author = new DocumentEditorAuthor { Id = "author-1", DisplayName = "Author" },
            Snapshot = snapshot
        };

        version.Kind.Should().Be(DocumentVersionKind.Major);
        version.Description.Should().Be("Approved contract");
        version.Author.Id.Should().Be("author-1");
        hash.Should().HaveLength(64);
        DocumentVersionHashHelper.ComputeSnapshotHash(snapshot).Should().Be(hash);
    }

    [Fact]
    public void AuditEvents_CoverOpenChangeVersionCommentAndExportActions()
    {
        var actions = new[]
        {
            DocumentEditorAuditAction.Open,
            DocumentEditorAuditAction.Change,
            DocumentEditorAuditAction.CreateVersion,
            DocumentEditorAuditAction.Comment,
            DocumentEditorAuditAction.Export
        };

        var events = actions.Select(action => new DocumentEditorAuditEvent
        {
            DocumentId = "doc-1",
            Action = action,
            Target = new DocumentEditorAuditTarget { Type = "document", Id = "doc-1" },
            Result = DocumentEditorAuditResult.Success
        });

        events.Should().OnlyContain(item => item.DocumentId == "doc-1");
        events.Select(item => item.Action).Should().Equal(actions);
    }

    [Fact]
    public void Operations_CarryStableIdsAuthorLogicalTimestampAndCoreOperationPayloads()
    {
        var operations = new List<DocumentOperation>
        {
            new()
            {
                Type = DocumentOperationType.InsertText,
                Text = "A",
                Target = new DocumentOperationTarget { BlockId = "block-1", Offset = 0 },
                Metadata = new DocumentOperationMetadata { AuthorId = "author-1", LogicalTimestamp = 1 }
            },
            new()
            {
                Type = DocumentOperationType.DeleteText,
                Text = "B",
                Target = new DocumentOperationTarget { BlockId = "block-1", Offset = 1 },
                Metadata = new DocumentOperationMetadata { AuthorId = "author-1", LogicalTimestamp = 2 }
            },
            new()
            {
                Type = DocumentOperationType.AddMark,
                Mark = new InlineMark { Type = InlineMarkType.Bold },
                Metadata = new DocumentOperationMetadata { AuthorId = "author-1", LogicalTimestamp = 3 }
            },
            new()
            {
                Type = DocumentOperationType.RemoveMark,
                Mark = new InlineMark { Type = InlineMarkType.Bold },
                Metadata = new DocumentOperationMetadata { AuthorId = "author-1", LogicalTimestamp = 4 }
            },
            new()
            {
                Type = DocumentOperationType.InsertBlock,
                Block = new DocumentBlock { Type = DocumentBlockType.Paragraph },
                Metadata = new DocumentOperationMetadata { AuthorId = "author-1", LogicalTimestamp = 5 }
            },
            new()
            {
                Type = DocumentOperationType.DeleteBlock,
                Target = new DocumentOperationTarget { BlockId = "block-2" },
                Metadata = new DocumentOperationMetadata { AuthorId = "author-1", LogicalTimestamp = 6 }
            },
            new()
            {
                Type = DocumentOperationType.MoveBlock,
                Target = new DocumentOperationTarget { BlockId = "block-3", Order = 20 },
                Metadata = new DocumentOperationMetadata { AuthorId = "author-1", LogicalTimestamp = 7 }
            },
            new()
            {
                Type = DocumentOperationType.SetBlockAttribute,
                AttributeName = "alignment",
                AttributeValueJson = "\"center\"",
                Metadata = new DocumentOperationMetadata { AuthorId = "author-1", LogicalTimestamp = 8 }
            }
        };

        var batch = new DocumentOperationBatch
        {
            DocumentId = "doc-1",
            BaseVersionId = "v1",
            Operations = operations
        };

        batch.Operations.Should().HaveCount(8);
        batch.Operations.Should().OnlyContain(operation => !string.IsNullOrWhiteSpace(operation.Id));
        batch.Operations.Should().OnlyContain(operation => operation.Metadata.AuthorId == "author-1");
        batch.Operations.Select(operation => operation.Metadata.LogicalTimestamp).Should().Equal(1, 2, 3, 4, 5, 6, 7, 8);
    }

    [Fact]
    public void OfflineDrafts_StoreBaseVersionSnapshotOperationBatchesStatusAndConflicts()
    {
        var batch = new DocumentOperationBatch { DocumentId = "doc-1", BaseVersionId = "v1" };
        var draft = new DocumentOfflineDraft
        {
            DocumentId = "doc-1",
            BaseVersionId = "v1",
            JsonSnapshot = "{\"documentId\":\"doc-1\"}",
            OperationBatches = [batch],
            SyncStatus = DocumentSyncStatus.Conflict,
            State = DocumentOfflineDraftState.Conflict
        };

        var conflict = new DocumentSyncConflict
        {
            DocumentId = "doc-1",
            LocalBaseVersionId = "v1",
            ServerVersionId = "v2",
            Reason = "Base version is stale",
            Resolution = DocumentSyncConflictResolution.Merge
        };

        draft.OperationBatches.Should().ContainSingle();
        draft.JsonSnapshot.Should().Contain("doc-1");
        draft.SyncStatus.Should().Be(DocumentSyncStatus.Conflict);
        conflict.LocalBaseVersionId.Should().Be("v1");
        conflict.ServerVersionId.Should().Be("v2");
        conflict.Resolution.Should().Be(DocumentSyncConflictResolution.Merge);
    }

    [Fact]
    public void Renditions_LinkVersionHashPagesPreviewAssetsNormalizedAnchorsAndPdfAttachment()
    {
        var rendition = new DocumentRendition
        {
            Id = "rendition-1",
            DocumentId = "doc-1",
            DocumentVersionId = "v1",
            Status = DocumentRenditionStatus.Finalized,
            Hash = new DocumentRenditionHash { Value = "abc123" },
            PdfAttachmentId = "pdf-1",
            Pages =
            [
                new DocumentRenditionPage
                {
                    PageNumber = 1,
                    Width = 595,
                    Height = 842,
                    PreviewImageUrl = "https://example.test/page-1.png",
                    PreviewImageAssetId = "page-1"
                }
            ],
            Anchors =
            [
                new DocumentRenditionAnchor
                {
                    Type = DocumentRenditionAnchorType.Token,
                    Key = "client.name",
                    PageNumber = 1,
                    X = 0.1,
                    Y = 0.2,
                    Width = 0.3,
                    Height = 0.04
                },
                new DocumentRenditionAnchor
                {
                    Type = DocumentRenditionAnchorType.Placeholder,
                    Key = "signature.client",
                    PageNumber = 1,
                    X = 0.4,
                    Y = 0.8,
                    Width = 0.2,
                    Height = 0.08
                }
            ]
        };

        rendition.DocumentId.Should().Be("doc-1");
        rendition.DocumentVersionId.Should().Be("v1");
        rendition.Hash.Value.Should().Be("abc123");
        rendition.Pages[0].PreviewImageAssetId.Should().Be("page-1");
        rendition.PdfAttachmentId.Should().Be("pdf-1");
        rendition.Anchors.Should().Contain(anchor => anchor.Type == DocumentRenditionAnchorType.Token && anchor.X >= 0 && anchor.X <= 1);
        rendition.Anchors.Should().Contain(anchor => anchor.Type == DocumentRenditionAnchorType.Placeholder && anchor.Key == "signature.client");
        rendition.IsImmutable.Should().BeTrue();
    }
}
