namespace Tempo.Blazor.Components.Navigation;

/// <summary>Represents an item in the bottom navigation bar.</summary>
public sealed class BottomNavItem
{
    /// <summary>The display text of the item.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>The icon name for the item.</summary>
    public string? Icon { get; set; }

    /// <summary>The navigation URL. If provided, the item renders as a link.</summary>
    public string? Href { get; set; }

    /// <summary>Whether the item is disabled.</summary>
    public bool Disabled { get; set; }
}
