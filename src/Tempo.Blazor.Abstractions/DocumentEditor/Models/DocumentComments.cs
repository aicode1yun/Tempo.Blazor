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

    /// <summary>Creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Last modification timestamp.</summary>
    public DateTimeOffset? ModifiedAt { get; set; }
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
