namespace Tempo.Blazor.Components.DocumentEditor.Registry;

/// <summary>Immutable snapshot of a named editor command's current state.</summary>
public sealed class DocumentEditorCommandState
{
    /// <summary>Registry name of this command (e.g. "bold", "save").</summary>
    public required string Name { get; init; }

    /// <summary>Whether this command can currently be executed.</summary>
    public bool IsEnabled { get; init; }

    /// <summary>Optional formatting value for toggle commands: "active", "inactive", "mixed".</summary>
    public string? Value { get; init; }

    /// <summary>Whether executing this command modifies document data (and is therefore blocked by read-only mode).</summary>
    public bool AffectsData { get; init; }

    /// <summary>Whether this command should be shown in command-driven surfaces.</summary>
    public bool IsVisible { get; init; } = true;

    /// <summary>Localization key for the primary command label.</summary>
    public string? DescriptionKey { get; init; }

    /// <summary>Localization key for the command tooltip.</summary>
    public string? TooltipKey { get; init; }

    /// <summary>Category used by command palette and More menu grouping.</summary>
    public string? Category { get; init; }

    /// <summary>Default keyboard shortcut displayed for this command.</summary>
    public string? DefaultShortcut { get; init; }

    /// <summary>Icon name used by toolbar and command palette surfaces.</summary>
    public string? Icon { get; init; }

    /// <summary>Human-readable reason the command is currently disabled, or <c>null</c> when enabled.</summary>
    public string? DisabledReason { get; init; }

    /// <summary>Localization key for the disabled reason, or <c>null</c> when enabled.</summary>
    public string? DisabledReasonKey { get; init; }
}
