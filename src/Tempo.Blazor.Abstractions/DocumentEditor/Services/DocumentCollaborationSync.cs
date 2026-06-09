using System.Text.Json;
using Tempo.Blazor.DocumentEditor.Interfaces;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.DocumentEditor.Services;

/// <summary>Coordinates optimistic local operations, remote catch-up, and cursor state.</summary>
public class DocumentCollaborationSync
{
    private readonly IDocumentCollaborationProvider _provider;
    private readonly DocumentOperationApplier _applier;
    private readonly DocumentOperationConflictResolver _resolver;
    private readonly DocumentWysiwygOperationMapper _wysiwygOperationMapper;
    private readonly DocumentOperationLog _operationLog = new();
    private DocumentCollaborationSession? _session;
    private long _localSequence;

    /// <summary>Creates a sync coordinator.</summary>
    public DocumentCollaborationSync(
        IDocumentCollaborationProvider provider,
        DocumentOperationApplier? applier = null,
        DocumentOperationConflictResolver? resolver = null,
        DocumentWysiwygOperationMapper? wysiwygOperationMapper = null)
    {
        _provider = provider;
        _applier = applier ?? new DocumentOperationApplier();
        _resolver = resolver ?? new DocumentOperationConflictResolver();
        _wysiwygOperationMapper = wysiwygOperationMapper ?? new DocumentWysiwygOperationMapper();
    }

    /// <summary>Current document.</summary>
    public DocumentEditorDocument Document { get; private set; } = DocumentEditorDocument.Empty();

    /// <summary>Whether local changes are pending save/sync acknowledgement.</summary>
    public bool IsDirty { get; private set; }

    /// <summary>Last seen collaboration sequence.</summary>
    public long LastSeenSequence { get; private set; }

    /// <summary>Remote cursors currently known to the sync coordinator.</summary>
    public IReadOnlyList<DocumentCollaborationCursor> RemoteCursors { get; private set; } = [];

    /// <summary>Operations applied during the last reconnect cycle.</summary>
    public IReadOnlyList<DocumentOperation> LastAppliedRemoteOperations { get; private set; } = [];

    /// <summary>Joined collaboration session.</summary>
    public DocumentCollaborationSession? Session => _session;

    /// <summary>Joins a collaboration session for a document.</summary>
    public async Task<DocumentCollaborationSession> JoinAsync(
        DocumentEditorDocument document,
        string clientId,
        DocumentEditorAuthor author,
        long lastSeenSequence = 0,
        CancellationToken cancellationToken = default)
    {
        Document = Clone(document);
        LastSeenSequence = lastSeenSequence;
        _session = await _provider.JoinAsync(new DocumentCollaborationJoinRequest
        {
            DocumentId = document.DocumentId,
            ClientId = clientId,
            Author = author,
            LastSeenSequence = lastSeenSequence
        }, cancellationToken);
        return _session;
    }

    /// <summary>Leaves the current collaboration session.</summary>
    public async Task LeaveAsync(CancellationToken cancellationToken = default)
    {
        if (_session is not null)
        {
            await _provider.LeaveAsync(_session.Id, cancellationToken);
            _session = null;
        }
    }

    /// <summary>Creates a best-effort operation batch from a local document edit.</summary>
    public DocumentOperationBatch CreateLocalEditBatch(DocumentEditorDocument before, DocumentEditorDocument after)
    {
        var operations = new List<DocumentOperation>();
        var revisionOperationBlockIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var revisionOperation in CreateRevisionOperations(before, after))
        {
            operations.Add(revisionOperation);
            if (!string.IsNullOrWhiteSpace(revisionOperation.Revision?.Range.BlockId))
            {
                revisionOperationBlockIds.Add(revisionOperation.Revision.Range.BlockId);
            }
        }

