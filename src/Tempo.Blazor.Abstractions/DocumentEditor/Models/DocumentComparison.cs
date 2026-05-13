namespace Tempo.Blazor.DocumentEditor.Models;

/// <summary>Source kind used by document comparison requests.</summary>
public enum DocumentCompareSourceKind
{
    /// <summary>The current editor document snapshot.</summary>
    Current,

    /// <summary>A document loaded by id through the host provider.</summary>
    DocumentId,

    /// <summary>A raw JSON snapshot or imported upload snapshot.</summary>
    JsonSnapshot
}

/// <summary>Single source in a document comparison request.</summary>
public sealed class DocumentCompareSource
{
    /// <summary>Source kind.</summary>
    public DocumentCompareSourceKind Kind { get; set; } = DocumentCompareSourceKind.Current;

    /// <summary>Optional document id for provider-loaded sources.</summary>
    public string? DocumentId { get; set; }

    /// <summary>Optional in-memory document snapshot.</summary>
    public DocumentEditorDocument? Document { get; set; }

    /// <summary>Optional raw JSON document snapshot.</summary>
    public string? JsonSnapshot { get; set; }

    /// <summary>Optional display label or upload file name.</summary>
    public string? Label { get; set; }
}

/// <summary>Request passed to a host document comparison provider.</summary>
public sealed class DocumentCompareRequest
{
    /// <summary>Current document id.</summary>
    public string DocumentId { get; set; } = string.Empty;

    /// <summary>Base side of the comparison.</summary>
    public DocumentCompareSource BaseSource { get; set; } = new();

    /// <summary>Compared side of the comparison.</summary>
    public DocumentCompareSource CompareSource { get; set; } = new();

    /// <summary>Current editor document snapshot.</summary>
    public DocumentEditorDocument? CurrentDocument { get; set; }

    /// <summary>Author requesting the comparison.</summary>
    public DocumentEditorAuthor? Author { get; set; }
}

/// <summary>Kind of a block-level document comparison change.</summary>
public enum DocumentCompareChangeKind
{
    /// <summary>Block exists on both sides but its text changed.</summary>
    Changed,

    /// <summary>Block exists only on the compared side.</summary>
    Added,

    /// <summary>Block exists only on the base side.</summary>
    Removed
}

/// <summary>Summary counters for a document comparison result.</summary>
public sealed class DocumentCompareSummary
{
    /// <summary>Number of added blocks.</summary>
    public int AddedBlocks { get; set; }

    /// <summary>Number of removed blocks.</summary>
    public int RemovedBlocks { get; set; }

    /// <summary>Number of changed blocks.</summary>
    public int ChangedBlocks { get; set; }

    /// <summary>Whether the comparison found any changes.</summary>
    public bool HasChanges => AddedBlocks > 0 || RemovedBlocks > 0 || ChangedBlocks > 0;
}

/// <summary>Single block-level comparison item.</summary>
public sealed class DocumentCompareBlockChange
{
    /// <summary>Change kind.</summary>
    public DocumentCompareChangeKind Kind { get; set; }

    /// <summary>Block id, when available.</summary>
    public string? BlockId { get; set; }

    /// <summary>Base-side text.</summary>
    public string OldText { get; set; } = string.Empty;

    /// <summary>Compared-side text.</summary>
    public string NewText { get; set; } = string.Empty;

    /// <summary>Word-level diff for changed blocks.</summary>
    public DocumentTextDiffResult TextDiff { get; set; } = new();
}

/// <summary>Document comparison result.</summary>
public sealed class DocumentCompareResult
{
    /// <summary>Whether the provider or local service completed successfully.</summary>
    public bool Success { get; set; } = true;

    /// <summary>Optional error message when comparison failed.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Base document snapshot used for rendering the diff.</summary>
    public DocumentEditorDocument? BaseDocument { get; set; }

    /// <summary>Compared document snapshot used for rendering the diff.</summary>
    public DocumentEditorDocument? CompareDocument { get; set; }

    /// <summary>Overall text diff.</summary>
    public DocumentTextDiffResult TextDiff { get; set; } = new();

    /// <summary>Block-level changes.</summary>
    public List<DocumentCompareBlockChange> Changes { get; set; } = [];

    /// <summary>Summary counters.</summary>
    public DocumentCompareSummary Summary { get; set; } = new();
}
