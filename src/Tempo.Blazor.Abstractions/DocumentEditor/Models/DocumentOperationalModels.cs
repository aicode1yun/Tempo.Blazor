using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tempo.Blazor.DocumentEditor.Models;

/// <summary>Saved document version metadata.</summary>
public class DocumentVersion
{
    /// <summary>Stable version id.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Document id.</summary>
    public string DocumentId { get; set; } = string.Empty;

    /// <summary>Version kind.</summary>
    public DocumentVersionKind Kind { get; set; } = DocumentVersionKind.Minor;

    /// <summary>Version label, for example "1.0".</summary>
    public string? Label { get; set; }

    /// <summary>Human-readable version description.</summary>
    public string? Description { get; set; }

    /// <summary>Version author.</summary>
    public DocumentEditorAuthor Author { get; set; } = new();

    /// <summary>Creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Snapshot metadata.</summary>
    public DocumentVersionSnapshot Snapshot { get; set; } = new();
}

/// <summary>Saved document version kind.</summary>
public enum DocumentVersionKind
{
    /// <summary>Minor/manual version.</summary>
    Minor,

    /// <summary>Major/manual version.</summary>
    Major,

    /// <summary>Autosave version.</summary>
    Autosave,

    /// <summary>Restore point.</summary>
    Restore
}

/// <summary>Serialized document snapshot stored for versioning and offline reconciliation.</summary>
public class DocumentVersionSnapshot
{
    /// <summary>Document id.</summary>
    public string DocumentId { get; set; } = string.Empty;

    /// <summary>Schema version.</summary>
    public int SchemaVersion { get; set; } = DocumentEditorDocument.CurrentSchemaVersion;

    /// <summary>Raw JSON snapshot.</summary>
    public string Json { get; set; } = string.Empty;

    /// <summary>Stable hash of the snapshot JSON.</summary>
    public string Hash { get; set; } = string.Empty;
}

/// <summary>Hash helper for document version snapshots.</summary>
public static class DocumentVersionHashHelper
{
    private static readonly JsonSerializerOptions HashJsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    /// <summary>Computes a SHA-256 hash from a snapshot payload.</summary>
    public static string ComputeSnapshotHash(DocumentVersionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var payload = JsonSerializer.Serialize(new
        {
            snapshot.DocumentId,
            snapshot.SchemaVersion,
            snapshot.Json
        }, HashJsonOptions);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

/// <summary>Document editor audit event.</summary>
public class DocumentEditorAuditEvent
{
    /// <summary>Stable audit event id.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Document id.</summary>
    public string DocumentId { get; set; } = string.Empty;

    /// <summary>Audit action.</summary>
    public DocumentEditorAuditAction Action { get; set; } = DocumentEditorAuditAction.Open;

    /// <summary>Target of the audit action.</summary>
    public DocumentEditorAuditTarget Target { get; set; } = new();

    /// <summary>Action result.</summary>
    public DocumentEditorAuditResult Result { get; set; } = DocumentEditorAuditResult.Success;

    /// <summary>Actor who performed the action.</summary>
    public DocumentEditorAuthor? Actor { get; set; }

    /// <summary>Timestamp.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Optional details.</summary>
    public string? Details { get; set; }
}

/// <summary>Document editor audit action.</summary>
public enum DocumentEditorAuditAction
{
    /// <summary>Open document.</summary>
    Open,

    /// <summary>Change content.</summary>
    Change,

    /// <summary>Save document.</summary>
    Save,

    /// <summary>Create version.</summary>
    CreateVersion,

    /// <summary>Create or update comment.</summary>
    Comment,

    /// <summary>Export document.</summary>
    Export,

    /// <summary>Import document.</summary>
    Import,

    /// <summary>Compare document sources.</summary>
    Compare,

    /// <summary>Create finalized rendition.</summary>
    CreateRendition,

    /// <summary>Restore a historical document version.</summary>
    RestoreVersion
}

/// <summary>Audit event target.</summary>
public class DocumentEditorAuditTarget
{
    /// <summary>Target type, for example document, block, comment, or version.</summary>
    public string Type { get; set; } = "document";

    /// <summary>Target id.</summary>
    public string? Id { get; set; }
}

/// <summary>Audit event result.</summary>
public enum DocumentEditorAuditResult
{
    /// <summary>Action succeeded.</summary>
    Success,

    /// <summary>Action failed.</summary>
    Failure,

    /// <summary>Action was denied.</summary>
    Denied
}

/// <summary>Low-level operation intended for future OT/CRDT engines.</summary>
public class DocumentOperation
{
    private string _operationId = Guid.NewGuid().ToString("N");

    /// <summary>Stable operation id.</summary>
    public string OperationId
    {
        get => _operationId;
        set => _operationId = value;
    }

    /// <summary>Stable operation id.</summary>
    [JsonIgnore]
    [Obsolete("Use OperationId. Id remains as a JSON/backward-compatibility alias.")]
    public string Id
    {
        get => OperationId;
        set => OperationId = value;
    }

    /// <summary>Legacy JSON operation id alias.</summary>
    [JsonPropertyName("Id")]
    public string? LegacyOperationId
    {
        get => null;
        set
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                OperationId = value;
            }
        }
    }

