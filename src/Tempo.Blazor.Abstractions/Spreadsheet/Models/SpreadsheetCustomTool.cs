namespace Tempo.Blazor.Components.Spreadsheet.Models;

/// <summary>Represents a custom toolbar tool that can be injected into the spreadsheet toolbar.</summary>
public sealed class SpreadsheetCustomTool
{
    /// <summary>Unique identifier for the tool.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Display icon name (registered in IconRegistry).</summary>
    public string? IconName { get; set; }

    /// <summary>Tooltip / aria-label text (localizable key).</summary>
    public string? Title { get; set; }

    /// <summary>Optional CSS class applied to the tool button.</summary>
    public string? CssClass { get; set; }

    /// <summary>Tab group where the tool should appear: Home, Insert, View, or File.</summary>
    public string Tab { get; set; } = "Home";

    /// <summary>Order within the tab (lower = first).</summary>
    public int Order { get; set; }
}
