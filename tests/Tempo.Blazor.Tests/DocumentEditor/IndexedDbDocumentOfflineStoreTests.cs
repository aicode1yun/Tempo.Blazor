using FluentAssertions;
using Microsoft.JSInterop;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.Services;

namespace Tempo.Blazor.Tests.DocumentEditor;

public class IndexedDbDocumentOfflineStoreTests
{
    [Fact]
    public async Task IndexedDbStore_GracefullyFallsBackWithoutJsImplementation()
    {
        var store = new IndexedDbDocumentOfflineStore(new ThrowingJsRuntime());

        await store.SaveDraftAsync(new DocumentOfflineDraft { Id = "draft-1", DocumentId = "doc-1" });
        var loaded = await store.LoadDraftAsync("draft-1");
        var pending = await store.ListPendingDraftsAsync("doc-1");
        await store.DeleteDraftAsync("draft-1");

        loaded.Should().BeNull();
        pending.Should().BeEmpty();
    }

    [Fact]
    public async Task IndexedDbStore_DelegatesSaveLoadListAndDeleteToJs()
    {
        var draft = new DocumentOfflineDraft { Id = "draft-1", DocumentId = "doc-1" };
        var js = new RecordingJsRuntime
        {
            Results =
            {
                ["tmDocumentEditor.offlineStore.loadDraft"] = draft,
                ["tmDocumentEditor.offlineStore.listPendingDrafts"] = new[] { draft }
            }
        };
        var store = new IndexedDbDocumentOfflineStore(js);

        await store.SaveDraftAsync(draft);
        var loaded = await store.LoadDraftAsync("draft-1");
        var pending = await store.ListPendingDraftsAsync("doc-1");
        await store.DeleteDraftAsync("draft-1");

        loaded.Should().BeEquivalentTo(draft);
        pending.Should().ContainSingle(item => item.Id == "draft-1");
        js.Invocations.Should().Equal(
            "tmDocumentEditor.offlineStore.saveDraft",
            "tmDocumentEditor.offlineStore.loadDraft",
            "tmDocumentEditor.offlineStore.listPendingDrafts",
            "tmDocumentEditor.offlineStore.deleteDraft");
    }

    private sealed class ThrowingJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            throw new InvalidOperationException("JS runtime is not available.");
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            throw new InvalidOperationException("JS runtime is not available.");
        }
    }

    private sealed class RecordingJsRuntime : IJSRuntime
    {
        public List<string> Invocations { get; } = [];

        public Dictionary<string, object?> Results { get; } = [];

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            return InvokeAsync<TValue>(identifier, CancellationToken.None, args);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            Invocations.Add(identifier);
            Results.TryGetValue(identifier, out var result);
            return ValueTask.FromResult((TValue?)result ?? default!);
        }
    }
}
