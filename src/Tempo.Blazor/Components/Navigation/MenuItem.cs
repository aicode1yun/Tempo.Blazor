namespace Tempo.Blazor.Components.Navigation;

/// <summary>Represents an item in a menu.</summary>
public sealed class MenuItem
{
    /// <summary>The display text of the item.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>The icon name for the item.</summary>
    public string? Icon { get; set; }

    /// <summary>The navigation URL. If provided, the item renders as a link.</summary>
    public string? Href { get; set; }

    /// <summary>Whether the item is disabled.</summary>
    public bool Disabled { get; set; }

    /// <summary>Whether this item is a separator.</summary>
    public bool IsSeparator { get; set; }

    /// <summary>Child menu items for nested menus.</summary>
    public IReadOnlyList<MenuItem>? Children { get; set; }
}

/// <summary>Defines the orientation of a menu.</summary>
public enum MenuOrientation
{
    /// <summary>Horizontal menu bar.</summary>
    Horizontal,

    /// <summary>Vertical menu list.</summary>
    Vertical
}
