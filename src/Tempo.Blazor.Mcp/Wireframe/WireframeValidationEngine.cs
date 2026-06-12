using System.Text.Json;
using Tempo.Blazor.Components.Wireframe;
using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Mcp.Wireframe;

/// <summary>The outcome of validating a wireframe document against the schema registry.</summary>
public sealed record WireframeValidationResult(bool IsValid, IReadOnlyList<string> Errors);

/// <summary>
/// Validates a <see cref="WireframeDocument"/> against the <see cref="WireframeSchemaRegistry"/>,
/// producing precise, actionable messages with JSON-style paths — the main feedback loop that lets
/// an LLM correct a design instead of silently producing a broken one.
/// </summary>
public static class WireframeValidationEngine
{
    public static WireframeValidationResult Validate(WireframeDocument document, WireframeSchemaRegistry registry)
    {
        var errors = new List<string>();

        for (var pi = 0; pi < document.Pages.Count; pi++)
        {
            var page = document.Pages[pi];
            var elementIds = new HashSet<string>(StringComparer.Ordinal);

            for (var ei = 0; ei < page.Elements.Count; ei++)
            {
                var el = page.Elements[ei];
                var path = $"pages[{pi}].elements[{ei}]";

                if (!elementIds.Add(el.Id))
                {
                    errors.Add($"{path}.id: duplicate element id '{el.Id}'.");
                }

                if (el.W <= 0)
                {
                    errors.Add($"{path}.w: width must be greater than 0 (was {el.W}).");
                }
                if (el.H <= 0)
                {
                    errors.Add($"{path}.h: height must be greater than 0 (was {el.H}).");
                }

                var schema = registry.GetSchema(el.Type);
                if (schema is null)
                {
                    var suggestion = WireframeCatalog.SuggestType(registry, el.Type);
                    errors.Add(suggestion is null
                        ? $"{path}.type: unknown component type '{el.Type}'."
                        : $"{path}.type: unknown component type '{el.Type}'. Did you mean '{suggestion}'?");
                    continue;
                }

                ValidateProps(el, schema, path, errors);
            }

            for (var ci = 0; ci < page.Connectors.Count; ci++)
            {
                var c = page.Connectors[ci];
                var path = $"pages[{pi}].connectors[{ci}]";
                if (!elementIds.Contains(c.FromId))
                {
                    errors.Add($"{path}.fromId: connector references missing element '{c.FromId}'.");
                }
                if (!elementIds.Contains(c.ToId))
                {
                    errors.Add($"{path}.toId: connector references missing element '{c.ToId}'.");
                }
            }
        }

        return new WireframeValidationResult(errors.Count == 0, errors);
    }

    private static void ValidateProps(
        WireframeElement el, WireframeComponentSchema schema, string path, List<string> errors)
    {
        var defined = schema.Props.ToDictionary(p => p.Name, StringComparer.Ordinal);

        foreach (var (key, value) in el.Props)
        {
            if (!defined.TryGetValue(key, out var prop))
            {
                errors.Add($"{path}.props.{key}: unknown property for component '{schema.Type}'.");
                continue;
            }

            if (prop.Type == PropType.Enum && prop.Options is { Length: > 0 } options
                && value.ValueKind == JsonValueKind.String)
            {
                var v = value.GetString();
                if (v is not null && !options.Contains(v, StringComparer.Ordinal))
                {
                    errors.Add(
                        $"{path}.props.{key}: '{v}' is not a valid value. Allowed: {string.Join(", ", options)}.");
                }
            }
        }
    }
}
