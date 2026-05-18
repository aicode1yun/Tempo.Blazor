namespace Tempo.Blazor.DocumentEditor.Models;

/// <summary>Current autosave lifecycle status.</summary>
public enum DocumentAutosaveStatus
{
    /// <summary>The saved provider snapshot matches the editor snapshot.</summary>
    Synchronized,

    /// <summary>Local changes are waiting for the debounce interval before saving.</summary>
    Waiting,

    /// <summary>A save request is currently in flight.</summary>
    Saving,

    /// <summary>The latest save attempt failed.</summary>
    Error
}

/// <summary>Immutable snapshot of the document autosave state machine.</summary>
public sealed record DocumentAutosaveState
{
    /// <summary>Current autosave lifecycle status.</summary>
    public DocumentAutosaveStatus Status { get; init; } = DocumentAutosaveStatus.Synchronized;

    /// <summary>Whether another save should run immediately after the current save completes.</summary>
    public bool HasPendingImmediateSave { get; init; }

    /// <summary>Whether the last save error can be retried by the user or retry policy.</summary>
    public bool CanRetry { get; init; }

    /// <summary>Last save error message, if any.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Number of save attempts since the last synchronized state.</summary>
    public int Attempt { get; init; }
}
