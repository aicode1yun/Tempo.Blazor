using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Server;
using Tempo.Blazor.DocumentEditor.Interfaces;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Mcp.DocumentEditor;

/// <summary>MCP tools for reading and saving document editor snapshots.</summary>
[McpServerToolType]
public static class DocumentEditorDocumentTools
{
    [McpServerTool(Name = "document_editor_get_document")]
    [Description("Get a DocumentEditor document by id: typed document JSON, optional raw snapshot and current concurrencyToken. Returns not_found if missing.")]
    public static async Task<string> GetDocument(
        IDocumentEditorProvider documents,
        [Description("DocumentEditor document id.")] string documentId,
        [Description("Whether to include the raw JSON snapshot.")] bool includeJson = true)
    {
        var load = await documents.LoadAsync(documentId, new DocumentEditorLoadOptions
        {
            IncludeDocument = true,
            IncludeJson = includeJson
        });

        if (!load.Found || load.Document is null)
        {
            return McpToolResults.Failure(McpToolResults.NotFound, load.ErrorMessage ?? $"DocumentEditor document '{documentId}' not found.");
        }

        return McpToolResults.Success(new
        {
            id = documentId,
            concurrencyToken = load.ConcurrencyToken,
            document = JsonSerializer.SerializeToNode(load.Document, DocumentEditorJson.Options),
            jsonSnapshot = includeJson ? load.JsonSnapshot : null
        });
    }

    [McpServerTool(Name = "document_editor_get_json")]
    [Description("Get a raw DocumentEditor JSON snapshot by id. Returns not_found if missing.")]
    public static async Task<string> GetJson(
        IDocumentEditorProvider documents,
        [Description("DocumentEditor document id.")] string documentId)
    {
        var json = await documents.LoadJsonAsync(documentId);
        if (json is null)
        {
            return McpToolResults.Failure(McpToolResults.NotFound, $"DocumentEditor document '{documentId}' not found.");
        }

        return McpToolResults.Success(new { id = documentId, jsonSnapshot = json });
    }

    [McpServerTool(Name = "document_editor_save_document")]
    [Description("Save a full DocumentEditor document JSON snapshot. The payload is normalized and validated before saving. Pass expectedConcurrencyToken from document_editor_get_document to avoid overwriting concurrent edits; force=true overwrites without token validation.")]
    public static Task<string> SaveDocument(
        IDocumentEditorProvider documents,
        [Description("DocumentEditor document id.")] string documentId,
        [Description("Full DocumentEditor document JSON.")] string documentJson,
        [Description("Optional optimistic-concurrency token from document_editor_get_document.")] string? expectedConcurrencyToken = null,
        [Description("Overwrite without concurrency token validation.")] bool force = false)
        => SaveDocumentCore(documents, documentId, documentJson, expectedConcurrencyToken, force);

    [McpServerTool(Name = "document_editor_replace_document")]
    [Description("Replace a DocumentEditor document with the provided full JSON snapshot. Alias of document_editor_save_document for clients that distinguish replace from save.")]
    public static Task<string> ReplaceDocument(
        IDocumentEditorProvider documents,
        [Description("DocumentEditor document id.")] string documentId,
        [Description("Full replacement DocumentEditor document JSON.")] string documentJson,
        [Description("Optional optimistic-concurrency token from document_editor_get_document.")] string? expectedConcurrencyToken = null,
        [Description("Overwrite without concurrency token validation.")] bool force = false)
        => SaveDocumentCore(documents, documentId, documentJson, expectedConcurrencyToken, force);

    [McpServerTool(Name = "document_editor_get_versions")]
    [Description("List saved versions for a DocumentEditor document, if the host provider supports version history.")]
    public static async Task<string> GetVersions(
        IDocumentEditorProvider documents,
        [Description("DocumentEditor document id.")] string documentId)
    {
        var versions = await documents.GetVersionsAsync(documentId);
        return McpToolResults.Success(new
        {
            id = documentId,
            versions = versions.Select(v => new
            {
                id = v.Id,
                kind = v.Kind,
                label = v.Label,
                description = v.Description,
                author = v.Author,
                createdAt = v.CreatedAt,
                snapshot = new
                {
                    v.Snapshot.DocumentId,
                    v.Snapshot.SchemaVersion,
                    v.Snapshot.Hash
                }
            }).ToList()
        });
    }

