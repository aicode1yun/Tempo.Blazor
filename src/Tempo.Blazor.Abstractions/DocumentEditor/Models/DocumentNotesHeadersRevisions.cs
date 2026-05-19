namespace Tempo.Blazor.DocumentEditor.Models;

/// <summary>Footnote or endnote definition.</summary>
public class DocumentNote
{
    /// <summary>Stable note id.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Note type.</summary>
    public DocumentNoteType Type { get; set; } = DocumentNoteType.Footnote;

    /// <summary>Optional section id that owns the note.</summary>
    public string? SectionId { get; set; }

    /// <summary>Optional marker displayed in the document.</summary>
    public string? Marker { get; set; }

    /// <summary>Note body blocks.</summary>
    public List<DocumentBlock> Blocks { get; set; } = [];

    /// <summary>Ids of inline references pointing to this note.</summary>
    public List<string> ReferenceIds { get; set; } = [];
}

/// <summary>Document note type.</summary>
public enum DocumentNoteType
{
    /// <summary>Footnote.</summary>
    Footnote,

    /// <summary>Endnote.</summary>
    Endnote
}

/// <summary>Footnote and endnote numbering settings.</summary>
public class DocumentNoteNumbering
{
    /// <summary>Numbering style, for example decimal, lowerRoman, or lowerLetter.</summary>
    public string Style { get; set; } = "decimal";

    /// <summary>Starting number.</summary>
    public int StartAt { get; set; } = 1;

    /// <summary>Whether numbering restarts for each section.</summary>
    public bool RestartEachSection { get; set; } = true;
}

/// <summary>Header or footer content definition.</summary>
public class DocumentHeaderFooter
{
    /// <summary>Stable header/footer id.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Header or footer type.</summary>
    public DocumentHeaderFooterType Type { get; set; } = DocumentHeaderFooterType.Header;

    /// <summary>Header/footer scope.</summary>
    public DocumentHeaderFooterScope Scope { get; set; } = DocumentHeaderFooterScope.Primary;

    /// <summary>Optional owning section id.</summary>
    public string? SectionId { get; set; }

    /// <summary>Header/footer blocks.</summary>
    public List<DocumentBlock> Blocks { get; set; } = [];
}

/// <summary>Header/footer type.</summary>
public enum DocumentHeaderFooterType
{
    /// <summary>Header content.</summary>
    Header,

    /// <summary>Footer content.</summary>
    Footer
}

/// <summary>Header/footer page scope.</summary>
public enum DocumentHeaderFooterScope
{
    /// <summary>Primary/default pages.</summary>
    Primary,

    /// <summary>First page only.</summary>
    FirstPage,

    /// <summary>Even pages.</summary>
    EvenPages,

    /// <summary>Odd pages.</summary>
    OddPages
}

/// <summary>Tracked document revision.</summary>
public class DocumentRevision
{
    /// <summary>Stable revision id.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Revision type.</summary>
    public DocumentRevisionType Type { get; set; } = DocumentRevisionType.Insertion;

    /// <summary>Range affected by the revision.</summary>
    public DocumentRevisionRange Range { get; set; } = new();

    /// <summary>Revision author.</summary>
    public DocumentRevisionAuthor Author { get; set; } = new();

    /// <summary>Creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Current review action.</summary>
    public DocumentRevisionAction Action { get; set; } = DocumentRevisionAction.Pending;

    /// <summary>Optional payload for format imports or future engine metadata.</summary>
    public string? PayloadJson { get; set; }
}

/// <summary>Structured payload stored for formatting tracked-change revisions.</summary>
public sealed class DocumentFormattingRevisionPayload
{
    /// <summary>Inline mark affected by the formatting change.</summary>
    public InlineMarkType MarkType { get; set; }

    /// <summary>Whether the pending revision currently applies the mark.</summary>
    public bool NewActive { get; set; }
}

/// <summary>Tracked revision type.</summary>
public enum DocumentRevisionType
{
    /// <summary>Inserted content.</summary>
    Insertion,

    /// <summary>Deleted content.</summary>
    Deletion,

    /// <summary>Formatting change.</summary>
    Formatting,

    /// <summary>Moved content.</summary>
    Move,

    /// <summary>Structural change such as paragraph split or soft break.</summary>
    Structure,

    /// <summary>Image object change.</summary>
    Image,

    /// <summary>Table object change.</summary>
    Table
}

/// <summary>Range affected by a tracked revision.</summary>
public class DocumentRevisionRange
{
    /// <summary>Target block id.</summary>
    public string? BlockId { get; set; }

    /// <summary>Optional source block id for move revisions.</summary>
    public string? SourceBlockId { get; set; }

    /// <summary>Start inline index.</summary>
    public int? StartInlineIndex { get; set; }

    /// <summary>Start character offset.</summary>
    public int? StartOffset { get; set; }

    /// <summary>End inline index.</summary>
    public int? EndInlineIndex { get; set; }

    /// <summary>End character offset.</summary>
    public int? EndOffset { get; set; }
}

/// <summary>Author metadata stored with a revision.</summary>
public class DocumentRevisionAuthor : DocumentEditorAuthor
{
}

/// <summary>Review action applied to a revision.</summary>
public enum DocumentRevisionAction
{
    /// <summary>Revision is pending review.</summary>
    Pending,

    /// <summary>Revision was accepted.</summary>
    Accepted,

    /// <summary>Revision was rejected.</summary>
    Rejected
}

/// <summary>Display mode used when rendering tracked changes.</summary>
public enum DocumentReviewDisplayMode
{
    /// <summary>Show all insertion/deletion/formatting markup.</summary>
    AllMarkup,

    /// <summary>Show text normally and keep only compact revision indicators.</summary>
    SimpleMarkup,

    /// <summary>Show the final document content without visible revision markup.</summary>
    NoMarkup,

    /// <summary>Show the original document content before pending revisions.</summary>
    Original
}

/// <summary>Filter for batch revision review operations.</summary>
public sealed class DocumentRevisionFilter
{
    /// <summary>Optional author id filter.</summary>
    public string? AuthorId { get; set; }

    /// <summary>Optional revision type filter.</summary>
    public DocumentRevisionType? Type { get; set; }

    /// <summary>Returns whether the revision matches this filter.</summary>
    public bool Matches(DocumentRevision revision)
    {
        if (!string.IsNullOrWhiteSpace(AuthorId)
            && !string.Equals(revision.Author.Id, AuthorId, StringComparison.Ordinal))
        {
            return false;
        }

        return Type is null || revision.Type == Type.Value;
    }
}
