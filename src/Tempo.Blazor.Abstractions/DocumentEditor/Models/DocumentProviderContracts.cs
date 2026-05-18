using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tempo.Blazor.DocumentEditor.Models;

/// <summary>Load options for document editor providers.</summary>
public class DocumentEditorLoadOptions
{
    /// <summary>Whether raw JSON should be included in the result.</summary>
    public bool IncludeJson { get; set; } = true;

    /// <summary>Whether the materialized document should be included in the result.</summary>
    public bool IncludeDocument { get; set; } = true;
}

/// <summary>Result returned when loading a document editor snapshot.</summary>
public class DocumentEditorLoadResult
{
    /// <summary>Whether the document was found.</summary>
    public bool Found { get; set; } = true;

    /// <summary>Materialized document.</summary>
    public DocumentEditorDocument? Document { get; set; }

    /// <summary>Raw JSON snapshot.</summary>
    public string? JsonSnapshot { get; set; }

    /// <summary>Concurrency token for optimistic saves.</summary>
    public string? ConcurrencyToken { get; set; }

    /// <summary>Error message when load failed.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Creates a not-found result.</summary>
    public static DocumentEditorLoadResult NotFound(string? errorMessage = null)
    {
        return new DocumentEditorLoadResult
        {
            Found = false,
            ErrorMessage = errorMessage
        };
    }
}

/// <summary>Save request for a document editor snapshot.</summary>
public class DocumentEditorSaveRequest
{
    /// <summary>Document id.</summary>
    public string DocumentId { get; set; } = string.Empty;

    /// <summary>Materialized document to save.</summary>
    public DocumentEditorDocument? Document { get; set; }

    /// <summary>Raw JSON snapshot to save.</summary>
    public string? JsonSnapshot { get; set; }

    /// <summary>Expected concurrency token.</summary>
    public string? BaseConcurrencyToken { get; set; }

    /// <summary>Concurrency behavior.</summary>
    public DocumentEditorConcurrencyMode ConcurrencyMode { get; set; } = DocumentEditorConcurrencyMode.Required;

    /// <summary>Whether raw JSON should be normalized before it is stored.</summary>
    public bool NormalizeJson { get; set; } = true;

    /// <summary>Author who saved the document.</summary>
    public DocumentEditorAuthor? Author { get; set; }

    /// <summary>Whether the save was triggered by autosave instead of an explicit user action.</summary>
    public bool IsAutosave { get; set; }

    /// <summary>Optional version kind for providers that create versions from saves.</summary>
    public DocumentVersionKind? VersionKind { get; set; }
}

/// <summary>Save result for a document editor snapshot.</summary>
public class DocumentEditorSaveResult
{
    /// <summary>Whether save succeeded.</summary>
    public bool Success { get; set; }

    /// <summary>Whether save failed because of an optimistic concurrency conflict.</summary>
    public bool Conflict { get; set; }

    /// <summary>New concurrency token.</summary>
    public string? ConcurrencyToken { get; set; }

    /// <summary>Saved document.</summary>
    public DocumentEditorDocument? Document { get; set; }

    /// <summary>Saved JSON snapshot.</summary>
    public string? JsonSnapshot { get; set; }

    /// <summary>Error message when save failed.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Error classification used by autosave retry policy and UI.</summary>
    public DocumentEditorSaveErrorKind ErrorKind { get; set; } = DocumentEditorSaveErrorKind.None;

    /// <summary>Creates a successful save result.</summary>
    public static DocumentEditorSaveResult Saved(DocumentEditorDocument document, string jsonSnapshot, string concurrencyToken)
    {
        return new DocumentEditorSaveResult
        {
            Success = true,
            Document = document,
            JsonSnapshot = jsonSnapshot,
            ConcurrencyToken = concurrencyToken
        };
    }

    /// <summary>Creates a conflict save result.</summary>
    public static DocumentEditorSaveResult ConcurrencyConflict(string? currentConcurrencyToken = null)
    {
        return new DocumentEditorSaveResult
        {
            Success = false,
            Conflict = true,
            ConcurrencyToken = currentConcurrencyToken,
            ErrorKind = DocumentEditorSaveErrorKind.Conflict,
            ErrorMessage = "The document was changed by another writer."
        };
    }
}

/// <summary>Classifies a failed document editor save.</summary>
public enum DocumentEditorSaveErrorKind
{
    /// <summary>No explicit classification was supplied.</summary>
    None,

    /// <summary>The failure is expected to succeed if retried later.</summary>
    Recoverable,

