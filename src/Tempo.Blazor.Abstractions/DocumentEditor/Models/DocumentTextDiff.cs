namespace Tempo.Blazor.DocumentEditor.Models;

/// <summary>Kind of a text diff segment.</summary>
public enum DocumentTextDiffSegmentKind
{
    /// <summary>Text is present in both inputs.</summary>
    Unchanged,

    /// <summary>Text was added in the new input.</summary>
    Added,

    /// <summary>Text was removed from the old input.</summary>
    Removed
}

/// <summary>Single word-level diff segment.</summary>
public sealed class DocumentTextDiffSegment
{
    /// <summary>Segment kind.</summary>
    public DocumentTextDiffSegmentKind Kind { get; set; }

    /// <summary>Segment text.</summary>
    public string Text { get; set; } = string.Empty;
}

/// <summary>Result of a word-level text diff.</summary>
public sealed class DocumentTextDiffResult
{
    /// <summary>Ordered diff segments.</summary>
    public List<DocumentTextDiffSegment> Segments { get; set; } = [];

    /// <summary>Whether the compared text is identical.</summary>
    public bool HasChanges => Segments.Any(segment => segment.Kind != DocumentTextDiffSegmentKind.Unchanged);
}
