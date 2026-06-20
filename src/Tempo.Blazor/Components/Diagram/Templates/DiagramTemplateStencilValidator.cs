using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Serialization;
using Tempo.Blazor.Components.Diagram.Stencils;

namespace Tempo.Blazor.Components.Diagram.Templates;

/// <summary>Validates that a diagram template references registered Tempo-original stencils.</summary>
public static class DiagramTemplateStencilValidator
{
    /// <summary>Validates node stencil ids and edge connector types in <paramref name="template"/>.</summary>
    public static DiagramTemplateStencilValidationResult Validate(DiagramTemplate template, DiagramStencilRegistry stencilRegistry)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(stencilRegistry);

        var errors = new List<string>();
        if (!DiagramSerializer.TryDeserialize(template.DocumentJson, out var document) || document is null)
        {
            errors.Add($"template-json-invalid:{template.Id}");
            return new DiagramTemplateStencilValidationResult(errors);
        }

        var tempoStencils = stencilRegistry.GetTempoOriginal().ToList();
        var tempoStencilIds = tempoStencils.Select(stencil => stencil.Id).ToHashSet(StringComparer.Ordinal);
        var tempoConnectorTypes = tempoStencils
            .Where(stencil => stencil.Kind == DiagramStencilKind.Edge)
            .Select(stencil => stencil.EdgeDefaults?.ConnectorType)
            .Where(connectorType => !string.IsNullOrWhiteSpace(connectorType))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var node in document.Pages.SelectMany(page => page.Nodes))
        {
            if (!tempoStencilIds.Contains(node.StencilId))
                errors.Add($"node-stencil-not-tempo-original:{node.StencilId}");
        }

        foreach (var edge in document.Pages.SelectMany(page => page.Edges))
        {
            if (!string.IsNullOrWhiteSpace(edge.ConnectorType) && !tempoConnectorTypes.Contains(edge.ConnectorType))
                errors.Add($"edge-connector-not-tempo-original:{edge.ConnectorType}");
        }

        return new DiagramTemplateStencilValidationResult(errors);
    }
}
