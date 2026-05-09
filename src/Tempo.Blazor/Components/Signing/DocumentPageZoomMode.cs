namespace Tempo.Blazor.Components.Signing;

/// <summary>Determines how a document page viewer chooses its display scale.</summary>
public enum DocumentPageZoomMode
{
    /// <summary>Use the explicitly configured scale.</summary>
    Custom,

    /// <summary>Fit the page width to the available viewport.</summary>
    FitWidth,

    /// <summary>Fit the entire page to the available viewport.</summary>
    FitPage
}
