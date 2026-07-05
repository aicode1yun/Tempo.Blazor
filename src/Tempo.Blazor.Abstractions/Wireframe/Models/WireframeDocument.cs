using System.Text.Json.Serialization;

namespace Tempo.Blazor.Components.Wireframe.Models;

/// <summary>Root document of a wireframe. Serializes to/from JSON for AI-friendly editing.</summary>
public sealed class WireframeDocument
{
    /// <summary>Schema version. Bumped on breaking changes to enable migration.</summary>
    public string Version { get; set; } = "2.1";

    /// <summary>Human-readable title shown in the editor toolbar.</summary>
    public string Title { get; set; } = "Untitled wireframe";

    /// <summary>All pages in this document.</summary>
    public List<WireframePage> Pages { get; set; } = [];

    /// <summary>Id of the currently active page.</summary>
    public string? ActivePageId { get; set; }

    /// <summary>UTC timestamp of document creation.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp of last modification.</summary>
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Namespaced stencil pack ids this document renders against, e.g. <c>tempo</c> or <c>app:{id}</c>.</summary>
    [JsonPropertyName("targetPacks")]
    public List<string> TargetPackIds { get; set; } = [];

    /// <summary>Optional theme name selecting a token override set within the target packs.</summary>
    public string? TargetTheme { get; set; }

    // ── Convenience accessors (delegate to the active page) ───────────────────

    /// <summary>
    /// Returns the active page, or the first page if <see cref="ActivePageId"/> is not set.
    /// Returns <c>null</c> when there are no pages.
    /// </summary>
    [JsonIgnore]
    public WireframePage? ActivePage =>
        Pages.FirstOrDefault(p => p.Id == ActivePageId) ?? Pages.FirstOrDefault();

    /// <summary>Elements on the active page. Auto-creates a default page when empty.</summary>
    [JsonIgnore]
    public List<WireframeElement> Elements
    {
        get { EnsureActivePage(); return ActivePage!.Elements; }
    }

    /// <summary>Connectors on the active page. Auto-creates a default page when empty.</summary>
    [JsonIgnore]
    public List<WireframeConnector> Connectors
    {
        get { EnsureActivePage(); return ActivePage!.Connectors; }
    }

    /// <summary>Canvas width of the active page. Falls back to 1280.</summary>
    [JsonIgnore]
    public double Width
    {
        get => ActivePage?.Width ?? 1280;
        set { EnsureActivePage(); if (ActivePage is not null) ActivePage.Width = value; }
    }

    /// <summary>Canvas height of the active page. Falls back to 800.</summary>
    [JsonIgnore]
    public double Height
    {
        get => ActivePage?.Height ?? 800;
        set { EnsureActivePage(); if (ActivePage is not null) ActivePage.Height = value; }
    }

    /// <summary>Layers on the active page. Auto-creates a default page + layer when empty.</summary>
    [JsonIgnore]
    public List<WireframeLayer> Layers
    {
        get { EnsureActivePage(); ActivePage!.EnsureDefaultLayer(); return ActivePage.Layers; }
    }

    /// <summary>Active layer id on the active page. Auto-creates a default page + layer when empty.</summary>
    [JsonIgnore]
    public string? ActiveLayerId
    {
        get { EnsureActivePage(); ActivePage!.EnsureDefaultLayer(); return ActivePage.ActiveLayerId; }
        set { EnsureActivePage(); ActivePage!.EnsureDefaultLayer(); ActivePage.ActiveLayerId = value; }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Ensures the document has at least one page and a valid <see cref="ActivePageId"/>.
    /// Creates a default page when the list is empty.
    /// </summary>
    public void EnsureActivePage()
    {
        if (Pages.Count == 0)
        {
            var page = new WireframePage { Name = "Page 1" };
            page.EnsureDefaultLayer();
            Pages.Add(page);
            ActivePageId = page.Id;
        }
        else if (string.IsNullOrEmpty(ActivePageId) || Pages.All(p => p.Id != ActivePageId))
        {
            ActivePageId = Pages[0].Id;
        }
    }
}
