using System.Text.Json;
using System.Text.Json.Nodes;
using System.ComponentModel;
using ModelContextProtocol.Server;
using Tempo.Blazor.Components.Wireframe;
using Tempo.Blazor.DocumentLibrary;
using Tempo.Blazor.NotionEditor.Interfaces;

namespace Tempo.Blazor.Mcp.Wireframe;

/// <summary>
/// MCP write tools for building a wireframe: granular operation batches and whole-document
/// replacement. Both validate the result against the schema registry before persisting, and
/// support optimistic concurrency via expectedModifiedAt.
/// </summary>
[McpServerToolType]
public static class WireframeOperationTools
{
    private static readonly HashSet<string> StrictWarningCodes =
        new(StringComparer.Ordinal)
        {
            "unknown-prop",
            "enum-normalized",
            "enum-out-of-range",
            "type-mismatch",
            "scaffold-missing-schema"
        };

    [McpServerTool(Name = "wireframe_apply_operations")]
    [Description("Apply an ordered batch of edit operations to a wireframe and save it. operationsJson is a JSON array; each item has an 'op' field: setTitle, addPage, updatePage, removePage, setCanvasSize, addElement, updateElement, removeElement, scaffold, addConnector, updateConnector, removeConnector, stack, row, grid. Page-targeted operations accept optional pageId; when omitted, the current active page is used. addPage returns the new page id in createdIds but does not make it active, so use that id as pageId for subsequent operations on the new page. addElement accepts numeric x/y/w/h, plus below or rightOf with margin for relative placement. scaffold accepts archetype landing, list, detail, form, dashboard or auth and returns a regionMap. stack, row and grid auto-position existing ids or inline children; layout parameters are gap, padding, columns, direction, align, wrap, margin, x, y, w, h and type. Inline children return their ids in createdIds. With schema resolution, w/h may be 'fill' (page or layout size minus padding) or 'auto' (component default size). Props are linted against the component schema: enum casing is normalized and warnings[] reports unknown-prop, enum-normalized, enum-out-of-range and type-mismatch. Document authoring warnings[] also reports non-blocking default-size, off-canvas, overlap, text-overflow and empty-required-content. Set strict=true to reject prop/enum warning batches with validation_failed; document authoring warnings remain advisory. The batch is validated against the schema before saving — nothing is persisted if validation fails. Pass expectedModifiedAt (from wireframe_get_document) to avoid overwriting concurrent edits.")]
    public static async Task<string> ApplyOperationsScoped(
        ITempoDocumentLibraryProvider library,
        IWireframeDocumentProvider documents,
        WireframeSchemaRegistry registry,
        [Description("Wireframe document id (GUID).")] Guid documentId,
        [Description("JSON array of operations.")] string operationsJson,
        [Description("Optional optimistic-concurrency token from wireframe_get_document.")] DateTime? expectedModifiedAt = null,
        [Description("Optional app id used to resolve local custom type names and scoped app component types during validation.")] string? scopeAppId = null,
        [Description("When true, prop lint warnings cause validation_failed and nothing is saved. Default false applies and returns warnings.")] bool strict = false)
    {
        var entry = await library.GetEntryAsync(TempoDocumentKind.Wireframe, documentId);
        if (entry is null)
        {
            return McpToolResults.Failure(McpToolResults.NotFound, $"Wireframe {documentId} not found.");
        }
        if (McpConcurrency.DateTimeConflict(expectedModifiedAt, entry.ModifiedAt, "wireframe_get_document") is { } conflict)
        {
            return McpToolResults.Failure(McpToolResults.Conflict, conflict);
        }

        var current = await documents.GetWireframeDocumentAsync(documentId);
        if (current is null)
        {
            return McpToolResults.Failure(McpToolResults.NotFound, $"Wireframe {documentId} not found.");
        }

        var scope = WireframeComponentScope.FromAppId(scopeAppId);
        var working = WireframeSerializer.Deserialize(WireframeSerializer.Serialize(current));
        var opResult = WireframeOperationEngine.Apply(working, operationsJson, registry, scope);
        if (!opResult.Success)
        {
            return McpToolResults.Failure(McpToolResults.ValidationFailed, "One or more operations failed.", opResult.Errors);
        }

        var validation = WireframeValidationEngine.Validate(working, registry, scope);
        var warnings = DistinctWarnings(opResult.Warnings, validation.Warnings);
        if (strict && warnings.Any(IsStrictWarning))
        {
            return FailureWithWarnings(
                "One or more operations produced strict warnings; nothing was saved.",
                warnings);
        }

        if (!validation.IsValid)
        {
            return FailureWithWarnings(
                "The resulting document is invalid; nothing was saved.",
                warnings,
                validation.Errors);
        }

        await documents.SaveWireframeDocumentAsync(documentId, working);
        var saved = await library.GetEntryAsync(TempoDocumentKind.Wireframe, documentId);

        return McpToolResults.Success(new
        {
            id = documentId,
            applied = opResult.Applied,
            createdIds = opResult.CreatedIds,
            regionMap = opResult.RegionMap,
            warnings,
            modifiedAt = saved?.ModifiedAt
        });
    }

    public static Task<string> ApplyOperations(
        ITempoDocumentLibraryProvider library,
        IWireframeDocumentProvider documents,
        WireframeSchemaRegistry registry,
        Guid documentId,
        string operationsJson,
        DateTime? expectedModifiedAt = null,
        bool strict = false)
        => ApplyOperationsScoped(
            library,
            documents,
            registry,
            documentId,
            operationsJson,
            expectedModifiedAt,
            scopeAppId: null,
            strict: strict);

