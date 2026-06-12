namespace Tempo.Blazor.EmailTemplates.Abstractions.Templating;

/// <summary>
/// Generates a plausible sample data model from a template's extracted variables, so previews and the
/// "fill from template" action produce realistic, fully-populated output.
/// </summary>
public static class SampleDataGenerator
{
    /// <summary>Builds a nested sample model (dictionaries/lists) covering all the given variables.</summary>
    public static Dictionary<string, object?> Generate(IReadOnlyList<TemplateVariableInfo> variables)
    {
        var root = new Dictionary<string, object?>();
        var collections = variables.Where(v => v.Kind == VariableKind.Collection).Select(v => v.Path).ToList();
        var scalars = variables.Where(v => v.Kind == VariableKind.Scalar).Select(v => v.Path).ToList();

        // Shallow paths first so deeper paths can turn an intermediate into a nested object.
        foreach (var path in scalars.OrderBy(p => p.Count(c => c == '.')))
        {
            // Skip scalar paths that live under a collection; they describe the element, handled below.
            if (collections.Any(c => path.StartsWith(c + ".", StringComparison.Ordinal))) continue;
            SetPath(root, path.Split('.'), SampleValue(LastSegment(path)));
        }

        foreach (var path in collections)
            SetPath(root, path.Split('.'), BuildCollection(path, scalars));

        return root;
    }

    private static List<object?> BuildCollection(string collectionPath, List<string> scalars)
    {
        var prefix = collectionPath + ".";
        var elementProps = scalars
            .Where(p => p.StartsWith(prefix, StringComparison.Ordinal))
            .Select(p => p[prefix.Length..])
            .ToList();

        object? MakeElement()
        {
            if (elementProps.Count == 0)
                return SampleValue(Singularize(LastSegment(collectionPath)));

            var element = new Dictionary<string, object?>();
            foreach (var prop in elementProps)
                SetPath(element, prop.Split('.'), SampleValue(LastSegment(prop)));
            return element;
        }

        return new List<object?> { MakeElement(), MakeElement() };
    }

    private static void SetPath(Dictionary<string, object?> root, string[] segments, object? value)
    {
        var current = root;
        for (var i = 0; i < segments.Length - 1; i++)
        {
            if (current.TryGetValue(segments[i], out var existing) && existing is Dictionary<string, object?> dict)
                current = dict;
            else
            {
                var created = new Dictionary<string, object?>();
                current[segments[i]] = created;
                current = created;
            }
        }
        current[segments[^1]] = value;
    }

    private static object? SampleValue(string name)
    {
        var n = name.ToLowerInvariant();

        if (n.StartsWith("is_") || n.StartsWith("has_") || n.StartsWith("can_") ||
            n.StartsWith("show_") || n.StartsWith("enable"))
            return true;

        if (Contains(n, "email")) return "sample@example.com";
        if (Contains(n, "url", "href", "link")) return "https://example.com";
        if (Contains(n, "price", "amount", "total", "sum")) return 99.90;
        if (Contains(n, "count", "qty", "quantity", "age", "number", "num", "id")) return 3;
        if (Contains(n, "date", "_at", "time")) return new DateTime(2026, 6, 11);
        if (Contains(n, "first_name")) return "Jane";
        if (Contains(n, "last_name")) return "Doe";
        if (Contains(n, "name")) return "Sample Name";

        return "Sample " + Humanize(name);
    }

    private static bool Contains(string value, params string[] needles)
        => needles.Any(needle => value.Contains(needle, StringComparison.Ordinal));

    private static string LastSegment(string path)
    {
        var dot = path.LastIndexOf('.');
        return dot < 0 ? path : path[(dot + 1)..];
    }

    private static string Singularize(string word)
        => word.EndsWith('s') && word.Length > 1 ? word[..^1] : word;

    private static string Humanize(string name)
        => name.Replace('_', ' ').Trim();
}
