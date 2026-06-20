namespace Tempo.Blazor.Components.Buttons;

/// <summary>Represents an item in the floating action button speed dial menu.</summary>
public sealed class FabItem
{
    /// <summary>The icon name for the item.</summary>
    public string? Icon { get; set; }

    /// <summary>The label text for the item.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Whether the item is disabled.</summary>
    public bool Disabled { get; set; }
}

/// <summary>Defines the position of the floating action button.</summary>
public enum FabPosition
{
    /// <summary>Bottom right corner.</summary>
    BottomRight,

    /// <summary>Bottom left corner.</summary>
    BottomLeft,

    /// <summary>Top right corner.</summary>
    TopRight,

    /// <summary>Top left corner.</summary>
    TopLeft
}
