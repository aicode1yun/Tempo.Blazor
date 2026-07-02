using Tempo.Blazor.Components.Wireframe;
using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Mcp.Wireframe;

/// <summary>The outcome of validating a wireframe document against the schema registry.</summary>
public sealed record WireframeValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<WireframeLintWarning> Warnings);

/// <summary>
/// Validates a <see cref="WireframeDocument"/> against the <see cref="WireframeSchemaRegistry"/>,
/// producing precise, actionable messages with JSON-style paths — the main feedback loop that lets
/// an LLM correct a design instead of silently producing a broken one.
/// </summary>
public static class WireframeValidationEngine
{
    public static WireframeValidationResult Validate(WireframeDocument document, WireframeSchemaRegistry registry)
        => Validate(document, registry, scope: null);

    public static WireframeValidationResult Validate(
        WireframeDocument document,
        WireframeSchemaRegistry registry,
        WireframeComponentScope? scope)
    {
        var errors = new List<string>();
        var warnings = new List<WireframeLintWarning>();

        for (var pi = 0; pi < document.Pages.Count; pi++)
        {
            var page = document.Pages[pi];
            var elementIds = new HashSet<string>(StringComparer.Ordinal);
            var targetPackIds = EffectiveTargetPackIds(document, page);

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

                var schema = registry.GetSchema(el.Type, scope, targetPackIds);
                if (schema is null)
                {
                    var suggestion = WireframeCatalog.SuggestType(registry, el.Type, scope, targetPackIds);
                    errors.Add(suggestion is null
                        ? $"{path}.type: unknown component type '{el.Type}'."
                        : $"{path}.type: unknown component type '{el.Type}'. Did you mean '{suggestion}'?");
                    continue;
                }

                ValidateProps(el, schema, warnings);
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

        warnings.AddRange(WireframeDocumentLinter.Lint(document, registry, scope));
        return new WireframeValidationResult(errors.Count == 0, errors, warnings);
    }

    private static IReadOnlyList<string>? EffectiveTargetPackIds(WireframeDocument document, WireframePage page)
        => page.TargetPackIds ?? document.TargetPackIds;

    private static void ValidateProps(
        WireframeElement el,
        WireframeComponentSchema schema,
        List<WireframeLintWarning> warnings)
        => warnings.AddRange(WireframePropLinter.Lint(el, schema));
}
