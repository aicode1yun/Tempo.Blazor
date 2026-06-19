using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Server;
using Tempo.Blazor.DocumentEditor.Interfaces;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;

namespace Tempo.Blazor.Mcp.DocumentEditor;

/// <summary>MCP write tools for applying DocumentEditor operation batches.</summary>
[McpServerToolType]
public static class DocumentEditorOperationTools
{
    [McpServerTool(Name = "document_editor_apply_operations")]
    [Description("Apply a DocumentOperationBatch, or a JSON array of DocumentOperation objects, to a DocumentEditor document and save the result. Pass expectedConcurrencyToken from document_editor_get_document to avoid overwriting concurrent edits; force=true overwrites without token validation.")]
    public static async Task<string> ApplyOperations(
        IDocumentEditorProvider documents,
        [Description("DocumentEditor document id.")] string documentId,
        [Description("DocumentOperationBatch JSON object, or JSON array of operations.")] string operationsJson,
        [Description("Optional optimistic-concurrency token from document_editor_get_document.")] string? expectedConcurrencyToken = null,
        [Description("Overwrite without concurrency token validation.")] bool force = false)
    {
        var load = await documents.LoadAsync(documentId, new DocumentEditorLoadOptions
        {
            IncludeDocument = true,
            IncludeJson = false
        });

        if (!load.Found || load.Document is null)
        {
            return McpToolResults.Failure(McpToolResults.NotFound, load.ErrorMessage ?? $"DocumentEditor document '{documentId}' not found.");
        }
        if (McpConcurrency.TokenConflict(expectedConcurrencyToken, load.ConcurrencyToken, "document_editor_get_document") is { } conflict)
        {
            return McpToolResults.Failure(McpToolResults.Conflict, conflict);
        }

        var batch = ParseBatch(documentId, operationsJson, out var parseErrors);
        if (batch is null)
        {
            return McpToolResults.Failure(McpToolResults.InvalidOperation, "The operation batch could not be parsed.", parseErrors);
        }

        if (string.IsNullOrWhiteSpace(batch.DocumentId))
        {
            batch.DocumentId = documentId;
        }

        if (!string.IsNullOrWhiteSpace(batch.CanvasOperationBatchJson))
        {
            return McpToolResults.Failure(
                McpToolResults.Unsupported,
                "Raw canvas operation relay batches cannot be applied through IDocumentEditorProvider. Send a typed DocumentOperationBatch or save a full normalized document snapshot.");
        }

        var working = McpJsonHelpers.Clone(load.Document, DocumentEditorJson.Options);
        var applyResult = new DocumentOperationApplier().Apply(working, batch);
        if (!applyResult.IsValid)
        {
            return McpToolResults.Failure(McpToolResults.InvalidOperation, "One or more document operations failed.", applyResult.Errors);
        }

        var postFixWarnings = DocumentEditorMcpPostFixer.Fix(working);
        var validation = DocumentEditorValidationEngine.Validate(working);
        if (!validation.IsValid)
        {
            return McpToolResults.Failure(McpToolResults.ValidationFailed, "The resulting document is invalid; nothing was saved.", validation.Errors);
        }

        var normalized = DocumentEditorJson.Serialize(working);
        var save = await documents.SaveAsync(new DocumentEditorSaveRequest
        {
            DocumentId = documentId,
            Document = working,
            JsonSnapshot = normalized,
            BaseConcurrencyToken = expectedConcurrencyToken,
            ConcurrencyMode = force
                ? DocumentEditorConcurrencyMode.Force
                : string.IsNullOrEmpty(expectedConcurrencyToken)
                    ? DocumentEditorConcurrencyMode.Optional
                    : DocumentEditorConcurrencyMode.Required,
            NormalizeJson = true
        });

        if (save.Conflict)
        {
            return McpToolResults.Failure(
                McpToolResults.Conflict,
                "The document was modified since you read it. Re-read with document_editor_get_document and retry.");
        }

        if (!save.Success)
        {
            return McpToolResults.Failure(McpToolResults.Error, save.ErrorMessage ?? "The document could not be saved.");
        }

        return McpToolResults.Success(new
        {
            id = documentId,
            applied = batch.Operations.Count,
            concurrencyToken = save.ConcurrencyToken,
            postFixWarnings = DocumentEditorMcpPostFixer.ToToolWarnings(postFixWarnings),
            document = JsonSerializer.SerializeToNode(save.Document ?? working, DocumentEditorJson.Options),
            jsonSnapshot = save.JsonSnapshot
        });
    }

    private static DocumentOperationBatch? ParseBatch(
        string documentId,
        string operationsJson,
        out IReadOnlyList<string> errors)
    {
        try
        {
            var node = JsonNode.Parse(operationsJson);
            if (node is JsonArray)
            {
                var operations = JsonSerializer.Deserialize<List<DocumentOperation>>(
                    operationsJson,
                    DocumentEditorJson.Options);
                errors = [];
                return new DocumentOperationBatch
                {
                    DocumentId = documentId,
                    Operations = operations ?? []
                };
            }

            if (node is JsonObject)
            {
                var batch = JsonSerializer.Deserialize<DocumentOperationBatch>(
                    operationsJson,
                    DocumentEditorJson.Options);
                errors = [];
                return batch;
            }

            errors = ["operationsJson: expected a JSON object or array."];
            return null;
        }
        catch (JsonException ex)
        {
            errors = [$"operationsJson: invalid JSON ({ex.Message})."];
            return null;
        }
    }
}
