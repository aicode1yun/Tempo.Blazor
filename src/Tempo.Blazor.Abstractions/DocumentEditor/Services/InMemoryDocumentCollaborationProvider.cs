using Tempo.Blazor.DocumentEditor.Interfaces;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.DocumentEditor.Services;

/// <summary>In-memory collaboration provider for tests and demos.</summary>
public class InMemoryDocumentCollaborationProvider : IDocumentCollaborationProvider
{
    private readonly Dictionary<string, DocumentCollaborationSession> _sessions = [];
    private readonly Dictionary<string, List<DocumentCollaborationOperationBatch>> _batches = [];
    private readonly Dictionary<string, DocumentCollaborationCursor> _cursors = [];
    private long _sequence;

    // Concurrent editors broadcast in parallel (and a backplane may ingest on other threads), so
    // every read/write of the shared dictionaries must hold this gate.
    private readonly object _gate = new();

    /// <summary>Clears all in-memory collaboration sessions, operation batches, and cursors.</summary>
    public void Reset()
    {
        lock (_gate)
        {
            _sessions.Clear();
            _batches.Clear();
            _cursors.Clear();
            _sequence = 0;
        }
    }

    /// <inheritdoc />
    public virtual Task<DocumentCollaborationSession> JoinAsync(
        DocumentCollaborationJoinRequest request,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _sequence = Math.Max(_sequence, request.LastSeenSequence);
            var session = new DocumentCollaborationSession
            {
                DocumentId = request.DocumentId,
                ClientId = request.ClientId,
                Author = request.Author,
                LastSeenSequence = request.LastSeenSequence
            };
            _sessions[session.Id] = Clone(session);
            return Task.FromResult(Clone(session));
        }
    }

    /// <inheritdoc />
    public virtual Task LeaveAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_sessions.Remove(sessionId, out var session))
            {
                _cursors.Remove(GetCursorKey(session.DocumentId, sessionId));
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public virtual Task<DocumentCollaborationOperationBatch> BroadcastOperationBatchAsync(
        string sessionId,
        DocumentOperationBatch batch,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
            {
                throw new InvalidOperationException("Collaboration session was not found.");
            }

            var normalized = Clone(batch);
            var validation = DocumentOperationLog.Validate(normalized);
            if (!validation.IsValid)
            {
                throw new InvalidOperationException(string.Join(" ", validation.Errors));
            }

            var item = new DocumentCollaborationOperationBatch
            {
                Sequence = ++_sequence,
                SessionId = sessionId,
                Batch = normalized
            };

            if (!_batches.TryGetValue(session.DocumentId, out var documentBatches))
            {
                documentBatches = [];
                _batches[session.DocumentId] = documentBatches;
            }

            documentBatches.Add(Clone(item));
            session.LastSeenSequence = item.Sequence;
            _sessions[sessionId] = Clone(session);
            return Task.FromResult(Clone(item));
        }
    }

    /// <inheritdoc />
    public virtual Task<IReadOnlyList<DocumentCollaborationOperationBatch>> GetOperationBatchesAsync(
        string documentId,
        long afterSequence,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var result = _batches.TryGetValue(documentId, out var batches)
                ? batches.Where(batch => batch.Sequence > afterSequence).Select(Clone).ToList()
                : [];

            return Task.FromResult<IReadOnlyList<DocumentCollaborationOperationBatch>>(result);
        }
    }

    /// <inheritdoc />
    public virtual Task BroadcastCursorAsync(DocumentCollaborationCursor cursor, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _cursors[GetCursorKey(cursor.DocumentId, cursor.SessionId)] = Clone(cursor);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public virtual Task<IReadOnlyList<DocumentCollaborationCursor>> GetCursorsAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var cursors = _cursors.Values
                .Where(cursor => cursor.DocumentId == documentId)
                .Select(Clone)
                .ToList();

            return Task.FromResult<IReadOnlyList<DocumentCollaborationCursor>>(cursors);
        }
    }

    /// <summary>
    /// Ingests an operation batch produced by another server instance (backplane fan-out): the
    /// batch is appended to the local per-document log under a fresh local sequence number, so
    /// clients polling this instance see it. Cross-replica ordering consistency comes from the
    /// conflict resolver's logical-timestamp order, not from server sequences.
    /// </summary>
    protected void IngestExternalOperationBatch(string documentId, DocumentCollaborationOperationBatch batch)
    {
        lock (_gate)
        {
            var item = Clone(batch);
            item.Sequence = ++_sequence;
            if (!_batches.TryGetValue(documentId, out var documentBatches))
            {
                documentBatches = [];
                _batches[documentId] = documentBatches;
            }

            documentBatches.Add(item);
        }
    }

    /// <summary>Ingests a presence cursor produced by another server instance.</summary>
    protected void IngestExternalCursor(DocumentCollaborationCursor cursor)
    {
        lock (_gate)
        {
            _cursors[GetCursorKey(cursor.DocumentId, cursor.SessionId)] = Clone(cursor);
        }
    }

    private static string GetCursorKey(string documentId, string sessionId)
    {
        return $"{documentId}:{sessionId}";
    }

    private static T Clone<T>(T value)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(value, DocumentEditorJson.Options);
        return System.Text.Json.JsonSerializer.Deserialize<T>(json, DocumentEditorJson.Options)!;
    }
}
