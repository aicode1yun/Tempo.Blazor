namespace Tempo.Blazor.Components.Feedback;

/// <summary>Size variants for the progress bar.</summary>
public enum ProgressBarSize
{
    /// <summary>Small progress bar height.</summary>
    Sm,

    /// <summary>Medium progress bar height (default).</summary>
    Md,

    /// <summary>Large progress bar height.</summary>
    Lg
}

/// <summary>Color variant for the progress bar.</summary>
public enum ProgressBarVariant
{
    /// <summary>Default color (primary).</summary>
    Default,

    /// <summary>Success color (green).</summary>
    Success,

    /// <summary>Warning color (yellow/amber).</summary>
    Warning,

    /// <summary>Error color (red).</summary>
    Error,

    /// <summary>Gradient color effect.</summary>
    Gradient
}

/// <summary>A single segment in a multi-segment progress bar.</summary>
public sealed record ProgressSegment(double Value, string Color, string? Label = null);