    /// <summary>Document schema version this operation was created for.</summary>
    public int SchemaVersion { get; set; } = DocumentEditorDocument.CurrentSchemaVersion;

    /// <summary>Operation type.</summary>
    public DocumentOperationType Type { get; set; } = DocumentOperationType.InsertText;

    /// <summary>Operation target.</summary>
    public DocumentOperationTarget Target { get; set; } = new();

    /// <summary>Operation metadata.</summary>
    public DocumentOperationMetadata Metadata { get; set; } = new();

    /// <summary>Inserted or deleted text value.</summary>
    public string? Text { get; set; }

    /// <summary>Inline mark payload for add/remove mark operations.</summary>
    public InlineMark? Mark { get; set; }

    /// <summary>Block payload for insert block operations.</summary>
    public DocumentBlock? Block { get; set; }

    /// <summary>Previous object layout for drawing-object move operations.</summary>
    public DocumentObjectLayout? OldLayout { get; set; }

    /// <summary>New object layout for drawing-object move operations.</summary>
    public DocumentObjectLayout? NewLayout { get; set; }

    /// <summary>Previous object anchor for drawing-object move operations.</summary>
    public DocumentObjectAnchor? OldAnchor { get; set; }

    /// <summary>New object anchor for drawing-object move operations.</summary>
    public DocumentObjectAnchor? NewAnchor { get; set; }

    /// <summary>Revision payload for tracked-change operations.</summary>
    public DocumentRevision? Revision { get; set; }

    /// <summary>Generic attribute name for set attribute operations.</summary>
    public string? AttributeName { get; set; }

    /// <summary>Generic attribute value for set attribute operations.</summary>
    public string? AttributeValueJson { get; set; }
}

/// <summary>Document operation type.</summary>
public enum DocumentOperationType
{
    /// <summary>Insert text.</summary>
    InsertText,

    /// <summary>Delete text.</summary>
    DeleteText,

    /// <summary>Add an inline mark.</summary>
    AddInlineMark,

    /// <summary>Add an inline mark.</summary>
    AddMark = AddInlineMark,

    /// <summary>Remove an inline mark.</summary>
    RemoveInlineMark,

    /// <summary>Remove an inline mark.</summary>
    RemoveMark = RemoveInlineMark,

    /// <summary>Insert a block.</summary>
    InsertBlock,

    /// <summary>Delete a block.</summary>
    DeleteBlock,

    /// <summary>Move a block.</summary>
    MoveBlock,

    /// <summary>Set a block or document attribute.</summary>
    SetBlockAttribute,

    /// <summary>Update a whole block payload without degrading object content to text.</summary>
    UpdateBlock,

    /// <summary>Move a drawing object to a new anchor and layout position.</summary>
    MoveDrawingObject,

    /// <summary>Create a tracked revision and apply its pending document markup.</summary>
    CreateRevision,

    /// <summary>Accept a tracked revision.</summary>
    AcceptRevision,

    /// <summary>Reject a tracked revision.</summary>
    RejectRevision
}

/// <summary>Target for a document operation.</summary>
public class DocumentOperationTarget
{
    /// <summary>Target block id.</summary>
    public string? BlockId { get; set; }

    /// <summary>Optional target section id.</summary>
    public string? SectionId { get; set; }

    /// <summary>Inline index.</summary>
    public int? InlineIndex { get; set; }

    /// <summary>Stable inline id.</summary>
    public string? InlineId { get; set; }

    /// <summary>Stable drawing object id.</summary>
    public string? ObjectId { get; set; }

    /// <summary>Stable table cell id when the operation targets nested table content.</summary>
    public string? TableCellId { get; set; }

    /// <summary>Character offset.</summary>
    public int? Offset { get; set; }

    /// <summary>Character range length.</summary>
    public int? Length { get; set; }

    /// <summary>Target order for move/insert block operations.</summary>
    public double? Order { get; set; }
}

/// <summary>Operation metadata used by future collaboration engines.</summary>
public class DocumentOperationMetadata
{
    /// <summary>Author id.</summary>
    public string AuthorId { get; set; } = string.Empty;

    /// <summary>Collaboration session that originated the operation.</summary>
    public string? OriginSessionId { get; set; }

    /// <summary>Logical timestamp.</summary>
    public long LogicalTimestamp { get; set; }

    /// <summary>Client id for offline and collaborative editing.</summary>
    public string? ClientId { get; set; }

    /// <summary>WYSIWYG transaction id that produced the operation.</summary>
    public string? TransactionId { get; set; }

    /// <summary>Track-changes revision id that produced the operation.</summary>
    public string? RevisionId { get; set; }

