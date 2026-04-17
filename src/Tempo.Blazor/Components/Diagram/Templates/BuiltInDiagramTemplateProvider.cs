using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Serialization;
using Tempo.Blazor.Components.Diagram.Services;

namespace Tempo.Blazor.Components.Diagram.Templates;

/// <summary>
/// Built-in provider that loads diagram templates from the embedded
/// <c>_content/Tempo.Blazor/diagram-templates/index.json</c> catalog.
/// </summary>
public sealed class BuiltInDiagramTemplateProvider : IDiagramTemplateProvider
{
    private readonly HttpClient _http;
    private List<DiagramTemplateCategory>? _categories;

    /// <summary>
    /// Creates a new instance of <see cref="BuiltInDiagramTemplateProvider"/>.
    /// </summary>
    public BuiltInDiagramTemplateProvider(NavigationManager navigation, IHttpClientFactory httpClientFactory)
    {
        var baseUri = navigation.BaseUri.TrimEnd('/');
        _http = httpClientFactory.CreateClient();
        _http.BaseAddress = new Uri(baseUri);
    }

    /// <inheritdoc />
    public int Priority => 0;

    /// <inheritdoc />
    public IEnumerable<DiagramTemplateCategory> GetTemplateCategories()
    {
        if (_categories is not null)
            return _categories;

        _categories = LoadCategoriesAsync().GetAwaiter().GetResult();
        return _categories;
    }

    private async Task<List<DiagramTemplateCategory>> LoadCategoriesAsync()
    {
        try
        {
            var catalogJson = await _http.GetStringAsync("/_content/Tempo.Blazor/diagram-templates/index.json");
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
                        template.DocumentJson = await _http.GetStringAsync($"/_content/Tempo.Blazor/{documentJsonPath.TrimStart('/')}");
                    }
                    catch
                    {
                        template.DocumentJson = string.Empty;
                    }
                }

                // Generate SVG thumbnail automatically for built-in templates
                try
                {
                    DiagramDocument? thumbDoc = null;
                    if (!string.IsNullOrWhiteSpace(template.DocumentJson))
                    {
                        DiagramSerializer.TryDeserialize(template.DocumentJson, out thumbDoc);
                    }
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
