namespace Tempo.Blazor.Components.Layout;

/// <summary>Defines the orientation of a stack layout.</summary>
public enum StackOrientation
{
    /// <summary>Items are arranged horizontally.</summary>
    Horizontal,

    /// <summary>Items are arranged vertically.</summary>
    Vertical
}

/// <summary>Defines how items are aligned along the cross axis.</summary>
public enum AlignItems
{
    /// <summary>Stretch to fill the container.</summary>
    Stretch,

    /// <summary>Align to the start of the cross axis.</summary>
    Start,

    /// <summary>Align to the center of the cross axis.</summary>
    Center,

    /// <summary>Align to the end of the cross axis.</summary>
    End,

    /// <summary>Baseline alignment.</summary>
    Baseline
}

/// <summary>Defines how items are distributed along the main axis.</summary>
public enum JustifyContent
{
    /// <summary>Items are packed toward the start.</summary>
    Start,

    /// <summary>Items are packed toward the center.</summary>
    Center,

    /// <summary>Items are packed toward the end.</summary>
    End,

    /// <summary>Items are evenly distributed with equal space around them.</summary>
    SpaceAround,

    /// <summary>Items are evenly distributed with equal space between them.</summary>
    SpaceBetween,

    /// <summary>Items are evenly distributed with equal space including edges.</summary>
    SpaceEvenly
}
