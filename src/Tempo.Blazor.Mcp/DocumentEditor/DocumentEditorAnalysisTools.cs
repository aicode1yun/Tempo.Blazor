using System.ComponentModel;
using ModelContextProtocol.Server;
using Tempo.Blazor.DocumentEditor.Interfaces;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;

namespace Tempo.Blazor.Mcp.DocumentEditor;

/// <summary>MCP validation and analysis tools for DocumentEditor snapshots.</summary>
[McpServerToolType]
public static class DocumentEditorAnalysisTools
{
    [McpServerTool(Name = "document_editor_validate_document")]
    [Description("Validate a full DocumentEditor document JSON snapshot for schema, block, table, image, comment and revision integrity.")]
    public static string ValidateDocument(
        [Description("Full DocumentEditor document JSON to validate.")] string documentJson)
    {
        DocumentEditorDocument document;
        try
        {
            document = DocumentEditorJson.Deserialize(documentJson);
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or NotSupportedException)
        {
            return McpToolResults.Failure(McpToolResults.ValidationFailed, $"The document JSON could not be parsed: {ex.Message}");
        }

        var result = DocumentEditorValidationEngine.Validate(document);
        return McpToolResults.Success(new
        {
            valid = result.IsValid,
            validationErrors = result.Errors
        });
    }

    [McpServerTool(Name = "document_editor_get_outline")]
    [Description("Return a heading outline for a DocumentEditor document using its heading blocks.")]
    public static async Task<string> GetOutline(
        IDocumentEditorProvider documents,
        [Description("DocumentEditor document id.")] string documentId)
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

        var outline = new DocumentOutlineService().GetOutline(load.Document);
        return McpToolResults.Success(new
        {
            id = documentId,
            concurrencyToken = load.ConcurrencyToken,
            outline
        });
    }

    [McpServerTool(Name = "document_editor_search_text")]
    [Description("Search text in a DocumentEditor document body, headers/footers and comments.")]
    public static async Task<string> SearchText(
        IDocumentEditorProvider documents,
        [Description("DocumentEditor document id.")] string documentId,
        [Description("Text or regex to search for.")] string text,
        [Description("Whether matching is case-sensitive.")] bool caseSensitive = false,
        [Description("Whether matching must be whole-word.")] bool wholeWord = false,
        [Description("Whether text should be interpreted as regex.")] bool useRegex = false,
        [Description("Search scope: Body, HeadersFooters, Comments or All.")] DocumentSearchScope scope = DocumentSearchScope.Body)
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

        var results = new DocumentSearchService().Search(load.Document, new DocumentSearchQuery
        {
            Text = text,
            CaseSensitive = caseSensitive,
            WholeWord = wholeWord,
            UseRegex = useRegex,
            Scope = scope
        });

        return McpToolResults.Success(new
        {
            id = documentId,
            concurrencyToken = load.ConcurrencyToken,
            totalCount = results.Count,
            results
        });
    }
}