    /// <summary>The failure is caused by an optimistic concurrency conflict.</summary>
    Conflict,

    /// <summary>The submitted document failed provider validation.</summary>
    Validation,

    /// <summary>The current user is not authorized to save.</summary>
    Unauthorized,

    /// <summary>The failure should not be retried automatically.</summary>
    NonRecoverable
}

/// <summary>Optimistic concurrency mode for document saves.</summary>
public enum DocumentEditorConcurrencyMode
{
    /// <summary>Provider must validate the supplied token.</summary>
    Required,

    /// <summary>Provider validates the token only when one was supplied.</summary>
    Optional,

    /// <summary>Provider overwrites without token validation.</summary>
    Force
}

/// <summary>Offline behavior used by the document editor component.</summary>
public enum DocumentEditorOfflineMode
{
    /// <summary>Offline draft handling is disabled.</summary>
    Disabled,

    /// <summary>Offline draft handling is enabled when a store is provided.</summary>
    Enabled
}

/// <summary>Host-controlled permissions for <c>TmDocumentEditor</c>.</summary>
public class DocumentEditorPermissions
{
    /// <summary>Whether the current user can read the document.</summary>
    public bool CanRead { get; set; } = true;

    /// <summary>Whether the current user can edit document content.</summary>
    public bool CanEdit { get; set; } = true;

    /// <summary>Whether the current user can create comments and replies.</summary>
    public bool CanComment { get; set; } = true;

    /// <summary>Whether the current user can create review suggestions.</summary>
    public bool CanSuggest { get; set; } = true;

    /// <summary>Whether the current user can accept or reject review suggestions.</summary>
    public bool CanReviewSuggestions { get; set; } = true;

    /// <summary>Whether the current user can create document versions.</summary>
    public bool CanCreateVersion { get; set; } = true;

    /// <summary>Whether the current user can import external document formats.</summary>
    public bool CanImport { get; set; } = true;

    /// <summary>Whether the current user can export the document.</summary>
    public bool CanExport { get; set; } = true;

    /// <summary>Whether the current user can view audit information exposed by host UI.</summary>
    public bool CanViewAudit { get; set; } = false;
}

/// <summary>Behavior used when an audit sink fails while recording an editor event.</summary>
public enum DocumentEditorAuditFailureMode
{
    /// <summary>Audit failures are propagated to the triggering editor workflow.</summary>
    Blocking,

    /// <summary>Audit failures are swallowed so the editor workflow can continue.</summary>
    NonBlocking
}

/// <summary>Request for creating a document version.</summary>
public class DocumentVersionCreateRequest
{
    /// <summary>Document id.</summary>
    public string DocumentId { get; set; } = string.Empty;

    /// <summary>Version kind.</summary>
    public DocumentVersionKind Kind { get; set; } = DocumentVersionKind.Minor;

    /// <summary>Version label.</summary>
    public string? Label { get; set; }

    /// <summary>Version description.</summary>
    public string? Description { get; set; }

    /// <summary>Version author.</summary>
    public DocumentEditorAuthor Author { get; set; } = new();
}

/// <summary>Options that guide offline document storage.</summary>
public class DocumentOfflineOptions
{
    /// <summary>Whether offline draft storage is enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Maximum retained pending drafts per document.</summary>
    public int MaxDraftsPerDocument { get; set; } = 10;

    /// <summary>Whether operation batches should be stored with drafts.</summary>
    public bool StoreOperationBatches { get; set; } = true;
}

/// <summary>Request for synchronizing an offline draft with the authoritative provider.</summary>
public class DocumentSyncRequest
{
    /// <summary>Draft to synchronize.</summary>
    public DocumentOfflineDraft Draft { get; set; } = new();

    /// <summary>Operation batches being submitted.</summary>
    public List<DocumentOperationBatch> OperationBatches { get; set; } = [];

    /// <summary>Whether the draft should be deleted from offline storage after a successful sync.</summary>
    public bool DeleteDraftOnSuccess { get; set; } = true;
}

/// <summary>Result of offline draft synchronization.</summary>
public class DocumentSyncResult
{
    /// <summary>Whether synchronization succeeded.</summary>
    public bool Success { get; set; }

    /// <summary>Saved document result.</summary>
    public DocumentEditorSaveResult? SaveResult { get; set; }

    /// <summary>Conflict returned when synchronization cannot be completed automatically.</summary>
    public DocumentSyncConflict? Conflict { get; set; }

