namespace Tempo.Blazor.Components.Feedback;

/// <summary>Size variants for the modal dialog.</summary>
public enum ModalSize
{
    /// <summary>Small modal width (e.g., 400px).</summary>
    Small,

    /// <summary>Medium modal width (default, e.g., 600px).</summary>
    Medium,

    /// <summary>Large modal width (e.g., 800px).</summary>
    Large,

    /// <summary>Extra large modal width (e.g., 1000px).</summary>
    XLarge,

    /// <summary>Fullscreen modal covering the entire viewport.</summary>
    Fullscreen
}

/// <summary>Position of the modal on the screen.</summary>
public enum ModalPosition
{
    /// <summary>Centered vertically and horizontally (default).</summary>
    Center,

    /// <summary>Positioned at the top of the screen.</summary>
    Top,

    /// <summary>Positioned at the bottom of the screen.</summary>
    Bottom
}

/// <summary>Type of dialog to display.</summary>
public enum DialogType
{
    /// <summary>Simple alert with a message and OK button.</summary>
    Alert,

    /// <summary>Confirmation dialog with OK and Cancel buttons.</summary>
    Confirm,

    /// <summary>Prompt dialog with input field and OK/Cancel buttons.</summary>
    Prompt,

    /// <summary>Custom dialog with fully customizable content.</summary>
    Custom
}

/// <summary>Visual variant for the dialog indicating severity or purpose.</summary>
public enum DialogVariant
{
    /// <summary>Informational dialog (blue).</summary>
    Info,

    /// <summary>Success dialog (green).</summary>
    Success,

    /// <summary>Warning dialog (yellow/amber).</summary>
    Warning,

    /// <summary>Error dialog (red).</summary>
    Error
}
