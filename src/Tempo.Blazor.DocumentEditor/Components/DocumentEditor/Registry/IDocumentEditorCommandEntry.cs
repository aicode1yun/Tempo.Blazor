namespace Tempo.Blazor.Components.DocumentEditor.Registry;

/// <summary>A named command registered in the <see cref="DocumentEditorCommandRegistry"/>.</summary>
public interface IDocumentEditorCommandEntry
{
    /// <summary>Registry name of this command (e.g. "bold", "save"). Must be unique within the registry.</summary>
    string Name { get; }

    /// <summary>Whether executing this command modifies document data.
    /// Commands with <c>AffectsData=false</c> (e.g. zoom, view toggles) remain enabled in read-only mode.</summary>
    bool AffectsData { get; }

    /// <summary>Localization key for the primary command label.</summary>
    string? DescriptionKey { get; }

    /// <summary>Localization key for the command tooltip.</summary>
    string? TooltipKey { get; }

    /// <summary>Category used by command palette and More menu grouping.</summary>
    string? Category { get; }

    /// <summary>Default keyboard shortcut displayed for this command.</summary>
    string? DefaultShortcut { get; }

    /// <summary>Icon name used by toolbar and command palette surfaces.</summary>
    string? Icon { get; }

    /// <summary>Localization key for the disabled reason when the command is unavailable.</summary>
    string? DisabledReasonKey { get; }

    /// <summary>Returns whether this command should be enabled given the current editor state.
    /// The registry applies forced-disable and read-only logic on top of this value.</summary>
    bool ComputeEnabled(DocumentEditorCommandContext context);

    /// <summary>Returns whether this command should be shown in command-driven surfaces.</summary>
    bool ComputeVisible(DocumentEditorCommandContext context);

    /// <summary>Returns an optional formatting value for toggle commands: "active", "inactive", "mixed".
    /// Return <c>null</c> for action-only commands (save, undo, etc.).</summary>
    string? ComputeValue(DocumentEditorCommandContext context);

    /// <summary>Executes the command.</summary>
    Task ExecuteAsync(DocumentEditorCommandContext context, object? payload = null);
}
