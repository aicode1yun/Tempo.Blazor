using Bunit;
using FluentAssertions;
using Tempo.Blazor.Components.DocumentEditor;
using Tempo.Blazor.DocumentEditor.Interfaces;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.DocumentEditor;

public class TmDocumentEditorOfflineTests : LocalizationTestBase
{
    [Fact]
    public async Task OfflineMode_DisabledKeepsEditorOnlineOnly()
    {
        var provider = new FailingSaveProvider();
        var seeded = provider.SeedContractDocument("doc-1");
        var store = new InMemoryDocumentOfflineStore();

        var cut = RenderDocumentEditorLegacy(parameters => parameters
            .Add(p => p.DocumentId, "doc-1")
            .Add(p => p.Provider, provider)
            .Add(p => p.OfflineStore, store));

        cut.WaitForAssertion(() => cut.Find("[data-testid='document-wysiwyg-host']").Should().NotBeNull());
        await SimulateTextInsertAsync(cut, seeded, "Offline text ");
        cut.Find("[data-testid='document-save']").Click();

        cut.WaitForAssertion(() => cut.Find("[data-testid='document-save-message']").TextContent.Should().Contain("Save failed"));
        cut.FindAll("[data-testid='document-offline-banner']").Should().BeEmpty();
        (await store.ListPendingDraftsAsync("doc-1")).Should().BeEmpty();
    }

    [Fact]
    public async Task OfflineMode_SaveFailureStoresDraftAndShowsStatus()
    {
        var provider = new FailingSaveProvider();
        var seeded = provider.SeedContractDocument("doc-1");
        var store = new InMemoryDocumentOfflineStore();

        var cut = RenderDocumentEditorLegacy(parameters => parameters
            .Add(p => p.DocumentId, "doc-1")
            .Add(p => p.Provider, provider)
            .Add(p => p.OfflineMode, DocumentEditorOfflineMode.Enabled)
            .Add(p => p.OfflineStore, store));

        cut.WaitForAssertion(() => cut.Find("[data-testid='document-wysiwyg-host']").Should().NotBeNull());
        await SimulateTextInsertAsync(cut, seeded, "Offline text ");
        cut.Find("[data-testid='document-save']").Click();

        cut.WaitForAssertion(() => cut.Find("[data-testid='document-offline-banner']").TextContent.Should().Contain("Saved as an offline draft"));
        var draft = (await store.ListPendingDraftsAsync("doc-1")).Should().ContainSingle().Subject;
        DocumentEditorJson.Deserialize(draft.JsonSnapshot).Blocks.Should().NotBeEmpty();
    }

