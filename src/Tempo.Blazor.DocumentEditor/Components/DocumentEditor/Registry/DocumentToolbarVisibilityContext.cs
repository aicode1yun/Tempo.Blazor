using Tempo.Blazor.Components.DocumentEditor;

namespace Tempo.Blazor.Components.DocumentEditor.Registry;

/// <summary>Runtime context used to decide whether a toolbar item should be visible.</summary>
public sealed record DocumentToolbarVisibilityContext
{
    /// <summary>Optional command registry used to inspect command availability.</summary>
    public DocumentEditorCommandRegistry? CommandRegistry { get; init; }

    /// <summary>Current toolbar mode.</summary>
    public DocumentToolbarMode ToolbarMode { get; init; } = DocumentToolbarMode.Ribbon;

    /// <summary>Whether the editor is currently editing a header or footer region.</summary>
    public bool IsHeaderFooterMode { get; init; }

    /// <summary>Feature names disabled for this toolbar instance.</summary>
    public IReadOnlyCollection<string>? DisabledFeatures { get; init; }

    /// <summary>Returns whether the given feature name is enabled for this context.</summary>
    public bool IsFeatureEnabled(string featureName) =>
        DisabledFeatures is null
        || !DisabledFeatures.Any(disabled => string.Equals(disabled, featureName, StringComparison.OrdinalIgnoreCase));
}
