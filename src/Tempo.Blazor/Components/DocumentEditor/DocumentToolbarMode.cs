namespace Tempo.Blazor.Components.DocumentEditor;

/// <summary>Visual density and visibility mode for the document editor toolbar.</summary>
public enum DocumentToolbarMode
{
    /// <summary>Full ribbon with tabs, groups, icons, and labels.</summary>
    Ribbon,

    /// <summary>Dense ribbon optimized for smaller work areas.</summary>
    Compact,

    /// <summary>Hides the ribbon for focused writing while keeping floating editing surfaces available.</summary>
    DistractionFree
}
