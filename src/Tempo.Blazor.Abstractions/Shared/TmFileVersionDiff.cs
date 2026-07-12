namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Nature of a single line in a version diff.</summary>
public enum TmFileVersionDiffKind
{
    /// <summary>Line is present and identical in both versions.</summary>
    Unchanged = 0,

    /// <summary>Line exists only in the newer version.</summary>
    Added = 1,

    /// <summary>Line exists only in the older version.</summary>
    Removed = 2
}

/// <summary>A single line entry in a text diff between two file versions.</summary>
public sealed class TmFileVersionDiffLine
{
    /// <summary>Whether the line was added, removed, or unchanged.</summary>
    public TmFileVersionDiffKind Kind { get; set; }

    /// <summary>Line text (without trailing newline).</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>1-based line number in the older version, when applicable.</summary>
    public int? OldLineNumber { get; set; }

    /// <summary>1-based line number in the newer version, when applicable.</summary>
    public int? NewLineNumber { get; set; }
}

/// <summary>Result of comparing two versions of a file.</summary>
public sealed class TmFileVersionDiff
{
    /// <summary>Identifier of the logical item.</summary>
    public string ItemId { get; set; } = string.Empty;

    /// <summary>Older version being compared.</summary>
    public string FromVersionId { get; set; } = string.Empty;

    /// <summary>Newer version being compared.</summary>
    public string ToVersionId { get; set; } = string.Empty;

    /// <summary>True when <see cref="Lines"/> holds a meaningful line-by-line text diff.</summary>
    public bool IsTextDiff { get; set; }

    /// <summary>Line-by-line diff for text content; empty for binary comparisons.</summary>
    public IReadOnlyList<TmFileVersionDiffLine> Lines { get; set; } = [];

    /// <summary>Size change in bytes (newer minus older).</summary>
    public long SizeDelta { get; set; }

    /// <summary>File name of the older version.</summary>
    public string? FromFileName { get; set; }

    /// <summary>File name of the newer version.</summary>
    public string? ToFileName { get; set; }

    /// <summary>Number of added lines in a text diff.</summary>
    public int AddedLines => Lines.Count(l => l.Kind == TmFileVersionDiffKind.Added);

    /// <summary>Number of removed lines in a text diff.</summary>
    public int RemovedLines => Lines.Count(l => l.Kind == TmFileVersionDiffKind.Removed);
}
