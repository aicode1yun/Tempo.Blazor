using System.Text.Json;
using System.Text.Json.Nodes;

namespace JsonDocumentationGenerator;

/// <summary>
/// Backward-compatible component JSON enricher used by the legacy <c>--enrich</c> flow.
/// The extraction is delegated to the package-aware scanner so nested components, code-behind
/// files, generic parameters, and <c>[EditorRequired]</c> metadata are handled consistently.
/// </summary>
internal static class ParameterEnricher
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static int Run(string srcComponentsDir, string jsonComponentsDir)
    {
        var repoRoot = FindRepoRoot(srcComponentsDir) ?? Path.GetFullPath(Path.Combine(srcComponentsDir, "..", "..", ".."));
        var generatedItems = ComponentDocumentationScanner.Scan(srcComponentsDir, repoRoot, "Tempo.Blazor");
        var existingFiles = Directory.Exists(jsonComponentsDir)
            ? Directory.GetFiles(jsonComponentsDir, "*.json", SearchOption.AllDirectories)
                .Select(path => new { Name = Path.GetFileNameWithoutExtension(path), Path = path })
                .Where(item => !string.IsNullOrWhiteSpace(item.Name))
                .ToDictionary(item => item.Name!, item => item.Path, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var updated = 0;
        foreach (var item in generatedItems.OrderBy(i => i.Category).ThenBy(i => i.ItemName, StringComparer.OrdinalIgnoreCase))
        {
            var targetPath = ResolveTargetPath(jsonComponentsDir, existingFiles, item);
            JsonObject output;
            if (File.Exists(targetPath))
            {
                var manual = JsonNode.Parse(File.ReadAllText(targetPath))?.AsObject();
                output = manual is null
                    ? item.Json
                    : DocumentJsonMerger.Merge(item.Json, manual, item.IsFallbackDescription);
            }
            else
            {
                output = item.Json;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            File.WriteAllText(targetPath, output.ToJsonString(JsonOptions));
            updated++;
        }

        return updated;
    }

    private static string ResolveTargetPath(
        string jsonComponentsDir,
        IReadOnlyDictionary<string, string> existingFiles,
        GeneratedDocumentationItem item)
    {
        if (existingFiles.TryGetValue(item.ItemName, out var existing))
        {
            return existing;
        }

        return Path.Combine(jsonComponentsDir, SafePathSegment(item.Category), SafePathSegment(item.ItemName) + ".json");
    }

    private static string SafePathSegment(string value)
    {
        var cleaned = string.Concat(value.Select(ch => char.IsLetterOrDigit(ch) || ch is '.' or '_' or '-' ? ch : '-'));
        while (cleaned.Contains("--", StringComparison.Ordinal))
        {
            cleaned = cleaned.Replace("--", "-", StringComparison.Ordinal);
        }

        return string.IsNullOrWhiteSpace(cleaned) ? "General" : cleaned.Trim('-');
    }

    private static string? FindRepoRoot(string startPath)
    {
        var dir = new DirectoryInfo(Path.GetFullPath(startPath));
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "JsonDocumentation"))
                && Directory.Exists(Path.Combine(dir.FullName, "src")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return null;
    }
}
