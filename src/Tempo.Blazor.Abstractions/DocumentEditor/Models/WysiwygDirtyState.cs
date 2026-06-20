namespace Tempo.Blazor.DocumentEditor.Models;

/// <summary>Describes JS-owned dirty/save state for a WYSIWYG editor instance.</summary>
public sealed class WysiwygDirtyState
{
    /// <summary>Whether the JS runtime contains unsaved changes.</summary>
    public bool IsDirty { get; set; }

    /// <summary>Monotonic runtime dirty epoch incremented when content changes.</summary>
    public int DirtyEpoch { get; set; }

    /// <summary>Dirty epoch that was last acknowledged as saved.</summary>
    public int SavedEpoch { get; set; }

    /// <summary>Undo epoch at the time this state was produced.</summary>
    public int UndoEpoch { get; set; }

    /// <summary>Reason for the latest dirty transition.</summary>
    public string? Reason { get; set; }

    /// <summary>Provider marker associated with the latest successful save.</summary>
    public string? LastSavedMarker { get; set; }

    /// <summary>Timestamp reported by the JS runtime for the latest successful save.</summary>
    public DateTimeOffset? LastSavedAt { get; set; }
}
