namespace Tempo.Blazor.Abstractions.Models;

/// <summary>Represents a single pane inside a <see cref="DockManager"/> layout.</summary>
public class DockPane
{
    /// <summary>Unique identifier of the pane.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Display title shown in the pane header.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Optional icon name (registered in the icon system).</summary>
    public string? Icon { get; set; }

    /// <summary>Whether the pane can be floated into an overlay window.</summary>
    public bool CanFloat { get; set; } = true;

    /// <summary>Whether the pane shows a close button.</summary>
    public bool CanClose { get; set; } = true;

    /// <summary>Whether the pane is currently visible.</summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>Whether the pane is the active tab in its group.</summary>
    public bool IsActive { get; set; }

    /// <summary>Current docking position.</summary>
    public DockPosition Position { get; set; } = DockPosition.Center;

    /// <summary>Desired width in pixels when docked to left/right or floating.</summary>
    public double? Width { get; set; }

    /// <summary>Desired height in pixels when docked to top/bottom or floating.</summary>
    public double? Height { get; set; }

    /// <summary>Display order within the same docking position.</summary>
    public int Order { get; set; }

    /// <summary>Horizontal position for floating panes (CSS pixels).</summary>
    public double FloatX { get; set; } = 100;

    /// <summary>Vertical position for floating panes (CSS pixels).</summary>
    public double FloatY { get; set; } = 100;
}