        foreach (var afterBlock in after.Blocks.OrderBy(block => block.Order))
        {
            var beforeBlock = before.Blocks.FirstOrDefault(block => block.Id == afterBlock.Id);
            if (beforeBlock is null)
            {
                operations.Add(new DocumentOperation
                {
                    Type = DocumentOperationType.InsertBlock,
                    Target = new DocumentOperationTarget { BlockId = afterBlock.Id, Order = afterBlock.Order },
                    Block = Clone(afterBlock),
                    Metadata = CreateMetadata()
                });
                continue;
            }

            if (Math.Abs(beforeBlock.Order - afterBlock.Order) > double.Epsilon)
            {
                operations.Add(new DocumentOperation
                {
                    Type = DocumentOperationType.MoveBlock,
                    Target = new DocumentOperationTarget { BlockId = afterBlock.Id, Order = afterBlock.Order },
                    Metadata = CreateMetadata()
                });
            }

            if (!revisionOperationBlockIds.Contains(afterBlock.Id)
                && !BlocksEqualExceptOrder(beforeBlock, afterBlock))
            {
                operations.Add(new DocumentOperation
                {
                    Type = DocumentOperationType.UpdateBlock,
                    Target = new DocumentOperationTarget
                    {
                        BlockId = afterBlock.Id,
                        Order = Math.Abs(beforeBlock.Order - afterBlock.Order) > double.Epsilon
                            ? afterBlock.Order
                            : null
                    },
                    Block = Clone(afterBlock),
                    Metadata = CreateMetadata()
                });
            }
        }

        foreach (var removed in before.Blocks.Where(block => after.Blocks.All(item => item.Id != block.Id)))
        {
            operations.Add(new DocumentOperation
            {
                Type = DocumentOperationType.DeleteBlock,
                Target = new DocumentOperationTarget { BlockId = removed.Id },
                Metadata = CreateMetadata()
            });
        }