    [McpServerTool(Name = "document_editor_restore_version")]
    [Description("Restore a saved DocumentEditor version by saving its snapshot back to the document. Pass expectedConcurrencyToken to avoid overwriting concurrent edits; force=true overwrites without token validation.")]
    public static async Task<string> RestoreVersion(
        IDocumentEditorProvider documents,
        [Description("DocumentEditor document id.")] string documentId,
        [Description("Version id to restore.")] string versionId,
        [Description("Optional optimistic-concurrency token from document_editor_get_document.")] string? expectedConcurrencyToken = null,
        [Description("Overwrite without concurrency token validation.")] bool force = false)
    {
        var versions = await documents.GetVersionsAsync(documentId);
        var version = versions.FirstOrDefault(v => string.Equals(v.Id, versionId, StringComparison.Ordinal));
        if (version is null)
        {
            return McpToolResults.Failure(McpToolResults.NotFound, $"DocumentEditor version '{versionId}' not found.");
        }

        return await SaveDocumentCore(
            documents,
            documentId,
            version.Snapshot.Json,
            expectedConcurrencyToken,
            force,
            DocumentVersionKind.Restore);
    }

    private static async Task<string> SaveDocumentCore(
        IDocumentEditorProvider documents,
        string documentId,
        string documentJson,
        string? expectedConcurrencyToken,
        bool force,
        DocumentVersionKind? versionKind = null)
    {
        DocumentEditorDocument document;
        try
        {
            document = DocumentEditorJson.Deserialize(documentJson);
        }
        catch (JsonException ex)
        {
            return McpToolResults.Failure(McpToolResults.ValidationFailed, $"The document JSON could not be parsed: {ex.Message}");
        }

        document.DocumentId = documentId;
        var postFixWarnings = DocumentEditorMcpPostFixer.Fix(document);
        var validation = DocumentEditorValidationEngine.Validate(document);
        if (!validation.IsValid)
        {
            return McpToolResults.Failure(McpToolResults.ValidationFailed, "The document is invalid; nothing was saved.", validation.Errors);
        }

        var normalized = DocumentEditorJson.Serialize(document);
        var result = await documents.SaveAsync(new DocumentEditorSaveRequest
        {
            DocumentId = documentId,
            Document = document,
            JsonSnapshot = normalized,
            BaseConcurrencyToken = expectedConcurrencyToken,
            ConcurrencyMode = force
                ? DocumentEditorConcurrencyMode.Force
                : string.IsNullOrEmpty(expectedConcurrencyToken)
                    ? DocumentEditorConcurrencyMode.Optional
                    : DocumentEditorConcurrencyMode.Required,
            NormalizeJson = true,
            VersionKind = versionKind
        });

        if (result.Conflict)
        {
            return McpToolResults.Failure(
                McpToolResults.Conflict,
                $"The document was modified since you read it. Re-read with document_editor_get_document and retry.");
        }

        if (!result.Success)
        {
            return McpToolResults.Failure(
                result.ErrorKind == DocumentEditorSaveErrorKind.Validation
                    ? McpToolResults.ValidationFailed
                    : McpToolResults.Error,
                result.ErrorMessage ?? "The document could not be saved.");
        }

        return McpToolResults.Success(new
        {
            id = documentId,
            concurrencyToken = result.ConcurrencyToken,
            postFixWarnings = DocumentEditorMcpPostFixer.ToToolWarnings(postFixWarnings),
            document = JsonSerializer.SerializeToNode(result.Document ?? document, DocumentEditorJson.Options),
            jsonSnapshot = result.JsonSnapshot
        });
    }
}
