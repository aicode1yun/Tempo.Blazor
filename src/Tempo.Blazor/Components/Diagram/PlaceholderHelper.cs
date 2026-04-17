using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram;

/// <summary>Replaces diagram-specific placeholders in node text.</summary>
public static class PlaceholderHelper
{
    /// <summary>
    /// Supported placeholders:
    /// <list type="bullet">
    /// <item><description>%page% — current page name</description></item>
    /// <item><description>%pagenumber% — current page number (1-based)</description></item>
    /// <item><description>%totalpages% — total number of pages</description></item>
    /// <item><description>%date% — current UTC date in ISO format (yyyy-MM-dd)</description></item>
    /// </list>
    /// </summary>
    public static string ReplacePlaceholders(string? text, DiagramPage page, DiagramDocument doc)
    {
        if (string.IsNullOrEmpty(text)) return text ?? "";

        var pageIndex = doc.Pages.FindIndex(p => p.Id == page.Id);
        if (pageIndex < 0) pageIndex = 0;

        return text
            .Replace("%page%", page.Name)
            .Replace("%pagenumber%", (pageIndex + 1).ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Replace("%totalpages%", doc.Pages.Count.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Replace("%date%", DateTime.UtcNow.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
    }
}