    /// <summary>Track-changes revision type that produced the operation.</summary>
    public string? RevisionType { get; set; }

    /// <summary>Timestamp when the operation was created.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Batch of document operations.</summary>
public class DocumentOperationBatch
{
    /// <summary>Current collaboration operation protocol version.</summary>
    public const int CurrentProtocolVersion = 1;

    /// <summary>Stable batch id.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Document id.</summary>
    public string DocumentId { get; set; } = string.Empty;

    /// <summary>Collaboration operation protocol version used by this batch.</summary>
    public int ProtocolVersion { get; set; } = CurrentProtocolVersion;

    /// <summary>Base version id.</summary>
    public string? BaseVersionId { get; set; }

    /// <summary>Client id that created the local batch.</summary>
    public string? ClientId { get; set; }

    /// <summary>JS runtime transaction id that produced this batch.</summary>
    public string? TransactionId { get; set; }

    /// <summary>Monotonic client-local sequence assigned before broadcasting.</summary>
    public long LocalSequence { get; set; }

    /// <summary>Selection/cursor state after the transaction was applied locally.</summary>
    public WysiwygSelectionSnapshot? SelectionAfter { get; set; }

    /// <summary>Operations in the batch.</summary>
    public List<DocumentOperation> Operations { get; set; } = [];
}

/// <summary>Compatibility helpers for collaboration operation batch protocol versions.</summary>
public static class DocumentOperationBatchProtocol
{
    /// <summary>Normalizes a batch to the current protocol when it is safe to do so.</summary>
    public static DocumentOperationValidationResult Normalize(DocumentOperationBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);

        if (batch.ProtocolVersion == DocumentOperationBatch.CurrentProtocolVersion)
        {
            return DocumentOperationValidationResult.Valid();
        }

        if (batch.ProtocolVersion == 0 && IsLegacyTextOnlyBatch(batch))
        {
            batch.ProtocolVersion = DocumentOperationBatch.CurrentProtocolVersion;
            return DocumentOperationValidationResult.Valid();
        }

        return DocumentOperationValidationResult.Invalid(
            $"Unsupported collaboration protocol version {batch.ProtocolVersion}.");
    }

    private static bool IsLegacyTextOnlyBatch(DocumentOperationBatch batch)
    {
        return batch.Operations.All(operation =>
            operation.Type == DocumentOperationType.SetBlockAttribute
            && string.Equals(operation.AttributeName, "text", StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(operation.Target.BlockId));
    }
}

/// <summary>Validation result for a document operation or batch.</summary>
public class DocumentOperationValidationResult
{
    /// <summary>Whether validation succeeded.</summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>Validation errors.</summary>
    public List<string> Errors { get; set; } = [];

    /// <summary>Creates a successful result.</summary>
    public static DocumentOperationValidationResult Valid()
    {
        return new DocumentOperationValidationResult();
    }

    /// <summary>Creates a failed result.</summary>
    public static DocumentOperationValidationResult Invalid(params string[] errors)
    {
        return new DocumentOperationValidationResult { Errors = errors.ToList() };
    }
}

/// <summary>Joined collaboration session.</summary>
public class DocumentCollaborationSession
{
    /// <summary>Stable session id.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Document id.</summary>
    public string DocumentId { get; set; } = string.Empty;

    /// <summary>Client id.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Joined user.</summary>
    public DocumentEditorAuthor Author { get; set; } = new();

    /// <summary>Last operation sequence visible to this session.</summary>
    public long LastSeenSequence { get; set; }
}

/// <summary>Request to join a document collaboration session.</summary>
public class DocumentCollaborationJoinRequest
{
    /// <summary>Document id.</summary>
    public string DocumentId { get; set; } = string.Empty;

    /// <summary>Client id.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Joining author.</summary>
    public DocumentEditorAuthor Author { get; set; } = new();

    /// <summary>Last operation sequence already known to the client.</summary>
    public long LastSeenSequence { get; set; }
}

/// <summary>Collaboration operation batch with server sequence metadata.</summary>
public class DocumentCollaborationOperationBatch
{
    /// <summary>Server-assigned sequence number.</summary>
    public long Sequence { get; set; }

    /// <summary>Session id that submitted the batch.</summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>Operation batch.</summary>
    public DocumentOperationBatch Batch { get; set; } = new();
}

/// <summary>Collaborative cursor position.</summary>
public class DocumentCollaborationCursor
{
    /// <summary>Document id.</summary>
    public string DocumentId { get; set; } = string.Empty;

    /// <summary>Session id.</summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>Client id.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Display name.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Target block id.</summary>
    public string? BlockId { get; set; }

    /// <summary>Inline index.</summary>
    public int? InlineIndex { get; set; }

    /// <summary>Character offset.</summary>
    public int? Offset { get; set; }

    /// <summary>Cursor color.</summary>
    public string? Color { get; set; }

    /// <summary>Last update timestamp.</summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
