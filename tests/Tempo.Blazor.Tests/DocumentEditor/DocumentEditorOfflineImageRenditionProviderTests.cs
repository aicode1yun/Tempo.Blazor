using System.Text;
using FluentAssertions;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;

namespace Tempo.Blazor.Tests.DocumentEditor;

public class DocumentEditorOfflineImageRenditionProviderTests
{
    [Fact]
    public async Task OfflineStore_SavesLoadsListsAndDeletesDrafts()
    {
        var store = new InMemoryDocumentOfflineStore();
        var draft = new DocumentOfflineDraft
        {
            Id = "draft-1",
            DocumentId = "doc-1",
            BaseVersionId = "v1",
            JsonSnapshot = "{\"DocumentId\":\"doc-1\"}",
            OperationBatches =
            [
                new DocumentOperationBatch { DocumentId = "doc-1", BaseVersionId = "v1" }
            ]
        };

        await store.SaveDraftAsync(draft);
        var loaded = await store.LoadDraftAsync("draft-1");
        var pending = await store.ListPendingDraftsAsync("doc-1");
        await store.DeleteDraftAsync("draft-1");
        var deleted = await store.LoadDraftAsync("draft-1");

        loaded!.DocumentId.Should().Be("doc-1");
        loaded.BaseVersionId.Should().Be("v1");
        loaded.OperationBatches.Should().ContainSingle();
        pending.Should().ContainSingle(item => item.Id == "draft-1");
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task SyncProvider_SubmitsOfflineDraftAndDeletesItAfterSuccessfulSync()
    {
        var provider = new InMemoryDocumentEditorProvider();
        var document = provider.SeedEmptyDocument("doc-1");
        var loaded = await provider.LoadAsync("doc-1");
        var store = new InMemoryDocumentOfflineStore();
        var syncProvider = new InMemoryDocumentSyncProvider(provider, store);
        document.Metadata.Title = "Offline title";
        var draft = new DocumentOfflineDraft
        {
            Id = "draft-1",
            DocumentId = "doc-1",
            BaseVersionId = loaded.ConcurrencyToken,
            JsonSnapshot = DocumentEditorJson.Serialize(document),
            OperationBatches =
            [
                new DocumentOperationBatch
                {
                    DocumentId = "doc-1",
                    Operations =
                    [
                        new DocumentOperation
                        {
                            Type = DocumentOperationType.InsertText,
                            Text = "A",
                            Metadata = new DocumentOperationMetadata { AuthorId = "author-1", LogicalTimestamp = 1 }
                        }
                    ]
                }
            ]
        };
        await store.SaveDraftAsync(draft);

        var result = await syncProvider.SyncAsync(new DocumentSyncRequest { Draft = draft });
        var deleted = await store.LoadDraftAsync("draft-1");
        var submitted = await syncProvider.SubmitOperationBatchAsync(draft.OperationBatches[0]);

        result.Success.Should().BeTrue();
        deleted.Should().BeNull();
        submitted.Success.Should().BeTrue();
    }

    [Fact]
    public async Task SyncProvider_ReturnsConflictForStaleDraft()
    {
        var provider = new InMemoryDocumentEditorProvider();
        var document = provider.SeedEmptyDocument("doc-1");
        var syncProvider = new InMemoryDocumentSyncProvider(provider);
        document.Metadata.Title = "Offline title";

        var result = await syncProvider.SyncAsync(new DocumentSyncRequest
        {
            Draft = new DocumentOfflineDraft
            {
                DocumentId = "doc-1",
                BaseVersionId = "stale",
                JsonSnapshot = DocumentEditorJson.Serialize(document)
            }
        });

        result.Success.Should().BeFalse();
        result.Conflict!.Reason.Should().Contain("stale");
    }

    [Fact]
    public async Task SyncProvider_MergesOperationLogAgainstNewerServerVersion()
    {
        var provider = new InMemoryDocumentEditorProvider();
        var document = provider.SeedContractDocument("doc-1");
        var initial = await provider.LoadAsync("doc-1");
        var blockId = initial.Document!.Blocks[1].Id;
        document.Metadata.Title = "Server title";
        await provider.SaveAsync(new DocumentEditorSaveRequest
        {
            DocumentId = "doc-1",
            Document = document,
            BaseConcurrencyToken = initial.ConcurrencyToken
        });
        var store = new InMemoryDocumentOfflineStore();
        var syncProvider = new InMemoryDocumentSyncProvider(provider, store);
        var draft = new DocumentOfflineDraft
        {
            Id = "draft-merge",
            DocumentId = "doc-1",
            BaseVersionId = initial.ConcurrencyToken,
            JsonSnapshot = initial.JsonSnapshot ?? DocumentEditorJson.Serialize(initial.Document),
            OperationBatches =
            [
                new DocumentOperationBatch
                {
                    DocumentId = "doc-1",
                    Operations =
                    [
                        new DocumentOperation
                        {
                            Type = DocumentOperationType.InsertText,
                            Target = new DocumentOperationTarget { BlockId = blockId, InlineIndex = 0, Offset = 28 },
                            Text = " offline",
                            Metadata = new DocumentOperationMetadata { AuthorId = "author-1", ClientId = "client-1", LogicalTimestamp = 1 }
                        }
                    ]
                }
            ]
        };
        await store.SaveDraftAsync(draft);

        var result = await syncProvider.SyncAsync(new DocumentSyncRequest { Draft = draft });
        var loaded = await provider.LoadAsync("doc-1");
        var paragraph = (ParagraphBlockContent)loaded.Document!.Blocks[1].Content;

        result.Success.Should().BeTrue();
        paragraph.Inlines.OfType<TextRun>().First().Text.Should().Contain("offline");
        loaded.Document.Metadata.Title.Should().Be("Server title");
        (await store.ListPendingDraftsAsync("doc-1")).Should().BeEmpty();
    }

    [Fact]
    public async Task ImageProvider_UploadsResolvesDeletesCommitsValidatesAndRefreshesUrls()
    {
        var imageProvider = new InMemoryDocumentImageProvider(new DocumentImageProviderOptions
        {
            Validation = new DocumentImageValidationOptions
            {
                AllowedContentTypes = ["image/png"],
                MaxFileSizeBytes = 4
            }
        });

        await using var stream = new MemoryStream([1, 2, 3]);
        var upload = await imageProvider.UploadAsync(new DocumentImageUploadRequest
        {
            DocumentId = "doc-1",
            FileName = "image.png",
            ContentType = "image/png",
            SizeBytes = 3
        }, stream);

        var resolve = await imageProvider.ResolveAsync(new DocumentImageResolveRequest
        {
            DocumentId = "doc-1",
            AssetId = upload.AssetId!
        });

        var refreshed = await imageProvider.RefreshUrlAsync(new DocumentImageResolveRequest
        {
            DocumentId = "doc-1",
            AssetId = upload.AssetId!
        });

        var commit = await imageProvider.CommitAssetsAsync("doc-1", [upload.AssetId!]);
        await imageProvider.DeleteDraftAssetAsync("doc-1", upload.AssetId!);
        var stillResolvedAfterCommit = await imageProvider.ResolveUrlAsync("doc-1", upload.AssetId!);

        await using var rejectedStream = new MemoryStream(Encoding.UTF8.GetBytes("abcde"));
        var rejected = await imageProvider.UploadAsync(new DocumentImageUploadRequest
        {
            DocumentId = "doc-1",
            FileName = "image.jpg",
            ContentType = "image/jpeg",
            SizeBytes = 5
        }, rejectedStream);

        upload.Success.Should().BeTrue();
        resolve.Success.Should().BeTrue();
        resolve.Url.Should().StartWith("memory://document-images/doc-1/");
        refreshed.Url.Should().NotBe(resolve.Url);
        commit.AssetIds.Should().Contain(upload.AssetId);
        stillResolvedAfterCommit.Should().NotBeNullOrWhiteSpace();
        rejected.Success.Should().BeFalse();
    }

    [Fact]
    public async Task RenditionProvider_CreatesLoadsPagesAnchorMapAndAuditsFromVersion()
    {
        var provider = new InMemoryDocumentEditorProvider();
        var document = provider.SeedContractDocument("doc-1");
        document.Anchors.Add(new DocumentAnchor
        {
            Type = DocumentAnchorType.Token,
            Key = "client.name"
        });
        var loaded = await provider.LoadAsync("doc-1");
        await provider.SaveAsync(new DocumentEditorSaveRequest
        {
            DocumentId = "doc-1",
            Document = document,
            BaseConcurrencyToken = loaded.ConcurrencyToken
        });
        var version = await provider.CreateVersionAsync(new DocumentVersionCreateRequest
        {
            DocumentId = "doc-1",
            Kind = DocumentVersionKind.Major,
            Author = new DocumentEditorAuthor { Id = "author-1", DisplayName = "Author" }
        });

        var renditionProvider = new InMemoryDocumentRenditionProvider(provider, provider);
        var dirtyResult = await renditionProvider.CreateRenditionAsync(new DocumentRenditionRequest
        {
            DocumentId = "doc-1"
        });
        var result = await renditionProvider.CreateRenditionAsync(new DocumentRenditionRequest
        {
            DocumentId = "doc-1",
            DocumentVersionId = version.Id,
            Actor = new DocumentEditorAuthor { Id = "author-1", DisplayName = "Author" }
        });
        var loadedRendition = await renditionProvider.GetRenditionAsync(result.Rendition!.Id);
        var pages = await renditionProvider.GetPagesAsync(result.Rendition.Id);
        var anchors = await renditionProvider.GetAnchorMapAsync(result.Rendition.Id);

        dirtyResult.Success.Should().BeFalse();
        result.Success.Should().BeTrue();
        loadedRendition!.IsImmutable.Should().BeTrue();
        pages.Should().ContainSingle(page => page.PageNumber == 1);
        anchors.Should().ContainSingle(anchor => anchor.Type == DocumentRenditionAnchorType.Token && anchor.Key == "client.name");
        provider.ActivityEntries.Should().ContainSingle(item => item.Action == "create-rendition");
    }
}
