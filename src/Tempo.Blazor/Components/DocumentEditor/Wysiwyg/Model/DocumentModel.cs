namespace Tempo.Blazor.Components.DocumentEditor.Wysiwyg.Model;

/// <summary>Abstract base for all document nodes in the WYSIWYG model.</summary>
public abstract class DocumentNode
{
    /// <summary>Stable node identifier.</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>Optional node attributes for extensibility.</summary>
    public Dictionary<string, object> Attributes { get; init; } = new();
}

/// <summary>Root document model for the WYSIWYG editor.</summary>
public class DocumentModel : DocumentNode
{
    /// <summary>Document metadata (title, author, dates).</summary>
    public DocumentMetadata Metadata { get; set; } = new();

    /// <summary>Page settings (size, margins, orientation).</summary>
    public PageSettings PageSettings { get; set; } = PageSettings.DefaultA4();

    /// <summary>Document sections.</summary>
    public List<Section> Sections { get; init; } = new();

    /// <summary>Flat list of body blocks. Pages are virtual and computed from content + settings.</summary>
    public List<Block> Body { get; init; } = new();

    /// <summary>Headers and footers defined for the document.</summary>
    public List<HeaderFooter> HeadersFooters { get; init; } = new();

    /// <summary>Footnotes and endnotes.</summary>
    public List<DocumentNote> Notes { get; init; } = new();

    /// <summary>Comment threads.</summary>
    public List<DocumentComment> Comments { get; init; } = new();

    /// <summary>Track changes revisions.</summary>
    public List<DocumentRevision> Revisions { get; init; } = new();
}

/// <summary>Document metadata.</summary>
public class DocumentMetadata
{
    /// <summary>Document title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Document author identifier.</summary>
    public string? AuthorId { get; set; }

    /// <summary>Document author display name.</summary>
    public string? AuthorName { get; set; }

    /// <summary>Creation timestamp.</summary>
    public DateTimeOffset? CreatedAt { get; set; }

    /// <summary>Last modification timestamp.</summary>
    public DateTimeOffset? ModifiedAt { get; set; }
}

/// <summary>Page settings (size, margins, orientation).</summary>
public class PageSettings
{
    /// <summary>Page width (CSS value).</summary>
    public string Width { get; set; } = "210mm";

    /// <summary>Page height (CSS value).</summary>
    public string Height { get; set; } = "297mm";

    /// <summary>Top margin.</summary>
    public string MarginTop { get; set; } = "25.4mm";

    /// <summary>Bottom margin.</summary>
    public string MarginBottom { get; set; } = "25.4mm";

    /// <summary>Left margin.</summary>
    public string MarginLeft { get; set; } = "25.4mm";

    /// <summary>Right margin.</summary>
    public string MarginRight { get; set; } = "25.4mm";

    /// <summary>Page orientation.</summary>
    public PageOrientation Orientation { get; set; } = PageOrientation.Portrait;

    /// <summary>Factory for default A4 settings.</summary>
    public static PageSettings DefaultA4() => new();
}

/// <summary>Page orientation.</summary>
public enum PageOrientation
{
    Portrait,
    Landscape
}

/// <summary>Document section.</summary>
public class Section
{
    /// <summary>Section identifier.</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>Section-specific properties.</summary>
    public SectionProperties Properties { get; set; } = new();
}

/// <summary>Section properties.</summary>
public class SectionProperties
{
    /// <summary>Different header/footer for first page.</summary>
    public bool DifferentFirstPage { get; set; }

    /// <summary>Different header/footer for odd/even pages.</summary>
    public bool DifferentOddEvenPages { get; set; }

    /// <summary>Number of columns.</summary>
    public int ColumnCount { get; set; } = 1;
}

/// <summary>Header or footer definition.</summary>
public class HeaderFooter
{
    /// <summary>Identifier.</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>Type of header/footer.</summary>
    public HeaderFooterType Type { get; set; } = HeaderFooterType.Header;

    /// <summary>Scope (primary, first page, even page).</summary>
    public HeaderFooterScope Scope { get; set; } = HeaderFooterScope.Primary;

    /// <summary>Content blocks.</summary>
    public List<Block> Blocks { get; init; } = new();
}

/// <summary>Header/footer type.</summary>
public enum HeaderFooterType
{
    Header,
    Footer
}

/// <summary>Header/footer scope.</summary>
public enum HeaderFooterScope
{
    Primary,
    FirstPage,
    EvenPage
}

/// <summary>Document note (footnote or endnote).</summary>
public class DocumentNote
{
    /// <summary>Note identifier.</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>Note type.</summary>
    public DocumentNoteType NoteType { get; set; } = DocumentNoteType.Footnote;

    /// <summary>Visible marker (e.g. "1").</summary>
    public string Marker { get; set; } = string.Empty;

    /// <summary>Note content blocks.</summary>
    public List<Block> Blocks { get; init; } = new();
}

/// <summary>Note type.</summary>
public enum DocumentNoteType
{
    Footnote,
    Endnote
}

/// <summary>Document comment thread.</summary>
public class DocumentComment
{
    /// <summary>Comment identifier.</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>Anchor position/range.</summary>
    public DocumentCommentAnchor Anchor { get; set; } = new();

    /// <summary>Comment entries (replies).</summary>
    public List<DocumentCommentEntry> Entries { get; init; } = new();

    /// <summary>Resolved status.</summary>
    public bool IsResolved { get; set; }
}

/// <summary>Comment anchor.</summary>
public class DocumentCommentAnchor
{
    /// <summary>Start block identifier.</summary>
    public string StartBlockId { get; set; } = string.Empty;

    /// <summary>Start inline index.</summary>
    public int StartInlineIndex { get; set; }

    /// <summary>Start text offset.</summary>
    public int StartTextOffset { get; set; }

    /// <summary>End block identifier.</summary>
    public string EndBlockId { get; set; } = string.Empty;

    /// <summary>End inline index.</summary>
    public int EndInlineIndex { get; set; }

    /// <summary>End text offset.</summary>
    public int EndTextOffset { get; set; }
}

/// <summary>Single comment entry.</summary>
public class DocumentCommentEntry
{
    /// <summary>Author identifier.</summary>
    public string? AuthorId { get; set; }

    /// <summary>Author display name.</summary>
    public string? AuthorName { get; set; }

    /// <summary>Comment text.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Timestamp.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Track changes revision.</summary>
public class DocumentRevision
{
    /// <summary>Revision identifier.</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>Revision type.</summary>
    public DocumentRevisionType Type { get; set; } = DocumentRevisionType.Insertion;

    /// <summary>Author.</summary>
    public string? AuthorId { get; set; }

    /// <summary>Author name.</summary>
    public string? AuthorName { get; set; }

    /// <summary>Timestamp.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Current action status.</summary>
    public DocumentRevisionAction Action { get; set; } = DocumentRevisionAction.Pending;
}

/// <summary>Revision type.</summary>
public enum DocumentRevisionType
{
    Insertion,
    Deletion,
    Formatting,
    Move
}

/// <summary>Revision action.</summary>
public enum DocumentRevisionAction
{
    Pending,
    Accepted,
    Rejected
}
