using Tempo.Blazor.Modeling;

namespace Tempo.Blazor.Mcp.Modeling;

/// <summary>
/// Validates a whole <see cref="ModelingModelDto"/> against structural integrity and notation
/// relationship rules, producing <see cref="ModelingIssueDto"/> diagnostics (the same shape the
/// modeling editor surfaces).
/// </summary>
internal static class ModelingValidationEngine
{
    public static List<ModelingIssueDto> Validate(
        ModelingModelDto model,
        IModelingRelationshipRulesProvider? relationshipRules)
    {
        var issues = new List<ModelingIssueDto>();
        var elementsById = new Dictionary<string, ModelingElementDto>(StringComparer.Ordinal);

        foreach (var element in model.Elements)
        {
            if (string.IsNullOrWhiteSpace(element.Id))
            {
                issues.Add(Issue(ModelingIssueSeverity.Error, "validation", "An element has an empty id.",
                    "Assign a unique id to every element."));
                continue;
            }
            if (!elementsById.TryAdd(element.Id, element))
            {
                issues.Add(Issue(ModelingIssueSeverity.Error, "validation", $"Duplicate element id '{element.Id}'.",
                    "Element ids must be unique within the model.", sourceElementId: element.Id));
            }
        }

        var seenRelationshipIds = new HashSet<string>(StringComparer.Ordinal);
        var notationKey = model.Notation;

        foreach (var relationship in model.Relationships)
        {
            if (!string.IsNullOrEmpty(relationship.Id) && !seenRelationshipIds.Add(relationship.Id))
            {
                issues.Add(Issue(ModelingIssueSeverity.Error, "validation", $"Duplicate relationship id '{relationship.Id}'.",
                    "Relationship ids must be unique within the model.", sourceRelationshipId: relationship.Id));
            }

            var hasSource = elementsById.TryGetValue(relationship.SourceElementId, out var source);
            var hasTarget = elementsById.TryGetValue(relationship.TargetElementId, out var target);
            if (!hasSource || !hasTarget)
            {
                issues.Add(Issue(ModelingIssueSeverity.Error, "validation",
                    $"Relationship '{relationship.Id}' references a missing {(hasSource ? "target" : "source")} element.",
                    "Point the relationship at existing elements, or remove it.",
                    sourceRelationshipId: relationship.Id));
                continue;
            }

            if (relationshipRules is null)
            {
                continue;
            }

            var effectiveNotation = !string.IsNullOrWhiteSpace(notationKey) ? notationKey : source!.Notation;
            var ruleResult = relationshipRules.ValidateRelationship(new ModelingRelationshipRuleContext
            {
                NotationKey = effectiveNotation,
                Relationship = relationship,
                SourceElement = source!,
                TargetElement = target!,
                ElementsById = elementsById
            });

            if (!ruleResult.IsValid)
            {
                issues.Add(Issue(ModelingIssueSeverity.Error, "validation",
                    string.IsNullOrWhiteSpace(ruleResult.Message)
                        ? $"Relationship '{relationship.RelationshipType}' from '{source!.SemanticType}' to '{target!.SemanticType}' is not valid for notation '{effectiveNotation}'."
                        : ruleResult.Message,
                    ruleResult.SuggestedFix,
                    sourceRelationshipId: relationship.Id));
            }
        }

        return issues;
    }

    private static ModelingIssueDto Issue(
        ModelingIssueSeverity severity,
        string category,
        string message,
        string suggestedFix,
        string sourceElementId = "",
        string sourceRelationshipId = "")
        => new()
        {
            Id = Guid.NewGuid().ToString("n"),
            Severity = severity,
            Category = category,
            Message = message,
            SuggestedFix = suggestedFix ?? string.Empty,
            SourceElementId = sourceElementId,
            SourceRelationshipId = sourceRelationshipId
        };
}
