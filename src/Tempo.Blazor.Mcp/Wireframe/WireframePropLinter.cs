using System.Text.Json;
using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Mcp.Wireframe;

public static class WireframePropLinter
{
    public static string? SuggestNearest(string key, WireframeComponentSchema schema)
        => schema.Props
            .OrderBy(p => SuggestionScore(key, p))
            .Select(p => p.Name)
            .FirstOrDefault();

    private static int SuggestionScore(string key, PropDef prop)
    {
        var score = WireframeCatalog.Levenshtein(key, prop.Name);
        if (prop.IsRequired)
        {
            score -= 3;
        }

        if (string.Equals(prop.Category, "Content", StringComparison.OrdinalIgnoreCase))
        {
            score -= 1;
        }

        return score;
    }

    public static IReadOnlyList<WireframeLintWarning> Lint(
        WireframeElement el,
        WireframeComponentSchema schema)
        => LintCore(el, schema, normalizeEnums: false);

    public static IReadOnlyList<WireframeLintWarning> LintAndNormalize(
        WireframeElement el,
        WireframeComponentSchema schema)
        => LintCore(el, schema, normalizeEnums: true);

    private static IReadOnlyList<WireframeLintWarning> LintCore(
        WireframeElement el,
        WireframeComponentSchema schema,
        bool normalizeEnums)
    {
        var warnings = new List<WireframeLintWarning>();
        var defined = schema.Props.ToDictionary(p => p.Name, StringComparer.Ordinal);

        foreach (var key in el.Props.Keys.ToList())
        {
            if (!defined.TryGetValue(key, out var prop))
            {
                var hint = SuggestNearest(key, schema);
                warnings.Add(new(
                    el.Id,
                    "unknown-prop",
                    $"props.{key}: unknown for '{schema.Type}'."
                    + (hint is null ? string.Empty : $" Did you mean '{hint}'?")));
                continue;
            }

            var value = el.Props[key];
            if (prop.Type == PropType.Enum && prop.Options is { Length: > 0 } options)
            {
                WarnOrNormalizeEnum(el, prop, key, value, options, warnings, normalizeEnums);
                continue;
            }

            if (!IsExpectedType(prop.Type, value))
            {
                warnings.Add(new(
                    el.Id,
                    "type-mismatch",
                    $"props.{key}: expected {prop.Type}, got {value.ValueKind}."));
            }
        }

        return warnings;
    }

    private static void WarnOrNormalizeEnum(
        WireframeElement el,
        PropDef prop,
        string key,
        JsonElement value,
        string[] options,
        List<WireframeLintWarning> warnings,
        bool normalizeEnums)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            warnings.Add(new(
                el.Id,
                "type-mismatch",
                $"props.{key}: expected Enum, got {value.ValueKind}."));
            return;
        }

        var text = value.GetString();
        var match = options.FirstOrDefault(option =>
            string.Equals(option, text, StringComparison.OrdinalIgnoreCase));
        if (match is not null && match != text)
        {
            if (normalizeEnums)
            {
                el.Props[key] = JsonSerializer.SerializeToElement(match);
            }

            warnings.Add(new(
                el.Id,
                "enum-normalized",
                $"props.{key}: normalized '{text}' -> '{match}'."));
            return;
        }

        if (match is null)
        {
            warnings.Add(new(
                el.Id,
                "enum-out-of-range",
                $"props.{key}: '{text}' not in [{string.Join(", ", options)}]."));
        }
    }

    private static bool IsExpectedType(PropType type, JsonElement value)
        => type switch
        {
            PropType.String or PropType.Color or PropType.Icon => value.ValueKind == JsonValueKind.String,
            PropType.Int => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
            PropType.Double => value.ValueKind == JsonValueKind.Number,
            PropType.Bool => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            PropType.StringList => value.ValueKind == JsonValueKind.Array
                && value.EnumerateArray().All(item => item.ValueKind == JsonValueKind.String),
            PropType.Object => value.ValueKind == JsonValueKind.Object,
            PropType.Enum => value.ValueKind == JsonValueKind.String,
            _ => true
        };
}
