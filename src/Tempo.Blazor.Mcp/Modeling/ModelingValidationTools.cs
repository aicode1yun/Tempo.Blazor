using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using Tempo.Blazor.DocumentLibrary;
using Tempo.Blazor.Modeling;

namespace Tempo.Blazor.Mcp.Modeling;

/// <summary>MCP validation and notation-introspection tools for architecture/modeling models.</summary>
[McpServerToolType]
public static class ModelingValidationTools
{
    [McpServerTool(Name = "modeling_validate")]
    [Description("Validate an architecture/modeling model against structural integrity and notation relationship rules, returning issues (severity, category, message, suggestedFix, offending element/relationship id). Provide either modelId (loads the stored model) or modelJson (a ModelingModelDto for a stateless check).")]
    public static async Task<string> Validate(
        ITempoDocumentLibraryProvider library,
        IModelingModelDocumentProvider models,
        IModelingRelationshipRulesProvider? relationshipRules = null,
        [Description("Modeling model id (GUID) to load and validate.")] Guid? modelId = null,
        [Description("A full ModelingModelDto JSON to validate without loading (alternative to modelId).")] string? modelJson = null)
    {
        ModelingModelDto? model;

        if (modelId is { } id)
        {
            var entry = await library.GetEntryAsync(TempoDocumentKind.Modeling, id);
            if (entry is null)
            {
                return McpToolResults.Failure(McpToolResults.NotFound, $"Model {id} not found.");
            }
            model = await models.GetModelingModelDocumentAsync(id);
            if (model is null)
            {
                return McpToolResults.Failure(McpToolResults.NotFound, $"Model {id} not found.");
            }
        }
        else if (!string.IsNullOrWhiteSpace(modelJson))
        {
            try
            {
                model = JsonSerializer.Deserialize<ModelingModelDto>(modelJson, McpJson.Options);
            }
            catch (JsonException ex)
            {
                return McpToolResults.Failure(McpToolResults.ValidationFailed, $"The model JSON could not be parsed: {ex.Message}");
            }
            if (model is null)
            {
                return McpToolResults.Failure(McpToolResults.ValidationFailed, "The model JSON could not be parsed.");
            }
        }
        else
        {
            return McpToolResults.Failure(McpToolResults.ValidationFailed, "Provide either modelId or modelJson.");
        }

        var issues = ModelingValidationEngine.Validate(model, relationshipRules);
        return McpToolResults.Success(new
        {
            valid = !issues.Any(i => i.Severity == ModelingIssueSeverity.Error),
            issueCount = issues.Count,
            issues
        });
    }

    [McpServerTool(Name = "modeling_list_notations")]
    [Description("List the registered modeling notation profiles (notationKey, displayName, supported element/relationship/viewpoint types). Use this to discover the legal vocabulary before modeling_apply_operations. Returns an empty list when no notation profiles are registered on the host.")]
    public static string ListNotations(
        IModelingNotationProfileProvider? notationProfiles = null,
        IEnumerable<IModelingNotationProfile>? profiles = null)
    {
        var resolved = ResolveProfiles(notationProfiles, profiles);

        return McpToolResults.Success(new
        {
            totalCount = resolved.Count,
            notations = resolved
                .OrderBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(p => p.NotationKey, StringComparer.OrdinalIgnoreCase)
                .Select(p => new
                {
                    notationKey = p.NotationKey,
                    displayName = p.DisplayName,
                    enforcesStrictStencilMapping = p.EnforcesStrictStencilMapping,
                    supportedElementTypes = p.SupportedElementTypes.OrderBy(t => t, StringComparer.Ordinal).ToList(),
                    supportedRelationshipTypes = p.SupportedRelationshipTypes.OrderBy(t => t, StringComparer.Ordinal).ToList(),
                    supportedViewpointKeys = p.SupportedViewpointKeys.OrderBy(t => t, StringComparer.Ordinal).ToList()
                }).ToList()
        });
    }

    private static IReadOnlyList<IModelingNotationProfile> ResolveProfiles(
        IModelingNotationProfileProvider? notationProfiles,
        IEnumerable<IModelingNotationProfile>? profiles)
    {
        // Prefer the registry (a DI-backed IModelingNotationProfileProvider) when it exposes GetAll,
        // otherwise fall back to the raw registered profiles.
        if (notationProfiles is ModelingNotationProfileRegistry registry)
        {
            return registry.GetAll().ToList();
        }
        return profiles?.ToList() ?? [];
    }
}
