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
    private readonly DocumentOperationLog _operationLog = new();
    private DocumentCollaborationSession? _session;

    /// <summary>Creates a sync coordinator.</summary>
    public DocumentCollaborationSync(
        IDocumentCollaborationProvider provider,
        DocumentOperationApplier? applier = null,
        DocumentOperationConflictResolver? resolver = null)
    {
        _provider = provider;
        _applier = applier ?? new DocumentOperationApplier();
        _resolver = resolver ?? new DocumentOperationConflictResolver();
    }

    /// <summary>Current document.</summary>
    public DocumentEditorDocument Document { get; private set; } = DocumentEditorDocument.Empty();

    /// <summary>Whether local changes are pending save/sync acknowledgement.</summary>
    public bool IsDirty { get; private set; }

    /// <summary>Last seen collaboration sequence.</summary>
    public long LastSeenSequence { get; private set; }

    /// <summary>Remote cursors currently known to the sync coordinator.</summary>
    public IReadOnlyList<DocumentCollaborationCursor> RemoteCursors { get; private set; } = [];

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

            var beforeText = GetBlockText(beforeBlock);
            var afterText = GetBlockText(afterBlock);
            if (!string.Equals(beforeText, afterText, StringComparison.Ordinal))
            {
                operations.Add(new DocumentOperation
                {
                    Type = DocumentOperationType.SetBlockAttribute,
                    Target = new DocumentOperationTarget { BlockId = afterBlock.Id },
                    AttributeName = "text",
                    AttributeValueJson = JsonSerializer.Serialize(afterText, DocumentEditorJson.Options),
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

        return new DocumentOperationBatch
        {
            DocumentId = after.DocumentId,
            Operations = operations
        };
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

        var resolved = _resolver.Resolve(batch.Operations);
        batch.Operations = resolved.ToList();
        var append = _operationLog.Append(batch);
        if (!append.IsValid)
        {
            return append;
        }

        var apply = _applier.Apply(Document, batch);
        if (!apply.IsValid)
        {
            return apply;
        }

        var remote = await _provider.BroadcastOperationBatchAsync(_session.Id, batch, cancellationToken);
        LastSeenSequence = Math.Max(LastSeenSequence, remote.Sequence);
        IsDirty = true;
        return DocumentOperationValidationResult.Valid();
    }

    /// <summary>Applies a remote operation batch while preserving local dirty state.</summary>
    public DocumentOperationValidationResult ApplyRemoteBatch(DocumentCollaborationOperationBatch remoteBatch)
    {
        var wasDirty = IsDirty;
        var append = _operationLog.Append(remoteBatch.Batch);
        if (!append.IsValid)
        {
            return append;
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
        if (_session is null)
        {
            return DocumentOperationValidationResult.Invalid("Collaboration session is not joined.");
        }

        var batches = await _provider.GetOperationBatchesAsync(Document.DocumentId, LastSeenSequence, cancellationToken);
        foreach (var batch in batches)
        {
            var result = ApplyRemoteBatch(batch);
            if (!result.IsValid)
            {
                return result;
            }
        }

        return DocumentOperationValidationResult.Valid();
    }

    /// <summary>Broadcasts a local cursor and refreshes remote cursors.</summary>
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
        RemoteCursors = (await _provider.GetCursorsAsync(Document.DocumentId, cancellationToken))
            .Where(item => item.SessionId != _session.Id)
            .ToList();
    }

    private DocumentOperationMetadata CreateMetadata()
    {
        return new DocumentOperationMetadata
        {
            AuthorId = _session?.Author.Id ?? string.Empty,
            ClientId = _session?.ClientId,
            LogicalTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
    }

    private static string GetBlockText(DocumentBlock block)
    {
        return block.Content switch
        {
            ParagraphBlockContent paragraph => GetInlineText(paragraph.Inlines),
            HeadingBlockContent heading => GetInlineText(heading.Inlines),
            ListBlockContent list => GetInlineText(list.Inlines),
            QuoteBlockContent quote => GetInlineText(quote.Inlines),
            _ => string.Empty
        };
    }

    private static string GetInlineText(IEnumerable<InlineContent> inlines)
    {
        return string.Concat(inlines.Select(inline => inline switch
        {
            TextRun run => run.Text,
            TokenRun token => string.IsNullOrWhiteSpace(token.DisplayName) ? token.Key : token.DisplayName,
            _ => string.Empty
        }));
    }

    private static T Clone<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, DocumentEditorJson.Options);
        return JsonSerializer.Deserialize<T>(json, DocumentEditorJson.Options)!;
    }
}
