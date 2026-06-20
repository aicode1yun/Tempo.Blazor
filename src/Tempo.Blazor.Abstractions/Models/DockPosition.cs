namespace Tempo.Blazor.Abstractions.Models;

/// <summary>Defines where a dock pane is anchored inside a dock manager.</summary>
public enum DockPosition
{
    /// <summary>Docked to the left side.</summary>
    Left,

    /// <summary>Docked to the top side.</summary>
    Top,

    /// <summary>Docked to the right side.</summary>
    Right,

    /// <summary>Docked to the bottom side.</summary>
    Bottom,

    /// <summary>Occupies the central document area (tabbed by default).</summary>
    Center,

    /// <summary>Floating window overlay.</summary>
    Floating
}