    [Fact]
    public async Task OfflineMode_LoadsNewerLocalDraftWhenPreferred()
    {
        var provider = new InMemoryDocumentEditorProvider();
        var server = provider.SeedContractDocument("doc-1");
        server.Metadata.ModifiedAt = DateTimeOffset.UtcNow.AddMinutes(-30);
        var local = Clone(server);
        local.Metadata.Title = "Local draft";
        var store = new InMemoryDocumentOfflineStore();
        await store.SaveDraftAsync(new DocumentOfflineDraft
        {
            Id = "draft-1",
            DocumentId = "doc-1",
            JsonSnapshot = DocumentEditorJson.Serialize(local),
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var cut = RenderDocumentEditorLegacy(parameters => parameters
            .Add(p => p.DocumentId, "doc-1")
            .Add(p => p.Provider, provider)
            .Add(p => p.OfflineMode, DocumentEditorOfflineMode.Enabled)
            .Add(p => p.OfflineStore, store)
            .Add(p => p.PreferLocalDraft, true));

        cut.WaitForAssertion(() => cut.Find(".tm-document-editor__document-title").TextContent.Should().Contain("Local draft"));
        cut.Find("[data-testid='document-offline-banner']").TextContent.Should().Contain("Newer offline draft loaded");
    }

    [Fact]
    public async Task OfflineMode_DiscardDraftReloadsServerSnapshot()
    {
        var provider = new InMemoryDocumentEditorProvider();
        var server = provider.SeedContractDocument("doc-1");
        server.Metadata.ModifiedAt = DateTimeOffset.UtcNow.AddMinutes(-30);
        var local = Clone(server);
        local.Metadata.Title = "Local draft";
        var store = new InMemoryDocumentOfflineStore();
        await store.SaveDraftAsync(new DocumentOfflineDraft
        {
            Id = "draft-1",
            DocumentId = "doc-1",
            JsonSnapshot = DocumentEditorJson.Serialize(local),
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var cut = RenderDocumentEditorLegacy(parameters => parameters
            .Add(p => p.DocumentId, "doc-1")
            .Add(p => p.Provider, provider)
            .Add(p => p.OfflineMode, DocumentEditorOfflineMode.Enabled)
            .Add(p => p.OfflineStore, store)
            .Add(p => p.PreferLocalDraft, true));

        cut.WaitForAssertion(() => cut.Find(".tm-document-editor__document-title").TextContent.Should().Contain("Local draft"));
        cut.Find("[data-testid='document-offline-discard']").Click();

        cut.WaitForAssertion(() => cut.Find(".tm-document-editor__document-title").TextContent.Should().Contain("Service agreement"));
        (await store.ListPendingDraftsAsync("doc-1")).Should().BeEmpty();
    }

    [Fact]
    public async Task OfflineMode_SyncButtonSubmitsDraft()
    {
        var provider = new InMemoryDocumentEditorProvider();
        var server = provider.SeedContractDocument("doc-1");
        var local = Clone(server);
        local.Metadata.Title = "Local draft";
        var store = new InMemoryDocumentOfflineStore();
        await store.SaveDraftAsync(new DocumentOfflineDraft
        {
            Id = "draft-1",
            DocumentId = "doc-1",
            JsonSnapshot = DocumentEditorJson.Serialize(local),
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var cut = RenderDocumentEditorLegacy(parameters => parameters
            .Add(p => p.DocumentId, "doc-1")
            .Add(p => p.Provider, provider)
            .Add(p => p.OfflineMode, DocumentEditorOfflineMode.Enabled)
            .Add(p => p.OfflineStore, store)
            .Add(p => p.PreferLocalDraft, true));

        cut.WaitForAssertion(() => cut.Find("[data-testid='document-offline-sync']").Should().NotBeNull());
        cut.Find("[data-testid='document-offline-sync']").Click();

        cut.WaitForAssertion(() => cut.Find("[data-testid='document-save-message']").TextContent.Should().Contain("Offline draft synchronized"));
        (await store.ListPendingDraftsAsync("doc-1")).Should().BeEmpty();
    }

    [Fact]
    public async Task OfflineMode_ConflictShowsReviewActions()
    {
        var provider = new InMemoryDocumentEditorProvider();
        var server = provider.SeedContractDocument("doc-1");
        var local = Clone(server);
        local.Metadata.Title = "Local draft";
        var store = new InMemoryDocumentOfflineStore();
        await store.SaveDraftAsync(new DocumentOfflineDraft
        {
            Id = "draft-1",
            DocumentId = "doc-1",
            JsonSnapshot = DocumentEditorJson.Serialize(local),
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var cut = RenderDocumentEditorLegacy(parameters => parameters
            .Add(p => p.DocumentId, "doc-1")
            .Add(p => p.Provider, provider)
            .Add(p => p.SyncProvider, new ConflictSyncProvider())
            .Add(p => p.OfflineMode, DocumentEditorOfflineMode.Enabled)
            .Add(p => p.OfflineStore, store)
            .Add(p => p.PreferLocalDraft, true));

        cut.WaitForAssertion(() => cut.Find("[data-testid='document-offline-sync']").Should().NotBeNull());
        cut.Find("[data-testid='document-offline-sync']").Click();

        cut.WaitForAssertion(() => cut.Find("[data-testid='document-offline-banner']").TextContent.Should().Contain("conflicts"));
        cut.Find("[data-testid='document-offline-accept-local']").Should().NotBeNull();
        cut.Find("[data-testid='document-offline-accept-server']").Should().NotBeNull();
        cut.Find("[data-testid='document-offline-create-copy']").Should().NotBeNull();
    }

    [Fact]
    public async Task OfflineMode_ClipboardImageWithoutProviderCreatesPendingDraftAsset()
    {
        var provider = new FailingSaveProvider();
        provider.SeedContractDocument("doc-1");
        var store = new InMemoryDocumentOfflineStore();

        var cut = RenderDocumentEditorLegacy(parameters => parameters
            .Add(p => p.DocumentId, "doc-1")
            .Add(p => p.Provider, provider)
            .Add(p => p.OfflineMode, DocumentEditorOfflineMode.Enabled)
            .Add(p => p.OfflineStore, store));

        cut.WaitForAssertion(() => cut.Find("[data-testid='document-wysiwyg-host']").Should().NotBeNull());
        await cut.InvokeAsync(() => cut.FindComponent<TmDocumentWysiwygHost>().Instance.HandleImageUploadRequested(new WysiwygImagePayload
        {
            Source = DocumentImageSource.Clipboard,
            FileName = "paste.png",
            ContentType = "image/png",
            SizeBytes = 1,
            Base64Data = "AA=="
        }));
        cut.Find("[data-testid='document-save']").Click();

        cut.WaitForAssertion(() => cut.Find("[data-testid='document-offline-banner']").Should().NotBeNull());
        var draft = (await store.ListPendingDraftsAsync("doc-1")).Single();
        draft.PendingAssets.Should().ContainSingle(asset => asset.Source == DocumentImageSource.Clipboard && asset.IsLocalDraft);
        draft.PendingClipboardImages.Should().ContainSingle(image => image.LocalAssetId == draft.PendingAssets[0].Id);
    }

    private static T Clone<T>(T value)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(value, DocumentEditorJson.Options);
        return System.Text.Json.JsonSerializer.Deserialize<T>(json, DocumentEditorJson.Options)!;
    }

    private static Task SimulateTextInsertAsync(
        IRenderedComponent<TmDocumentEditor> cut,
        DocumentEditorDocument document,
        string text)
    {
        var paragraph = document.Blocks.First(block => block.Content is ParagraphBlockContent);
        var inline = ((ParagraphBlockContent)paragraph.Content).Inlines.OfType<TextRun>().First();

        return cut.InvokeAsync(() => cut.FindComponent<TmDocumentWysiwygHost>().Instance.HandlePatchGenerated(new WysiwygPatch
        {
            Type = "InsertText",
            Data = text,
            Selection = new WysiwygSelectionSnapshot
            {
                AnchorBlockId = paragraph.Id,
                AnchorInlineId = inline.Id,
                AnchorOffset = 0
            }
        }));
    }

    private sealed class FailingSaveProvider : InMemoryDocumentEditorProvider
    {
        public override Task<DocumentEditorSaveResult> SaveAsync(DocumentEditorSaveRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new DocumentEditorSaveResult
            {
                Success = false,
                ErrorMessage = "Save failed"
            });
        }
    }

    private sealed class ConflictSyncProvider : IDocumentSyncProvider
    {
        public Task<DocumentSyncResult> SyncAsync(DocumentSyncRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new DocumentSyncResult
            {
                Success = false,
                Conflict = new DocumentSyncConflict
                {
                    DocumentId = request.Draft.DocumentId,
                    Reason = "Server version changed."
                }
            });
        }

        public Task<DocumentSyncResult> SubmitOperationBatchAsync(DocumentOperationBatch batch, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new DocumentSyncResult { Success = true });
        }
    }
}
