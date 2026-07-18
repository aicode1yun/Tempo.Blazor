using Microsoft.JSInterop;
using Tempo.Blazor.DocumentEditor.Interfaces;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Services;

/// <summary>Browser IndexedDB-backed offline store for document editor drafts.</summary>
public class IndexedDbDocumentOfflineStore : IDocumentOfflineStore
{
    // The window.tmDocumentEditor.offlineStore global is installed by this ES module; the store
    // must import it before the first call because no host script tag provides the global.
    private const string BrowserGlobalsModulePath = "./_content/Tempo.Blazor.DocumentEditor/js/document-editor/interop/browser-globals.mjs";

    private readonly IJSRuntime _jsRuntime;
    private Task<IJSObjectReference>? _browserGlobalsModule;

    /// <summary>Creates an IndexedDB document offline store.</summary>
    public IndexedDbDocumentOfflineStore(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    private async Task EnsureBrowserGlobalsAsync(CancellationToken cancellationToken)
    {
        _browserGlobalsModule ??= _jsRuntime.InvokeAsync<IJSObjectReference>("import", cancellationToken, BrowserGlobalsModulePath).AsTask();
        try
        {
            await _browserGlobalsModule;
        }
        catch
        {
            // Do not cache a faulted import (prerender / disconnected runtime) — retry next call.
            _browserGlobalsModule = null;
            throw;
        }
    }

    /// <inheritdoc />
    public async Task SaveDraftAsync(DocumentOfflineDraft draft, CancellationToken cancellationToken = default)
    {
        await InvokeVoidSafeAsync("tmDocumentEditor.offlineStore.saveDraft", cancellationToken, draft);
    }

    /// <inheritdoc />
    public async Task<DocumentOfflineDraft?> LoadDraftAsync(string draftId, CancellationToken cancellationToken = default)
    {
        return await InvokeSafeAsync<DocumentOfflineDraft?>("tmDocumentEditor.offlineStore.loadDraft", cancellationToken, draftId);
    }

    /// <inheritdoc />
    public async Task DeleteDraftAsync(string draftId, CancellationToken cancellationToken = default)
    {
        await InvokeVoidSafeAsync("tmDocumentEditor.offlineStore.deleteDraft", cancellationToken, draftId);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DocumentOfflineDraft>> ListPendingDraftsAsync(
        string? documentId = null,
        CancellationToken cancellationToken = default)
    {
        var drafts = await InvokeSafeAsync<DocumentOfflineDraft[]?>(
            "tmDocumentEditor.offlineStore.listPendingDrafts",
            cancellationToken,
            documentId);

        return drafts ?? [];
    }

    private async Task<T?> InvokeSafeAsync<T>(string identifier, CancellationToken cancellationToken, params object?[] args)
    {
        try
        {
            await EnsureBrowserGlobalsAsync(cancellationToken);
            return await _jsRuntime.InvokeAsync<T>(identifier, cancellationToken, args);
        }
        catch (JSException)
        {
            return default;
        }
        catch (InvalidOperationException)
        {
            return default;
        }
        catch (Exception)
        {
            return default;
        }
    }

    private async Task InvokeVoidSafeAsync(string identifier, CancellationToken cancellationToken, params object?[] args)
    {
        try
        {
            await EnsureBrowserGlobalsAsync(cancellationToken);
            await _jsRuntime.InvokeVoidAsync(identifier, cancellationToken, args);
        }
        catch (JSException)
        {
        }
        catch (InvalidOperationException)
        {
        }
        catch (Exception)
        {
        }
    }
}
