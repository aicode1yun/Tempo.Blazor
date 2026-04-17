using System.Text.Json.Serialization;
using Tempo.Blazor.Components.Diagram.Serialization;

namespace Tempo.Blazor.Components.Diagram.Models;

/// <summary>Root document of a diagram. Serializes to/from JSON for persistence and AI-friendly editing.</summary>
[JsonConverter(typeof(DiagramDocumentJsonConverter))]
public sealed class DiagramDocument
{
    /// <summary>Schema version. Bumped on breaking changes to enable migration.</summary>
    public string Version { get; set; } = "2.0";

    /// <summary>Unique document identifier.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Human-readable title shown in the editor toolbar.</summary>
    public string Title { get; set; } = "Untitled diagram";

    /// <summary>All pages in the diagram. The first page is used by default.</summary>
    public List<DiagramPage> Pages { get; set; } = [];

    /// <summary>Index of the page currently being edited.</summary>
    public int ActivePageIndex { get; set; }

    /// <summary>UTC timestamp of document creation.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp of last modification.</summary>
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Returns the currently active page, ensuring at least one page exists.</summary>
    [JsonIgnore]
    public DiagramPage ActivePage
    {
        get
        {
            EnsurePages();
            return Pages[ActivePageIndex];
        }
    }

    /// <summary>Proxy to <see cref="ActivePage"/>.<see cref="DiagramPage.Width"/>.</summary>
    [JsonIgnore]
    public double Width
    {
        get => ActivePage.Width;
        set => ActivePage.Width = value;
    }

    /// <summary>Proxy to <see cref="ActivePage"/>.<see cref="DiagramPage.Height"/>.</summary>
    [JsonIgnore]
    public double Height
    {
        get => ActivePage.Height;
        set => ActivePage.Height = value;
    }

    /// <summary>Proxy to <see cref="ActivePage"/>.<see cref="DiagramPage.Nodes"/>.</summary>
    [JsonIgnore]
    public List<DiagramNode> Nodes
    {
        get => ActivePage.Nodes;
        set => ActivePage.Nodes = value;
    }

    /// <summary>Proxy to <see cref="ActivePage"/>.<see cref="DiagramPage.Edges"/>.</summary>
    [JsonIgnore]
    public List<DiagramEdge> Edges
    {
        get => ActivePage.Edges;
        set => ActivePage.Edges = value;
    }

    /// <summary>Proxy to <see cref="ActivePage"/>.<see cref="DiagramPage.Layers"/>.</summary>
    [JsonIgnore]
    public List<DiagramLayer> Layers
    {
        get => ActivePage.Layers;
        set => ActivePage.Layers = value;
    }

    /// <summary>Guarantees that <see cref="Pages"/> contains at least one page.</summary>
    public void EnsurePages()
    {
        if (Pages.Count == 0)
        {
            Pages.Add(new DiagramPage());
            ActivePageIndex = 0;
        }
        if (ActivePageIndex < 0 || ActivePageIndex >= Pages.Count)
        {
            ActivePageIndex = 0;
        }
    }
}
