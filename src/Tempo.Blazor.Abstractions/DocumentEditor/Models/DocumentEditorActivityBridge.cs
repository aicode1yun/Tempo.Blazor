using Tempo.Blazor.Abstractions.Shared;

namespace Tempo.Blazor.DocumentEditor.Models;

/// <summary>Converts document-editor audit events to shared activity entries.</summary>
public static class DocumentEditorActivityBridge
{
    /// <summary>Entity type used by document-editor activity entries.</summary>
    public const string EntityType = "document-editor-document";

    /// <summary>Converts a document-editor audit event to a shared activity entry.</summary>
    public static TmActivityEntry ToTmActivityEntry(DocumentEditorAuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        var metadata = new Dictionary<string, object>
        {
            ["Result"] = auditEvent.Result.ToString(),
            ["TargetType"] = auditEvent.Target.Type
        };

        if (!string.IsNullOrWhiteSpace(auditEvent.Target.Id))
            metadata["TargetId"] = auditEvent.Target.Id!;

        return new TmActivityEntry
        {
            Id = string.IsNullOrWhiteSpace(auditEvent.Id) ? Guid.NewGuid().ToString("N") : auditEvent.Id,
            EntityRef = TmEntityRef.Create(EntityType, auditEvent.DocumentId),
            Actor = auditEvent.Actor is null ? null : DocumentCommentBridge.ToTmUserRef(auditEvent.Actor),
            Action = ToActionKey(auditEvent.Action),
            Timestamp = auditEvent.CreatedAt == default ? DateTimeOffset.UtcNow : auditEvent.CreatedAt.ToUniversalTime(),
            Summary = auditEvent.Details,
            Metadata = metadata
        };
    }

    private static string ToActionKey(DocumentEditorAuditAction action)
        => action switch
        {
            DocumentEditorAuditAction.CreateVersion => "create-version",
            DocumentEditorAuditAction.CreateRendition => "create-rendition",
            DocumentEditorAuditAction.RestoreVersion => "restore-version",
            _ => action.ToString().ToLowerInvariant()
        };
}
