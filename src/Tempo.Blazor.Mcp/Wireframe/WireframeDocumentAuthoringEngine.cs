using Tempo.Blazor.Components.Wireframe;
using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Mcp.Wireframe;

/// <summary>Result of preparing a whole wireframe document for persistence.</summary>
public sealed record WireframeDocumentAuthoringResult(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<WireframeLintWarning> Warnings);

/// <summary>
/// Normalizes whole-document authoring payloads before they are validated and saved by MCP tools.
/// </summary>
public static class WireframeDocumentAuthoringEngine
{
    public static WireframeDocumentAuthoringResult NormalizeAndValidate(
        WireframeDocument document,
        WireframeSchemaRegistry registry,
        WireframeComponentScope? scope)
    {
        var errors = new List<string>();
        var warnings = new List<WireframeLintWarning>();

        document.EnsureActivePage();
        for (var pageIndex = 0; pageIndex < document.Pages.Count; pageIndex++)
        {
            var page = document.Pages[pageIndex];
            page.EnsureDefaultLayer();
            var targetPackIds = EffectiveTargetPackIds(document, page);

            for (var elementIndex = 0; elementIndex < page.Elements.Count; elementIndex++)
            {
                var element = page.Elements[elementIndex];
                var path = $"pages[{pageIndex}].elements[{elementIndex}]";
                ResolveElementRole(element, path, registry, scope, targetPackIds, warnings, errors);
                NormalizeProps(element, registry, scope, targetPackIds, warnings);
                ClampElementToPage(element, page, warnings);
            }
        }

        var validation = WireframeValidationEngine.Validate(document, registry, scope);
        return new(
            errors.Count == 0 && validation.IsValid,
            DistinctErrors(errors, validation.Errors),
            DistinctWarnings(warnings, validation.Warnings));
    }

    public static WireframeDocumentAuthoringResult ValidateReplacement(
        WireframeDocument document,
        WireframeSchemaRegistry registry,
        WireframeComponentScope? scope)
    {
        var validation = WireframeValidationEngine.Validate(document, registry, scope);
        return new(validation.IsValid, validation.Errors, validation.Warnings);
    }

    private static void ResolveElementRole(
        WireframeElement element,
        string path,
        WireframeSchemaRegistry registry,
        WireframeComponentScope? scope,
        IReadOnlyList<string>? targetPackIds,
        List<WireframeLintWarning> warnings,
        List<string> errors)
    {
        var role = NormalizeOptional(element.Role);
        if (role is null)
        {
            element.Role = null;
            return;
        }

        element.Role = role;
        var candidates = registry.ResolveByRole(role, scope, targetPackIds);
        if (candidates.Count == 0)
        {
            errors.Add(
                $"{path}.role: no component maps role '{role}' in target packs ({DescribeTargetPacks(targetPackIds)}); add a schema role mapping to close this gap.");
            return;
        }

        element.Type = candidates[0].Type;
        if (candidates.Count > 1)
        {
            warnings.Add(CreateAmbiguousRoleWarning(element.Id, role, candidates));
        }
    }

    private static void NormalizeProps(
        WireframeElement element,
        WireframeSchemaRegistry registry,
        WireframeComponentScope? scope,
        IReadOnlyList<string>? targetPackIds,
        List<WireframeLintWarning> warnings)
    {
        var schema = registry.GetSchema(element.Type, scope, targetPackIds);
        if (schema is not null)
        {
            warnings.AddRange(WireframePropLinter.LintAndNormalize(element, schema));
        }
    }

    private static void ClampElementToPage(
        WireframeElement element,
        WireframePage page,
        List<WireframeLintWarning> warnings)
    {
        var originalX = element.X;
        var originalY = element.Y;
        var originalW = element.W;
        var originalH = element.H;

        if (page.Width > 0 && element.W > 0)
        {
            element.W = Math.Min(element.W, page.Width);
            element.X = Clamp(element.X, 0, Math.Max(0, page.Width - element.W));
        }

        if (page.Height > 0 && element.H > 0)
        {
            element.H = Math.Min(element.H, page.Height);
            element.Y = Clamp(element.Y, 0, Math.Max(0, page.Height - element.H));
        }

        if (element.X != originalX
            || element.Y != originalY
            || element.W != originalW
            || element.H != originalH)
        {
            warnings.Add(new(
                element.Id,
                "clamped-to-canvas",
                $"element was clamped to page bounds {page.Width}x{page.Height}."));
        }
    }

    private static double Clamp(double value, double min, double max)
        => Math.Min(Math.Max(value, min), max);

    private static WireframeLintWarning CreateAmbiguousRoleWarning(
        string elementId,
        string role,
        IReadOnlyList<WireframeComponentSchema> candidates)
    {
        var selected = candidates[0].Type;
        var alternatives = string.Join(", ", candidates.Skip(1).Select(candidate => candidate.Type).Take(5));
        var suffix = candidates.Count > 6 ? ", ..." : string.Empty;
        return new WireframeLintWarning(
            elementId,
            "role-ambiguous",
            $"role '{role}' resolved to '{selected}' but also matched: {alternatives}{suffix}.");
    }

    private static IReadOnlyList<string>? EffectiveTargetPackIds(WireframeDocument document, WireframePage page)
        => page.TargetPackIds ?? document.TargetPackIds;

    private static string DescribeTargetPacks(IReadOnlyList<string>? targetPackIds)
        => targetPackIds is null || targetPackIds.Count == 0
            ? "all visible packs"
            : string.Join(", ", targetPackIds);

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IReadOnlyList<string> DistinctErrors(
        IReadOnlyList<string> first,
        IReadOnlyList<string> second)
        => first.Concat(second)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyList<WireframeLintWarning> DistinctWarnings(
        IReadOnlyList<WireframeLintWarning> first,
        IReadOnlyList<WireframeLintWarning> second)
    {
        var seen = new HashSet<(string ElementId, string Code, string Hint)>();
        var result = new List<WireframeLintWarning>();
        foreach (var warning in first.Concat(second))
        {
            if (seen.Add((warning.ElementId, warning.Code, warning.Hint)))
            {
                result.Add(warning);
            }
        }

        return result;
    }
}
