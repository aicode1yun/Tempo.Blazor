namespace Tempo.Blazor.DocumentEditor.Models;

/// <summary>Threaded document comment.</summary>
public class DocumentComment
{
    /// <summary>Stable comment id.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Comment anchor.</summary>
    public DocumentCommentAnchor Anchor { get; set; } = new();

    /// <summary>Thread entries.</summary>
    public List<DocumentCommentEntry> Entries { get; set; } = [];

    /// <summary>Current comment status.</summary>
    public DocumentCommentStatus Status { get; set; } = DocumentCommentStatus.Open;

    /// <summary>Visibility scope.</summary>
    public DocumentCommentVisibility Visibility { get; set; } = DocumentCommentVisibility.Internal;

    /// <summary>Source format when imported from DOCX, ODT, or another external system.</summary>
    public string? SourceFormat { get; set; }

    /// <summary>External id from the source format.</summary>
    public string? ExternalId { get; set; }

    /// <summary>Timestamp when the thread was resolved.</summary>
    public DateTimeOffset? ResolvedAt { get; set; }

    /// <summary>Author who resolved the thread.</summary>
    public DocumentEditorAuthor? ResolvedBy { get; set; }
}

/// <summary>Single entry in a comment thread.</summary>
public class DocumentCommentEntry
{
    /// <summary>Stable entry id.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Comment author.</summary>
    public DocumentEditorAuthor Author { get; set; } = new();

    /// <summary>Whether the author is external to the host application.</summary>
    public bool IsExternalAuthor { get; set; }

    /// <summary>Entry text.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Rich inline content for comment bodies, including images imported from DOCX comment parts.</summary>
    public List<InlineContent> Inlines { get; set; } = [];

    /// <summary>Creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Last modification timestamp.</summary>
    public DateTimeOffset? ModifiedAt { get; set; }
}

/// <summary>Request used to update an existing comment entry.</summary>
public class DocumentCommentEntryUpdateRequest
{
    /// <summary>Updated entry text.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Author who submitted the update.</summary>
    public DocumentEditorAuthor UpdatedBy { get; set; } = new();
}

/// <summary>Anchor describing the commented document range or object.</summary>
public class DocumentCommentAnchor
{
    /// <summary>Anchor type.</summary>
    public DocumentCommentAnchorType Type { get; set; } = DocumentCommentAnchorType.Block;

    /// <summary>Target block id.</summary>
    public string? BlockId { get; set; }

    /// <summary>Start inline index for text range anchors.</summary>
    public int? StartInlineIndex { get; set; }

    /// <summary>Start character offset for text range anchors.</summary>
    public int? StartOffset { get; set; }

    /// <summary>End inline index for text range anchors.</summary>
    public int? EndInlineIndex { get; set; }

    /// <summary>End character offset for text range anchors.</summary>
    public int? EndOffset { get; set; }

    /// <summary>External anchor id from imported DOCX/ODT content.</summary>
    public string? ExternalAnchorId { get; set; }

    /// <summary>Optional rendition anchor id for finalized outputs.</summary>
    public string? RenditionAnchorId { get; set; }

    /// <summary>Whether the original text range no longer exists in the live runtime document.</summary>
    public bool IsOrphaned { get; set; }
}

/// <summary>Document comment anchor type.</summary>
public enum DocumentCommentAnchorType
{
    /// <summary>Anchor targets an entire block.</summary>
    Block,

    /// <summary>Anchor targets a text range.</summary>
    TextRange,

    /// <summary>Anchor was imported from a DOCX comment range.</summary>
    ImportedDocx,

    /// <summary>Anchor was imported from an ODT annotation.</summary>
    ImportedOdt,

    /// <summary>Anchor targets a page-level location.</summary>
    Page,

    /// <summary>Anchor targets a finalized rendition location.</summary>
    Rendition
}

/// <summary>Document comment status.</summary>
public enum DocumentCommentStatus
{
    /// <summary>Thread is open.</summary>
    Open,

