using System.Net.Http.Json;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;

namespace Tempo.Blazor.Demo.Services;

/// <summary>HTTP-backed collaboration provider used by the document editor demo.</summary>
public sealed class DemoDocumentCollaborationProvider : InMemoryDocumentCollaborationProvider
{
    private readonly HttpClient? _http;

    /// <summary>Creates the provider and optionally binds it to the demo API client.</summary>
    public DemoDocumentCollaborationProvider(IHttpClientFactory? factory = null)
    {
        _http = factory?.CreateClient("DemoApi");
    }

    /// <inheritdoc />
    public override async Task<DocumentCollaborationSession> JoinAsync(
        DocumentCollaborationJoinRequest request,
        CancellationToken cancellationToken = default)
    {
        if (_http is not null)
        {
            try
            {
                var response = await _http.PostAsJsonAsync("api/document-editor/collaboration/join", request, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var session = await response.Content.ReadFromJsonAsync<DocumentCollaborationSession>(cancellationToken);
                    if (session is not null)
                    {
                        return session;
                    }
                }
            }
            catch
            {
                // Demo remains usable when the optional API is not running.
            }
        }

        return await base.JoinAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    public override async Task LeaveAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (_http is not null)
        {
            try
            {
                var response = await _http.PostAsync(
                    $"api/document-editor/collaboration/{Uri.EscapeDataString(sessionId)}/leave",
                    content: null,
                    cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch
            {
                // Demo remains usable when the optional API is not running.
            }
        }

        await base.LeaveAsync(sessionId, cancellationToken);
    }

    /// <inheritdoc />
    public override async Task<DocumentCollaborationOperationBatch> BroadcastOperationBatchAsync(
        string sessionId,
        DocumentOperationBatch batch,
        CancellationToken cancellationToken = default)
    {
        if (_http is not null)
        {
            try
            {
                var response = await _http.PostAsJsonAsync(
                    $"api/document-editor/collaboration/{Uri.EscapeDataString(sessionId)}/batches",
                    batch,
                    cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var broadcast = await response.Content.ReadFromJsonAsync<DocumentCollaborationOperationBatch>(cancellationToken);
                    if (broadcast is not null)
                    {
                        return broadcast;
                    }
                }
            }
            catch
            {
                // Demo remains usable when the optional API is not running.
            }
        }

        return await base.BroadcastOperationBatchAsync(sessionId, batch, cancellationToken);
    }

    /// <inheritdoc />
    public override async Task<IReadOnlyList<DocumentCollaborationOperationBatch>> GetOperationBatchesAsync(
        string documentId,
        long afterSequence,
        CancellationToken cancellationToken = default)
    {
        if (_http is not null)
        {
            try
            {
                var batches = await _http.GetFromJsonAsync<List<DocumentCollaborationOperationBatch>>(
                    $"api/document-editor/collaboration/documents/{Uri.EscapeDataString(documentId)}/batches?afterSequence={afterSequence}",
                    cancellationToken);
                if (batches is not null)
                {
                    return batches;
                }
            }
            catch
            {
                // Demo remains usable when the optional API is not running.
            }
        }

        return await base.GetOperationBatchesAsync(documentId, afterSequence, cancellationToken);
    }

    /// <inheritdoc />
    public override async Task BroadcastCursorAsync(
        DocumentCollaborationCursor cursor,
        CancellationToken cancellationToken = default)
    {
        if (_http is not null)
        {
            try
            {
                var response = await _http.PostAsJsonAsync(
                    "api/document-editor/collaboration/cursors",
                    cursor,
                    cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch
            {
                // Demo remains usable when the optional API is not running.
            }
        }

        await base.BroadcastCursorAsync(cursor, cancellationToken);
    }

    /// <inheritdoc />
    public override async Task<IReadOnlyList<DocumentCollaborationCursor>> GetCursorsAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        if (_http is not null)
        {
            try
            {
                var cursors = await _http.GetFromJsonAsync<List<DocumentCollaborationCursor>>(
                    $"api/document-editor/collaboration/documents/{Uri.EscapeDataString(documentId)}/cursors",
                    cancellationToken);
                if (cursors is not null)
                {
                    return cursors;
                }
            }
            catch
            {
                // Demo remains usable when the optional API is not running.
            }
        }

        return await base.GetCursorsAsync(documentId, cancellationToken);
    }
}
