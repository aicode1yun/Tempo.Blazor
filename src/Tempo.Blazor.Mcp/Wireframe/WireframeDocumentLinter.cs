using System.Text.Json;
using Tempo.Blazor.Components.Wireframe;
using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Mcp.Wireframe;

public static class WireframeDocumentLinter
{
    private const double ApproximateTextWidth = 7;

    private static readonly HashSet<string> DefaultSizeContainers =
        new(StringComparer.Ordinal) { "TmCard", "TmSection", "TmStackLayout" };

    private static readonly HashSet<string> TextProps =
        new(StringComparer.Ordinal) { "text", "title", "label" };

    public static IReadOnlyList<WireframeLintWarning> Lint(
        WireframeDocument document,
        WireframeSchemaRegistry registry,
        WireframeComponentScope? scope = null)
    {
        var warnings = new List<WireframeLintWarning>();
        foreach (var page in document.Pages)
        {
            var targetPackIds = EffectiveTargetPackIds(document, page);
            foreach (var element in page.Elements)
            {
                var schema = registry.GetSchema(element.Type, scope, targetPackIds);
                if (schema is null)
                {
                    continue;
                }

                AddDefaultSizeWarning(element, schema, warnings);
                AddOffCanvasWarning(element, page, warnings);
                AddTextOverflowWarnings(element, warnings);
                AddEmptyRequiredContentWarnings(element, schema, warnings);
            }

            AddOverlapWarnings(page, warnings);
        }

        return warnings;
    }

    private static IReadOnlyList<string>? EffectiveTargetPackIds(WireframeDocument document, WireframePage page)
        => page.TargetPackIds ?? document.TargetPackIds;

    private static void AddDefaultSizeWarning(
        WireframeElement element,
        WireframeComponentSchema schema,
        List<WireframeLintWarning> warnings)
    {
        if (!DefaultSizeContainers.Contains(element.Type)
            || element.W != schema.DefaultWidth
            || element.H != schema.DefaultHeight)
        {
            return;
        }

        warnings.Add(new(
            element.Id,
            "default-size",
            $"container '{element.Type}' has default size {element.W}x{element.H}; set w/h for the content."));
    }

    private static void AddOffCanvasWarning(
        WireframeElement element,
        WireframePage page,
        List<WireframeLintWarning> warnings)
    {
        if (element.X >= 0
            && element.Y >= 0
            && element.X + element.W <= page.Width
            && element.Y + element.H <= page.Height)
        {
            return;
        }

        var region = WireframeImplementationBrief.Classify(element, page.Width, page.Height);
        warnings.Add(new(
            element.Id,
            "off-canvas",
            $"element exceeds canvas {page.Width}x{page.Height} (region: {region})."));
    }

    private static void AddTextOverflowWarnings(
        WireframeElement element,
        List<WireframeLintWarning> warnings)
    {
        foreach (var (key, value) in element.Props)
        {
            if (!TextProps.Contains(key)
                || value.ValueKind != JsonValueKind.String
                || value.GetString() is not { Length: > 0 } text)
            {
                continue;
            }

            var estimatedWidth = text.Length * ApproximateTextWidth;
            if (estimatedWidth <= element.W)
            {
                continue;
            }

            warnings.Add(new(
                element.Id,
                "text-overflow",
                $"props.{key}: estimated text width {estimatedWidth}px exceeds element width {element.W}px."));
        }
    }

    private static void AddEmptyRequiredContentWarnings(
        WireframeElement element,
        WireframeComponentSchema schema,
        List<WireframeLintWarning> warnings)
    {
        foreach (var prop in schema.Props.Where(p => p.IsRequired))
        {
            if (element.Props.TryGetValue(prop.Name, out var value) && !IsEmpty(value))
            {
                continue;
            }

            warnings.Add(new(
                element.Id,
                "empty-required-content",
                $"props.{prop.Name}: required content is missing or empty."));
        }
    }

    private static bool IsEmpty(JsonElement value)
        => value.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => true,
            JsonValueKind.String => string.IsNullOrWhiteSpace(value.GetString()),
            JsonValueKind.Array => !value.EnumerateArray().Any(),
            _ => false
        };

    private static void AddOverlapWarnings(WireframePage page, List<WireframeLintWarning> warnings)
    {
        for (var i = 0; i < page.Elements.Count; i++)
        {
            for (var j = i + 1; j < page.Elements.Count; j++)
            {
                var first = page.Elements[i];
                var second = page.Elements[j];
                if (!Overlaps(first, second))
                {
                    continue;
                }

                warnings.Add(new(
                    first.Id,
                    "overlap",
                    $"overlaps sibling element '{second.Id}'."));
                warnings.Add(new(
                    second.Id,
                    "overlap",
                    $"overlaps sibling element '{first.Id}'."));
            }
        }
    }

    private static bool Overlaps(WireframeElement first, WireframeElement second)
        => first.X < second.X + second.W
            && first.X + first.W > second.X
            && first.Y < second.Y + second.H
            && first.Y + first.H > second.Y;
}
