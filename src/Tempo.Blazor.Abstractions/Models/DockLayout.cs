namespace Tempo.Blazor.Abstractions.Models;

/// <summary>Serializable snapshot of a dock-manager layout used for persistence.</summary>
public class DockLayout
{
    /// <summary>Serialized pane states.</summary>
    public List<DockLayoutPane> Panes { get; set; } = [];
}

/// <summary>Serialized state of a single pane inside a <see cref="DockLayout"/>.</summary>
public class DockLayoutPane
{
    /// <summary>Pane identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Docking position.</summary>
    public DockPosition Position { get; set; }

    /// <summary>Whether the pane is visible.</summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>Whether the pane is active in its tab group.</summary>
    public bool IsActive { get; set; }

    /// <summary>Width in pixels.</summary>
    public double? Width { get; set; }

    /// <summary>Height in pixels.</summary>
    public double? Height { get; set; }

    /// <summary>Display order.</summary>
    public int Order { get; set; }

    /// <summary>Horizontal floating position.</summary>
    public double FloatX { get; set; }

    /// <summary>Vertical floating position.</summary>
    public double FloatY { get; set; }
}
