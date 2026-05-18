namespace Tempo.Blazor.Components.DocumentEditor.Registry;

/// <summary>Rendering kind for a declarative document toolbar item.</summary>
public enum DocumentToolbarItemKind
{
    /// <summary>Simple command button.</summary>
    Button,

    /// <summary>Toggle command button.</summary>
    Toggle,

    /// <summary>Backward-compatible alias for <see cref="Toggle"/>.</summary>
    ToggleButton = Toggle,

    /// <summary>Select/dropdown command input.</summary>
    Select,

    /// <summary>Backward-compatible alias for <see cref="Select"/>.</summary>
    DropdownSelect = Select,

    /// <summary>Color picker command input.</summary>
    ColorPicker,

    /// <summary>Split button with a primary action and secondary menu.</summary>
    SplitButton,

    /// <summary>Menu button.</summary>
    Menu,

    /// <summary>Grid picker command input, such as a table size picker.</summary>
    GridPicker,

    /// <summary>Visual separator.</summary>
    Separator
}
