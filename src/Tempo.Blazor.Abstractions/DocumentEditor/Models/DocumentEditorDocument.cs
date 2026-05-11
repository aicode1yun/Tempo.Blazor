using System.Text.Json.Serialization;

namespace Tempo.Blazor.DocumentEditor.Models;

/// <summary>Root JSON document used by <c>TmDocumentEditor</c>.</summary>
public class DocumentEditorDocument
{
    /// <summary>Current document editor schema version.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>Schema version used to serialize this document.</summary>
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>Stable document identifier.</summary>
    public string DocumentId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Document metadata.</summary>
    public DocumentEditorMetadata Metadata { get; set; } = new();

    /// <summary>Default page settings for the document.</summary>
    public DocumentPageSettings PageSettings { get; set; } = new();

    /// <summary>Document sections.</summary>
    public List<DocumentSection> Sections { get; set; } = [];

    /// <summary>Ordered document blocks.</summary>
    public List<DocumentBlock> Blocks { get; set; } = [];

    /// <summary>Document comments.</summary>
    public List<DocumentComment> Comments { get; set; } = [];

    /// <summary>Footnotes and endnotes used by the document.</summary>
    public List<DocumentNote> Notes { get; set; } = [];

    /// <summary>Header and footer definitions.</summary>
    public List<DocumentHeaderFooter> HeadersFooters { get; set; } = [];

    /// <summary>Tracked revisions stored with the document snapshot.</summary>
    public List<DocumentRevision> Revisions { get; set; } = [];

    /// <summary>Image/file assets referenced by blocks.</summary>
    public List<DocumentImageAsset> Assets { get; set; } = [];

    /// <summary>Named anchors used by tokens, placeholders, and signing-ready renditions.</summary>
    public List<DocumentAnchor> Anchors { get; set; } = [];

    /// <summary>Creates a new empty document with one default section.</summary>
    public static DocumentEditorDocument Empty(string? documentId = null)
    {
        var id = string.IsNullOrWhiteSpace(documentId)
            ? Guid.NewGuid().ToString("N")
            : documentId;

        var sectionId = Guid.NewGuid().ToString("N");
        return new DocumentEditorDocument
        {
            DocumentId = id!,
            Sections =
            [
                new DocumentSection
                {
                    Id = sectionId,
                    Order = 0,
                    Properties = new DocumentSectionProperties()
                }
            ]
        };
    }
}

/// <summary>Metadata associated with a document.</summary>
public class DocumentEditorMetadata
{
    /// <summary>Document title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Optional document description.</summary>
    public string? Description { get; set; }

    /// <summary>Document author.</summary>
    public DocumentEditorAuthor? Author { get; set; }

    /// <summary>Document creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Document last modification timestamp.</summary>
    public DateTimeOffset? ModifiedAt { get; set; }

    /// <summary>Current document status.</summary>
    public DocumentEditorStatus Status { get; set; } = DocumentEditorStatus.Draft;

    /// <summary>Arbitrary tags associated with the document.</summary>
    public List<string> Tags { get; set; } = [];
}

/// <summary>Document author or contributor metadata.</summary>
public class DocumentEditorAuthor
{
    /// <summary>Stable author identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Displayed author name.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Optional email address.</summary>
    public string? Email { get; set; }

    /// <summary>Optional avatar URL.</summary>
    public string? AvatarUrl { get; set; }
}

/// <summary>Document lifecycle status.</summary>
public enum DocumentEditorStatus
{
    /// <summary>Document is being drafted.</summary>
    Draft,

    /// <summary>Document is under review.</summary>
    Review,

    /// <summary>Document is finalized but not archived.</summary>
    Final,

    /// <summary>Document is archived and normally read-only.</summary>
    Archived
}

/// <summary>Editor interaction mode.</summary>
public enum DocumentEditorMode
{
    /// <summary>Full editing mode.</summary>
    Edit,

    /// <summary>Review mode with comments and revisions.</summary>
    Review,

    /// <summary>Read-only viewing mode.</summary>
    ReadOnly,

    /// <summary>Read-only view of a finalized rendition.</summary>
    RenditionPreview
}

/// <summary>Default page settings for a document or section.</summary>
public class DocumentPageSettings
{
    /// <summary>Page size.</summary>
    public DocumentPageSize Size { get; set; } = DocumentPageSize.A4;

    /// <summary>Page margins.</summary>
    public DocumentPageMargins Margins { get; set; } = DocumentPageMargins.Default;

    /// <summary>Whether pages are landscape instead of portrait.</summary>
    public bool Landscape { get; set; }
}

/// <summary>Physical page size in points.</summary>
public class DocumentPageSize
{
    /// <summary>A4 page size in points.</summary>
    public static DocumentPageSize A4 => new() { Name = "A4", Width = 595.276, Height = 841.89 };

    /// <summary>US Letter page size in points.</summary>
    public static DocumentPageSize Letter => new() { Name = "Letter", Width = 612, Height = 792 };

    /// <summary>Optional page size name.</summary>
    public string? Name { get; set; }

    /// <summary>Page width in points.</summary>
    public double Width { get; set; }

    /// <summary>Page height in points.</summary>
    public double Height { get; set; }
}

/// <summary>Page margins in points.</summary>
public class DocumentPageMargins
{
    /// <summary>Default 72 pt margins.</summary>
    public static DocumentPageMargins Default => new() { Top = 72, Right = 72, Bottom = 72, Left = 72 };

    /// <summary>Top margin in points.</summary>
    public double Top { get; set; }

    /// <summary>Right margin in points.</summary>
    public double Right { get; set; }

    /// <summary>Bottom margin in points.</summary>
    public double Bottom { get; set; }

    /// <summary>Left margin in points.</summary>
    public double Left { get; set; }
}

/// <summary>Document section with independent page settings and headers/footers.</summary>
public class DocumentSection
{
    /// <summary>Stable section identifier.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Sort order of the section.</summary>
    public int Order { get; set; }

    /// <summary>Section display title.</summary>
    public string? Title { get; set; }

    /// <summary>Section properties.</summary>
    public DocumentSectionProperties Properties { get; set; } = new();
}

/// <summary>Section-level page, header/footer, and note settings.</summary>
public class DocumentSectionProperties
{
    /// <summary>Page settings for this section.</summary>
    public DocumentPageSettings PageSettings { get; set; } = new();

    /// <summary>Whether the section has a different first page header/footer.</summary>
    public bool DifferentFirstPage { get; set; }

    /// <summary>Whether the section uses different odd and even headers/footers.</summary>
    public bool DifferentOddAndEvenPages { get; set; }

    /// <summary>Header and footer references used by this section.</summary>
    public List<DocumentHeaderFooterReference> HeaderFooterReferences { get; set; } = [];

    /// <summary>Footnote/endnote numbering settings.</summary>
    public DocumentNoteNumbering NoteNumbering { get; set; } = new();
}

/// <summary>Reference from a section to a header or footer definition.</summary>
public class DocumentHeaderFooterReference
{
    /// <summary>Header/footer definition identifier.</summary>
    public string HeaderFooterId { get; set; } = string.Empty;

    /// <summary>Referenced header/footer type.</summary>
    public DocumentHeaderFooterType Type { get; set; } = DocumentHeaderFooterType.Header;

    /// <summary>Scope where this reference applies.</summary>
    public DocumentHeaderFooterScope Scope { get; set; } = DocumentHeaderFooterScope.Primary;
}
