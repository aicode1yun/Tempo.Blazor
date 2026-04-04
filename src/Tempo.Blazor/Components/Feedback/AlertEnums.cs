namespace Tempo.Blazor.Components.Feedback;

/// <summary>
/// Severity level for the alert component.
/// </summary>
public enum AlertSeverity
{
    /// <summary>Informational message (blue).</summary>
    Info,

    /// <summary>Success message (green).</summary>
    Success,

    /// <summary>Warning message (yellow/amber).</summary>
    Warning,

    /// <summary>Error message (red).</summary>
    Error
}

/// <summary>
/// Visual variant for the alert component.
/// </summary>
public enum AlertVariant
{
    /// <summary>Subtle background tint (default).</summary>
    Soft,

    /// <summary>Fully colored background with white text.</summary>
    Filled,

    /// <summary>Bordered with transparent background.</summary>
    Outlined
}
