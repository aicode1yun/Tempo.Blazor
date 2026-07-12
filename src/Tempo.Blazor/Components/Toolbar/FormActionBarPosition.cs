namespace Tempo.Blazor.Components.Toolbar;

/// <summary>Placement mode for <see cref="TmFormActionBar"/>.</summary>
public enum FormActionBarPosition
{
    /// <summary>Render in normal document flow.</summary>
    Static,

    /// <summary>Stick to the top of the scroll container.</summary>
    StickyTop,

    /// <summary>Fix to the bottom of the viewport.</summary>
    FloatingBottom
}