    [McpServerTool(Name = "wireframe_scaffold")]
    [Description("Create a starter wireframe scaffold in an existing document. The archetype parameter accepts landing, list, detail, form, dashboard or auth. The tool creates desktop (1440px) and mobile (390px) pages, sizes each seeded slot from the resolved component schema defaults, returns createdIds plus a regionMap keyed by stable region names, and validates before saving. Pass scopeAppId for app-scoped component resolution and expectedModifiedAt (from wireframe_get_document) to avoid overwriting concurrent edits. Set strict=true to reject prop/enum or scaffold schema warnings with validation_failed.")]
    public static Task<string> ScaffoldScoped(
        ITempoDocumentLibraryProvider library,
        IWireframeDocumentProvider documents,
        WireframeSchemaRegistry registry,
        [Description("Wireframe document id (GUID).")] Guid documentId,
        [Description("Scaffold archetype: landing, list, detail, form, dashboard or auth.")] string archetype,
        [Description("Optional optimistic-concurrency token from wireframe_get_document.")] DateTime? expectedModifiedAt = null,
        [Description("Optional app id used to resolve local custom type names and scoped app component types during validation.")] string? scopeAppId = null,
        [Description("When true, prop/enum and scaffold schema warnings cause validation_failed and nothing is saved.")] bool strict = false)
    {
        var operationsJson = JsonSerializer.Serialize(
            new[] { new { op = "scaffold", archetype } },
            McpJson.Options);
        return ApplyOperationsScoped(
            library,
            documents,
            registry,
            documentId,
            operationsJson,
            expectedModifiedAt,
            scopeAppId,
            strict);
    }

    public static Task<string> Scaffold(
        ITempoDocumentLibraryProvider library,
        IWireframeDocumentProvider documents,
        WireframeSchemaRegistry registry,
        Guid documentId,
        string archetype,
        DateTime? expectedModifiedAt = null,
        bool strict = false)
        => ScaffoldScoped(
            library,
            documents,
            registry,
            documentId,
            archetype,
            expectedModifiedAt,
            scopeAppId: null,
            strict);

    [McpServerTool(Name = "wireframe_replace_document")]
    [Description("Replace a wireframe's entire content with the provided document JSON and save it. The document is validated against the schema before saving — nothing is persisted if validation fails. Pass expectedModifiedAt (from wireframe_get_document) for optimistic concurrency.")]
    public static async Task<string> ReplaceDocumentScoped(
        ITempoDocumentLibraryProvider library,
        IWireframeDocumentProvider documents,
        WireframeSchemaRegistry registry,
        [Description("Wireframe document id (GUID).")] Guid documentId,
        [Description("The full replacement wireframe document JSON.")] string documentJson,
        [Description("Optional optimistic-concurrency token from wireframe_get_document.")] DateTime? expectedModifiedAt = null,
        [Description("Optional app id used to resolve local custom type names and scoped app component types during validation.")] string? scopeAppId = null)
    {
        var entry = await library.GetEntryAsync(TempoDocumentKind.Wireframe, documentId);
        if (entry is null)
        {
            return McpToolResults.Failure(McpToolResults.NotFound, $"Wireframe {documentId} not found.");
        }
        if (McpConcurrency.DateTimeConflict(expectedModifiedAt, entry.ModifiedAt, "wireframe_get_document") is { } conflict)
        {
            return McpToolResults.Failure(McpToolResults.Conflict, conflict);
        }

        if (!WireframeSerializer.TryDeserialize(documentJson, out var document) || document is null)
        {
            return McpToolResults.Failure(McpToolResults.ValidationFailed, "The document JSON could not be parsed.");
        }

        var validation = WireframeValidationEngine.Validate(document, registry, WireframeComponentScope.FromAppId(scopeAppId));
        if (!validation.IsValid)
        {
            return McpToolResults.Failure(McpToolResults.ValidationFailed, "The document is invalid; nothing was saved.", validation.Errors);
        }

        await documents.SaveWireframeDocumentAsync(documentId, document);
        var saved = await library.GetEntryAsync(TempoDocumentKind.Wireframe, documentId);

        return McpToolResults.Success(new { id = documentId, modifiedAt = saved?.ModifiedAt });
    }

    public static Task<string> ReplaceDocument(
        ITempoDocumentLibraryProvider library,
        IWireframeDocumentProvider documents,
        WireframeSchemaRegistry registry,
        Guid documentId,
        string documentJson,
        DateTime? expectedModifiedAt = null)
        => ReplaceDocumentScoped(library, documents, registry, documentId, documentJson, expectedModifiedAt, scopeAppId: null);

    private static IReadOnlyList<WireframeLintWarning> DistinctWarnings(
        params IReadOnlyList<WireframeLintWarning>[] sources)
    {
        var seen = new HashSet<(string ElementId, string Code, string Hint)>();
        var result = new List<WireframeLintWarning>();
        foreach (var source in sources)
        {
            foreach (var warning in source)
            {
                if (seen.Add((warning.ElementId, warning.Code, warning.Hint)))
                {
                    result.Add(warning);
                }
            }
        }

        return result;
    }

    private static bool IsStrictWarning(WireframeLintWarning warning)
        => StrictWarningCodes.Contains(warning.Code);

    private static string FailureWithWarnings(
        string message,
        IReadOnlyList<WireframeLintWarning> warnings,
        IEnumerable<string>? validationErrors = null)
    {
        var result = new JsonObject
        {
            ["success"] = false,
            ["error"] = McpToolResults.ValidationFailed,
            ["message"] = message,
            ["warnings"] = JsonSerializer.SerializeToNode(warnings, McpJson.Options)
        };

        if (validationErrors is not null)
        {
            var errors = new JsonArray();
            foreach (var error in validationErrors)
            {
                errors.Add(error);
            }

            result["validationErrors"] = errors;
        }

        return result.ToJsonString(McpJson.Options);
    }
}