    /// <summary>Error message when synchronization failed.</summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>Image provider options.</summary>
public class DocumentImageProviderOptions
{
    /// <summary>Validation options for uploaded images.</summary>
    public DocumentImageValidationOptions Validation { get; set; } = new();

    /// <summary>Default lifetime for generated image URLs.</summary>
    public TimeSpan UrlLifetime { get; set; } = TimeSpan.FromMinutes(15);
}

/// <summary>Result of committing image assets to a document save.</summary>
public class DocumentImageCommitResult
{
    /// <summary>Whether commit succeeded.</summary>
    public bool Success { get; set; }

    /// <summary>Committed asset ids.</summary>
    public List<string> AssetIds { get; set; } = [];

    /// <summary>Error message when commit failed.</summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>Request for creating an immutable rendition.</summary>
public class DocumentRenditionRequest
{
    /// <summary>Document id.</summary>
    public string DocumentId { get; set; } = string.Empty;

    /// <summary>Document version id.</summary>
    public string DocumentVersionId { get; set; } = string.Empty;

    /// <summary>Rendition options.</summary>
    public DocumentRenditionOptions Options { get; set; } = new();

    /// <summary>Actor requesting the rendition.</summary>
    public DocumentEditorAuthor? Actor { get; set; }
}

/// <summary>Options for rendition generation.</summary>
public class DocumentRenditionOptions
{
    /// <summary>Whether a PDF attachment should be produced.</summary>
    public bool IncludePdfAttachment { get; set; } = true;

    /// <summary>Whether preview images should be produced for pages.</summary>
    public bool IncludePreviewImages { get; set; } = true;

    /// <summary>Whether an anchor map should be produced.</summary>
    public bool IncludeAnchorMap { get; set; } = true;
}

/// <summary>Result of rendition generation.</summary>
public class DocumentRenditionResult
{
    /// <summary>Whether rendition generation succeeded.</summary>
    public bool Success { get; set; }

    /// <summary>Created rendition.</summary>
    public DocumentRendition? Rendition { get; set; }

    /// <summary>Error message when generation failed.</summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>Shared JSON options for document editor snapshots.</summary>
public static class DocumentEditorJson
{
    /// <summary>Default serializer options for document editor snapshots.</summary>
    public static JsonSerializerOptions Options { get; } = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    /// <summary>Serializes a document to normalized JSON.</summary>
    public static string Serialize(DocumentEditorDocument document)
    {
        return JsonSerializer.Serialize(document, Options);
    }

    /// <summary>Deserializes a document from JSON.</summary>
    public static DocumentEditorDocument Deserialize(string json)
    {
        var document = JsonSerializer.Deserialize<DocumentEditorDocument>(json, Options)
            ?? throw new JsonException("Document editor JSON snapshot is empty.");

        NormalizeDocument(document);
        return document;
    }

    /// <summary>Normalizes a document JSON snapshot.</summary>
    public static string Normalize(string json)
    {
        return Serialize(Deserialize(json));
    }

    private static void NormalizeDocument(DocumentEditorDocument document)
    {
        if (document.SchemaVersion > DocumentEditorDocument.CurrentSchemaVersion)
        {
            throw new JsonException($"Unsupported document editor schema version {document.SchemaVersion}.");
        }

        if (document.SchemaVersion <= 0)
        {
            document.SchemaVersion = DocumentEditorDocument.CurrentSchemaVersion;
        }

        if (string.IsNullOrWhiteSpace(document.DocumentId))
        {
            document.DocumentId = Guid.NewGuid().ToString("N");
        }

        document.Metadata ??= new DocumentEditorMetadata();
        document.PageSettings ??= new DocumentPageSettings();
        document.Theme ??= new DocumentEditorTheme();
        document.Sections ??= [];
        document.Blocks ??= [];
        document.Comments ??= [];
        document.Notes ??= [];
        document.HeadersFooters ??= [];
        document.Revisions ??= [];
        document.Assets ??= [];
        document.Anchors ??= [];
        document.RestrictedMarkers ??= [];

        if (document.Sections.Count == 0)
        {
            document.Sections.Add(new DocumentSection
            {
                Order = 0,
                Properties = new DocumentSectionProperties()
            });
        }

        foreach (var block in document.Blocks)
        {
            if (string.IsNullOrWhiteSpace(block.Id))
            {
                block.Id = Guid.NewGuid().ToString("N");
            }

            block.Content ??= new ParagraphBlockContent();
        }
    }
}
