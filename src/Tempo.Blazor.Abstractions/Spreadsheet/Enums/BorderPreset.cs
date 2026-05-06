namespace Tempo.Blazor.Components.Spreadsheet.Enums;

/// <summary>
/// Predefined border patterns that can be applied to a cell selection.
/// </summary>
public enum BorderPreset
{
    /// <summary>Remove all borders.</summary>
    None,

    /// <summary>Apply thin borders to all four edges of each cell.</summary>
    AllBorders,

    /// <summary>Apply thin border to the outside edges of the selection.</summary>
    OutsideBorders,

    /// <summary>Apply thick border to the outside edges of the selection.</summary>
    ThickBox,

    /// <summary>Apply thin border to the bottom edge only.</summary>
    BottomBorder,

    /// <summary>Apply thick border to the bottom edge only.</summary>
    ThickBottom,

    /// <summary>Apply double border to the bottom edge only.</summary>
    DoubleBottom,

    /// <summary>Apply thin border to the top edge only.</summary>
    TopBorder,

    /// <summary>Apply thin border to the left edge only.</summary>
    LeftBorder,

    /// <summary>Apply thin border to the right edge only.</summary>
    RightBorder,

    /// <summary>Apply thin top border and thick bottom border.</summary>
    TopAndThickBottom,
}