        var batch = new DocumentOperationBatch
        {
            DocumentId = after.DocumentId,
            Operations = operations
        };
        EnsureLocalBatchIdentity(batch);
        return batch;
    }

    private IEnumerable<DocumentOperation> CreateRevisionOperations(DocumentEditorDocument before, DocumentEditorDocument after)
    {
        foreach (var revision in after.Revisions)
        {
            var previous = before.Revisions.FirstOrDefault(item => item.Id == revision.Id);
            if (previous is null)
            {
                yield return CreateRevisionOperation(DocumentOperationType.CreateRevision, revision);
                continue;
            }

            if (previous.Action == DocumentRevisionAction.Pending
                && revision.Action is DocumentRevisionAction.Accepted or DocumentRevisionAction.Rejected)
            {
                yield return CreateRevisionOperation(
                    revision.Action == DocumentRevisionAction.Accepted
                        ? DocumentOperationType.AcceptRevision
                        : DocumentOperationType.RejectRevision,
                    revision);
            }
        }
    }

    private DocumentOperation CreateRevisionOperation(DocumentOperationType type, DocumentRevision revision)
    {
        var target = new DocumentOperationTarget
        {
            BlockId = revision.Range.BlockId,
            InlineIndex = revision.Range.StartInlineIndex,
            Offset = revision.Range.StartOffset
        };
        if (revision.Range.StartOffset is not null && revision.Range.EndOffset is not null)
        {
            target.Length = Math.Max(0, revision.Range.EndOffset.Value - revision.Range.StartOffset.Value);
        }

        var metadata = CreateMetadata();
        metadata.RevisionId = revision.Id;
        metadata.RevisionType = revision.Type.ToString();

        return new DocumentOperation
        {
            Type = type,
            Target = target,
            Text = type == DocumentOperationType.CreateRevision ? revision.PayloadJson : null,
            Revision = Clone(revision),
            Metadata = metadata
        };
    }

    /// <summary>Creates an operation batch from a local WYSIWYG patch.</summary>
    public DocumentOperationBatch CreateLocalPatchBatch(DocumentEditorDocument before, WysiwygPatch patch)
    {
        var batch = _wysiwygOperationMapper.CreateBatch(before, patch, CreateMetadata(patch));
        EnsureLocalBatchIdentity(
            batch,
            patch.TransactionId,
            patch.AfterSelection ?? patch.Selection);
        return batch;
    }

    /// <summary>Applies and broadcasts local operations optimistically.</summary>
    public async Task<DocumentOperationValidationResult> SubmitLocalBatchAsync(
        DocumentOperationBatch batch,
        CancellationToken cancellationToken = default)
    {
        if (_session is null)
        {
            return DocumentOperationValidationResult.Invalid("Collaboration session is not joined.");
        }

        EnsureLocalBatchIdentity(batch);
        EnsureLocalOperationIdentity(batch);
        var resolved = _resolver.Resolve(batch.Operations);
        batch.Operations = resolved.ToList();
        EnsureLocalOperationIdentity(batch);
        var append = _operationLog.Append(batch);
        if (!append.IsValid)
        {
            return append;
        }

        // Phase B operation-relay: a batch may carry only the verbatim canvas op-log JSON
        // (CanvasOperationBatchJson) with no typed Operations — it must still be broadcast. Only a truly
        // empty batch (no typed ops AND no canvas payload) is a no-op.
        if (batch.Operations.Count == 0 && string.IsNullOrEmpty(batch.CanvasOperationBatchJson))
        {
            return DocumentOperationValidationResult.Valid();
        }

        // The C# applier mutates the C# Document mirror from typed operations; canvas-relay batches carry no
        // typed operations (the canvas engine is the source of truth), so there is nothing to apply here.
        if (batch.Operations.Count > 0)
        {
            var apply = _applier.Apply(Document, batch);
            if (!apply.IsValid)
            {
                return apply;
            }
        }

        var remote = await _provider.BroadcastOperationBatchAsync(_session.Id, batch, cancellationToken);
        LastSeenSequence = Math.Max(LastSeenSequence, remote.Sequence);
        IsDirty = true;
        return DocumentOperationValidationResult.Valid();
    }

    /// <summary>Applies a remote operation batch while preserving local dirty state.</summary>
    public DocumentOperationValidationResult ApplyRemoteBatch(DocumentCollaborationOperationBatch remoteBatch)
    {
        if (_session is not null && string.Equals(remoteBatch.SessionId, _session.Id, StringComparison.Ordinal))
        {
            LastSeenSequence = Math.Max(LastSeenSequence, remoteBatch.Sequence);
            return DocumentOperationValidationResult.Valid();
        }

        var wasDirty = IsDirty;
        var append = _operationLog.Append(remoteBatch.Batch);
        if (!append.IsValid)
        {
            return append;
        }

        if (remoteBatch.Batch.Operations.Count == 0)
        {
            LastSeenSequence = Math.Max(LastSeenSequence, remoteBatch.Sequence);
            IsDirty = wasDirty;
            return DocumentOperationValidationResult.Valid();
        }

        var resolved = _resolver.Resolve(remoteBatch.Batch.Operations);
        remoteBatch.Batch.Operations = resolved.ToList();
        var apply = _applier.Apply(Document, remoteBatch.Batch);
        LastSeenSequence = Math.Max(LastSeenSequence, remoteBatch.Sequence);
        IsDirty = wasDirty;
        return apply;
    }

    /// <summary>Fetches and applies missed remote batches after reconnect.</summary>
    public async Task<DocumentOperationValidationResult> ReconnectAsync(CancellationToken cancellationToken = default)
    {
        LastAppliedRemoteOperations = [];
        if (_session is null)
        {
            return DocumentOperationValidationResult.Invalid("Collaboration session is not joined.");
        }

        var appliedOperations = new List<DocumentOperation>();
        var batches = await _provider.GetOperationBatchesAsync(Document.DocumentId, LastSeenSequence, cancellationToken);
        foreach (var batch in batches)
        {
            var isEcho = string.Equals(batch.SessionId, _session.Id, StringComparison.Ordinal);
            var result = ApplyRemoteBatch(batch);
            if (!result.IsValid)
            {
                LastAppliedRemoteOperations = appliedOperations;
                return result;
            }

            if (!isEcho && batch.Batch.Operations.Count > 0)
            {
                appliedOperations.AddRange(batch.Batch.Operations.Select(Clone));
            }
        }

        LastAppliedRemoteOperations = appliedOperations;
        return DocumentOperationValidationResult.Valid();
    }

    /// <summary>Broadcasts a local cursor and refreshes remote cursors when the provider is pull-based.</summary>
    public async Task UpdateCursorAsync(DocumentCollaborationCursor cursor, CancellationToken cancellationToken = default)
    {
        if (_session is null)
        {
            return;
        }

        cursor.DocumentId = Document.DocumentId;
        cursor.SessionId = _session.Id;
        cursor.ClientId = _session.ClientId;
        await _provider.BroadcastCursorAsync(cursor, cancellationToken);
        if (_provider is IDocumentCollaborationRealtimeProvider)
        {
            return;
        }

        RemoteCursors = (await _provider.GetCursorsAsync(Document.DocumentId, cancellationToken))
            .Where(item => item.SessionId != _session.Id)
            .ToList();
    }

    private DocumentOperationMetadata CreateMetadata(WysiwygPatch? patch = null)
    {
        return new DocumentOperationMetadata
        {
            AuthorId = _session?.Author.Id ?? string.Empty,
            OriginSessionId = _session?.Id,
            ClientId = _session?.ClientId,
            LogicalTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            TransactionId = patch?.TransactionId,
            RevisionId = patch?.RevisionId,
            RevisionType = patch?.RevisionType
        };
    }

    private void EnsureLocalBatchIdentity(
        DocumentOperationBatch batch,
        string? transactionId = null,
        WysiwygSelectionSnapshot? selectionAfter = null)
    {
        if (string.IsNullOrWhiteSpace(batch.ClientId))
        {
            batch.ClientId = _session?.ClientId;
        }

        if (string.IsNullOrWhiteSpace(batch.TransactionId))
        {
            batch.TransactionId = !string.IsNullOrWhiteSpace(transactionId)
                ? transactionId
                : batch.Operations
                    .Select(operation => operation.Metadata?.TransactionId)
                    .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        }

        if (batch.LocalSequence <= 0)
        {
            batch.LocalSequence = ++_localSequence;
        }
        else
        {
            _localSequence = Math.Max(_localSequence, batch.LocalSequence);
        }

        if (batch.SelectionAfter is null && selectionAfter is not null)
        {
            batch.SelectionAfter = Clone(selectionAfter);
        }
    }

    private void EnsureLocalOperationIdentity(DocumentOperationBatch batch)
    {
        foreach (var operation in batch.Operations)
        {
            if (string.IsNullOrWhiteSpace(operation.OperationId))
            {
                operation.OperationId = Guid.NewGuid().ToString("N");
            }

            operation.Metadata ??= new DocumentOperationMetadata();
            operation.Metadata.OriginSessionId = _session?.Id;
            operation.Metadata.ClientId ??= _session?.ClientId;
            if (string.IsNullOrWhiteSpace(operation.Metadata.AuthorId))
            {
                operation.Metadata.AuthorId = _session?.Author.Id ?? string.Empty;
            }

            if (operation.Metadata.LogicalTimestamp <= 0)
            {
                operation.Metadata.LogicalTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            }
        }
    }

    private static bool BlocksEqualExceptOrder(DocumentBlock left, DocumentBlock right)
    {
        var normalizedLeft = Clone(left);
        var normalizedRight = Clone(right);
        normalizedLeft.Order = 0;
        normalizedRight.Order = 0;
        var leftJson = JsonSerializer.Serialize(normalizedLeft, DocumentEditorJson.Options);
        var rightJson = JsonSerializer.Serialize(normalizedRight, DocumentEditorJson.Options);
        return string.Equals(leftJson, rightJson, StringComparison.Ordinal);
    }

    private static T Clone<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, DocumentEditorJson.Options);
        return JsonSerializer.Deserialize<T>(json, DocumentEditorJson.Options)!;
    }
}
