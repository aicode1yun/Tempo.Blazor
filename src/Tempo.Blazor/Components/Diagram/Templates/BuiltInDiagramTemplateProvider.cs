using System.Text.Json;
using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Serialization;
using Tempo.Blazor.Components.Diagram.Services;

namespace Tempo.Blazor.Components.Diagram.Templates;

/// <summary>
/// Built-in provider that loads diagram templates from embedded resources
/// (compiled from <c>wwwroot/diagram-templates/*.json</c>).
/// </summary>
public sealed class BuiltInDiagramTemplateProvider : IDiagramTemplateProvider
{
    private static readonly System.Reflection.Assembly _assembly = typeof(BuiltInDiagramTemplateProvider).Assembly;
    private List<DiagramTemplateCategory>? _categories;

    /// <inheritdoc />
    public int Priority => 0;

    /// <inheritdoc />
    public async Task<IEnumerable<DiagramTemplateCategory>> GetTemplateCategoriesAsync()
    {
        if (_categories is not null)
            return _categories;

        _categories = await LoadCategoriesAsync();
        return _categories;
    }

    private async Task<List<DiagramTemplateCategory>> LoadCategoriesAsync()
    {
        try
        {
            var catalogJson = await ReadResourceAsync("diagram-templates/index.json");
            if (string.IsNullOrWhiteSpace(catalogJson))
                return [];

            using var doc = JsonDocument.Parse(catalogJson);
            if (!doc.RootElement.TryGetProperty("templates", out var templatesEl) || templatesEl.ValueKind != JsonValueKind.Array)
                return [];

            var templates = new List<DiagramTemplate>();
            foreach (var el in templatesEl.EnumerateArray())
            {
                var id = GetString(el, "id");
                var name = GetString(el, "name");
                var category = GetString(el, "category");
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
                    continue;

                var template = new DiagramTemplate
                {
                    Id = id,
                    Name = name,
                    Category = category,
                    Tags = GetStringArray(el, "tags"),
                    ThumbnailUrl = GetString(el, "thumbnailUrl")
                };

                var documentJsonPath = GetString(el, "documentJson");
                if (!string.IsNullOrWhiteSpace(documentJsonPath))
                {
                    try
                    {
                        template.DocumentJson = await ReadResourceAsync(documentJsonPath.TrimStart('/'));
                    }
                    catch
                    {
                        template.DocumentJson = string.Empty;
                    }
                }

                try
                {
                    DiagramDocument? thumbDoc = null;
                    if (!string.IsNullOrWhiteSpace(template.DocumentJson))
                        DiagramSerializer.TryDeserialize(template.DocumentJson, out thumbDoc);

                    thumbDoc ??= new DiagramDocument();
                    thumbDoc.EnsurePages();
                    var svg = DiagramThumbnailSvgGenerator.Generate(thumbDoc);
                    template.ThumbnailUrl = "data:image/svg+xml;base64," + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(svg));
                }
                catch
                {
                    template.ThumbnailUrl = null;
                }

                templates.Add(template);
            }

            return templates
                .GroupBy(t => t.Category)
                .Select(g => new DiagramTemplateCategory
                {
                    Name = g.Key,
                    Templates = g.OrderBy(t => t.Name).ToList()
                })
                .OrderBy(c => c.Name)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    // Converts "diagram-templates/index.json" → "Tempo.Blazor.wwwroot.diagram_templates.index.json"
    // MSBuild replaces hyphens with underscores in directory segments of embedded resource names,
    // but leaves them in file names.
    private static async Task<string> ReadResourceAsync(string relativePath)
    {
        var segments = relativePath.Replace('\\', '/').Split('/');
        var normalized = string.Join(".", segments.Select((s, i) => i == segments.Length - 1 ? s : s.Replace('-', '_')));
        var resourceName = "Tempo.Blazor.wwwroot." + normalized;
        using var stream = _assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
            return string.Empty;

        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    private static string GetString(JsonElement el, string property)
    {
        if (el.TryGetProperty(property, out var prop) && prop.ValueKind == JsonValueKind.String)
            return prop.GetString() ?? "";
        return "";
    }

    private static string[] GetStringArray(JsonElement el, string property)
    {
        if (!el.TryGetProperty(property, out var prop) || prop.ValueKind != JsonValueKind.Array)
            return [];
        return prop.EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.String)
            .Select(x => x.GetString() ?? "")
            .Where(s => !string.IsNullOrEmpty(s))
            .ToArray();
    }
}
