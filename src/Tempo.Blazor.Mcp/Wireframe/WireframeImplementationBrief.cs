using Tempo.Blazor.Components.Wireframe.Models;

namespace Tempo.Blazor.Mcp.Wireframe;

/// <summary>One placed component in an implementation-brief region.</summary>
public sealed record BriefElement(string Id, string Type, double X, double Y, double W, double H);

/// <summary>A layout region (header / sidebar / content / footer) derived from element geometry.</summary>
public sealed record BriefRegion(string Kind, IReadOnlyList<BriefElement> Elements);

/// <summary>A component type and how many times it is used.</summary>
public sealed record BriefComponent(string Type, int Count);

/// <summary>A navigation flow between two elements, derived from a connector.</summary>
public sealed record BriefFlow(string FromId, string? FromType, string ToId, string? ToType, string? Label);

/// <summary>One page rendered as an implementation section.</summary>
public sealed record BriefPage(
    string Name, double Width, double Height,
    IReadOnlyList<BriefRegion> Regions,
    IReadOnlyList<BriefComponent> Components,
    IReadOnlyList<BriefFlow> Flows);

/// <summary>The full implementation brief for a wireframe document.</summary>
public sealed record WireframeBrief(
    string Title,
    IReadOnlyList<BriefPage> Pages,
    IReadOnlyList<BriefComponent> ComponentsUsed);

/// <summary>
/// Deterministically transforms a wireframe into an implementation-oriented brief: pages become
/// sections, geometry becomes layout regions, connectors become navigation flows, and component
/// usage is summarised — the artifact a plan/use-case consumes to build the real page.
/// </summary>
public static class WireframeImplementationBrief
{
    public static WireframeBrief Build(WireframeDocument document)
    {
        var pages = document.Pages.Select(BuildPage).ToList();

        var componentsUsed = document.Pages
            .SelectMany(p => p.Elements)
            .GroupBy(e => e.Type, StringComparer.Ordinal)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new BriefComponent(g.Key, g.Count()))
            .ToList();

        return new WireframeBrief(document.Title, pages, componentsUsed);
    }

    private static BriefPage BuildPage(WireframePage page)
    {
        var byRegion = page.Elements
            .GroupBy(e => Classify(e, page.Width, page.Height));

        // Fixed, readable region order.
        var order = new[] { "header", "sidebar", "content", "footer" };
        var regions = order
            .Select(kind => (kind, els: byRegion.FirstOrDefault(g => g.Key == kind)))
            .Where(x => x.els is not null)
            .Select(x => new BriefRegion(x.kind,
                x.els!.OrderBy(e => e.Y).ThenBy(e => e.X)
                    .Select(e => new BriefElement(e.Id, e.Type, e.X, e.Y, e.W, e.H)).ToList()))
            .ToList();

        var components = page.Elements
            .GroupBy(e => e.Type, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new BriefComponent(g.Key, g.Count()))
            .ToList();

        var typeById = page.Elements.ToDictionary(e => e.Id, e => e.Type, StringComparer.Ordinal);
        var flows = page.Connectors.Select(c => new BriefFlow(
            c.FromId, typeById.GetValueOrDefault(c.FromId),
            c.ToId, typeById.GetValueOrDefault(c.ToId),
            c.Label)).ToList();

        return new BriefPage(page.Name, page.Width, page.Height, regions, components, flows);
    }

    /// <summary>Classifies an element into a layout region from its position on the page.</summary>
    public static string Classify(WireframeElement el, double pageWidth, double pageHeight)
    {
        if (el.Y + el.H <= pageHeight * 0.15)
        {
            return "header";
        }
        if (el.Y >= pageHeight * 0.85)
        {
            return "footer";
        }
        if (el.X + el.W <= pageWidth * 0.2)
        {
            return "sidebar";
        }
        return "content";
    }
}