    /// <summary>Thread is resolved.</summary>
    Resolved
}

/// <summary>Comment visibility scope.</summary>
public enum DocumentCommentVisibility
{
    /// <summary>Internal application users only.</summary>
    Internal,

    /// <summary>External collaborators or clients.</summary>
    External,

    /// <summary>Client-visible comment.</summary>
    Client,

    /// <summary>Public comment included in shared outputs.</summary>
    Public
}

/// <summary>Comment rail filter mode.</summary>
public enum DocumentCommentFilter
{
    /// <summary>Show every comment thread.</summary>
    All,

    /// <summary>Show only open comment threads.</summary>
    Open,

    /// <summary>Show only resolved comment threads.</summary>
    Resolved,

    /// <summary>Show only threads authored by the current user.</summary>
    Mine
}

/// <summary>Comment rail sort mode.</summary>
public enum DocumentCommentSortMode
{
    /// <summary>Sort by document anchor position.</summary>
    Position,

    /// <summary>Sort by most recent activity first.</summary>
    Time
}

/// <summary>Helpers for filtering and ordering document comments.</summary>
public static class DocumentCommentComparer
{
    /// <summary>Filters and sorts comment threads for the review rail.</summary>
    public static IReadOnlyList<DocumentComment> Apply(
        IEnumerable<DocumentComment> comments,
        DocumentCommentFilter filter,
        DocumentCommentSortMode sortMode,
        string? currentAuthorId = null)
    {
        var filtered = comments.Where(comment => MatchesFilter(comment, filter, currentAuthorId));
        return (sortMode == DocumentCommentSortMode.Time
                ? filtered
                    .OrderBy(comment => comment.Status == DocumentCommentStatus.Resolved)
                    .ThenByDescending(GetLastActivity)
                    .ThenBy(comment => comment.Id, StringComparer.Ordinal)
                : filtered
                    .OrderBy(comment => comment.Status == DocumentCommentStatus.Resolved)
                    .ThenBy(GetAnchorBlockKey, StringComparer.Ordinal)
                    .ThenBy(GetAnchorStart)
                    .ThenBy(GetFirstActivity)
                    .ThenBy(comment => comment.Id, StringComparer.Ordinal))
            .ToList();
    }

    private static bool MatchesFilter(DocumentComment comment, DocumentCommentFilter filter, string? currentAuthorId)
        => filter switch
        {
            DocumentCommentFilter.Open => comment.Status == DocumentCommentStatus.Open,
            DocumentCommentFilter.Resolved => comment.Status == DocumentCommentStatus.Resolved,
            DocumentCommentFilter.Mine => IsMine(comment, currentAuthorId),
            _ => true
        };

    private static bool IsMine(DocumentComment comment, string? currentAuthorId)
        => !string.IsNullOrWhiteSpace(currentAuthorId)
        && comment.Entries.Any(entry => string.Equals(entry.Author.Id, currentAuthorId, StringComparison.Ordinal));

    private static string GetAnchorBlockKey(DocumentComment comment)
        => comment.Anchor.BlockId
        ?? comment.Anchor.ExternalAnchorId
        ?? comment.Anchor.RenditionAnchorId
        ?? string.Empty;

    private static int GetAnchorStart(DocumentComment comment)
        => comment.Anchor.StartOffset
        ?? comment.Anchor.StartInlineIndex
        ?? 0;

    private static DateTimeOffset GetFirstActivity(DocumentComment comment)
        => comment.Entries.OrderBy(entry => entry.CreatedAt).FirstOrDefault()?.CreatedAt
        ?? DateTimeOffset.MinValue;

    private static DateTimeOffset GetLastActivity(DocumentComment comment)
        => comment.Entries.OrderByDescending(entry => entry.ModifiedAt ?? entry.CreatedAt).FirstOrDefault()?.ModifiedAt
        ?? comment.Entries.OrderByDescending(entry => entry.CreatedAt).FirstOrDefault()?.CreatedAt
        ?? DateTimeOffset.MinValue;
}
