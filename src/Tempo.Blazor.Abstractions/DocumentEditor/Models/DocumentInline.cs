using System.Text.Json.Serialization;

namespace Tempo.Blazor.DocumentEditor.Models;

/// <summary>Base class for inline content inside text-based document blocks.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(TextRun), "text")]
[JsonDerivedType(typeof(TokenRun), "token")]
[JsonDerivedType(typeof(DocumentFieldRun), "field")]
[JsonDerivedType(typeof(DocumentNoteReferenceRun), "noteReference")]
[JsonDerivedType(typeof(DocumentDrawingRun), "drawing")]
public abstract class InlineContent
{
    /// <summary>Stable inline identifier used for selection mapping and comment anchoring.</summary>
    public string? Id { get; set; }

    /// <summary>Inline marks applied to this content run.</summary>
    public List<InlineMark> Marks { get; set; } = [];
}

/// <summary>Plain text run.</summary>
public class TextRun : InlineContent
{
    /// <summary>Text value.</summary>
    public string Text { get; set; } = string.Empty;
}

/// <summary>Drawing object anchored in a text run, such as an inline or floating image.</summary>
public class DocumentDrawingRun : InlineContent
{
    /// <summary>Stable drawing object identifier used by layout, selection, commands, and persistence.</summary>
    public string ObjectId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Drawing object kind. Defaults to image.</summary>
    public DocumentDrawingKind Kind { get; set; } = DocumentDrawingKind.Image;

    /// <summary>Image source kind when <see cref="Kind"/> is <see cref="DocumentDrawingKind.Image"/>.</summary>
    public DocumentImageSource Source { get; set; } = DocumentImageSource.Url;

    /// <summary>Direct image URL when <see cref="Source"/> is <see cref="DocumentImageSource.Url"/>.</summary>
    public string? Url { get; set; }

    /// <summary>Provider asset id when <see cref="Source"/> is asset-backed.</summary>
    public string? AssetId { get; set; }

    /// <summary>Alternative text for assistive technology.</summary>
    public string? AltText { get; set; }

    /// <summary>Whether assistive technology should ignore this drawing as decorative.</summary>
    public bool IsDecorative { get; set; }

    /// <summary>Optional caption associated with the drawing.</summary>
    public string? Caption { get; set; }

    /// <summary>Source/default image size. User-controlled rendered size is stored in <see cref="Layout"/>.</summary>
    public DocumentImageSize Size { get; set; } = new();

    /// <summary>Intrinsic image size reported by the image asset once loaded.</summary>
    public DocumentImageSize NaturalSize { get; set; } = new();

    /// <summary>Canonical object layout used for inline, anchored, and fixed drawing positioning.</summary>
    public DocumentObjectLayout Layout { get; set; } = DocumentObjectLayout.Inline();

    /// <summary>Optional hyperlink URL wrapping the drawing.</summary>
    public string? LinkUrl { get; set; }

    /// <summary>Optional DOCX DrawingML metadata used for high-fidelity import, export, and preserve-only roundtrips.</summary>
    public DocumentDocxDrawingMetadata? Docx { get; set; }

    /// <summary>Supplemental drawing metadata used by importers, exporters, and editor runtime migrations.</summary>
    public Dictionary<string, string?> Metadata { get; set; } = [];
}

/// <summary>Drawing object kinds supported by document inline content.</summary>
public enum DocumentDrawingKind
{
    /// <summary>Bitmap or vector image drawing.</summary>
    Image
}

/// <summary>Token or merge field run.</summary>
public class TokenRun : InlineContent
{
    /// <summary>Stable token key.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Display label shown in the editor.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Optional token type metadata, for example text, date, number, url, or signing-field.</summary>
    public string? TokenType { get; set; }

    /// <summary>Optional short type label shown next to the token chip.</summary>
    public string? TypeLabel { get; set; }

    /// <summary>Optional CSS class copied from the token catalog.</summary>
    public string? ColorClass { get; set; }

    /// <summary>Optional token description copied from the token catalog.</summary>
    public string? Description { get; set; }

    /// <summary>Optional fallback text used when the token has no value.</summary>
    public string? FallbackText { get; set; }
}

/// <summary>Automatic document field rendered from page or document context.</summary>
public class DocumentFieldRun : InlineContent
{
    /// <summary>Automatic field type.</summary>
    public DocumentFieldType FieldType { get; set; } = DocumentFieldType.PageNumber;

    /// <summary>Optional field formatting hint, for example a date format.</summary>
    public string? Format { get; set; }

    /// <summary>Fallback text used when the field cannot be resolved.</summary>
    public string? FallbackText { get; set; }

    /// <summary>Last display text captured by an editor runtime. The renderer may recompute this value.</summary>
    public string? DisplayText { get; set; }
}

/// <summary>Automatic document fields supported by headers, footers, and document text.</summary>
public enum DocumentFieldType
{
    /// <summary>Current page number.</summary>
    PageNumber,

    /// <summary>Total page count.</summary>
    PageCount,

    /// <summary>Current page number followed by the total page count.</summary>
    PageXOfY,

    /// <summary>Current date.</summary>
    Date,

    /// <summary>Document title from metadata.</summary>
    DocumentTitle,

    /// <summary>Document author display name from metadata.</summary>
    Author,

    /// <summary>Last saved or modified date from metadata.</summary>
    LastSaved,

    /// <summary>Current page number within the active section.</summary>
    SectionPageNumber,

    /// <summary>Total page count within the active section.</summary>
    SectionPageCount,

    /// <summary>Document file name from metadata or storage context.</summary>
    FileName,

    /// <summary>Document revision number from metadata or storage context.</summary>
    RevisionNumber
}

/// <summary>Reference to a footnote or endnote.</summary>
public class DocumentNoteReferenceRun : InlineContent
{
    /// <summary>Referenced note id.</summary>
    public string NoteId { get; set; } = string.Empty;

    /// <summary>Referenced note type.</summary>
    public DocumentNoteType NoteType { get; set; } = DocumentNoteType.Footnote;

    /// <summary>Optional displayed marker, for example "1" or "i".</summary>
    public string? DisplayMarker { get; set; }
}

/// <summary>Formatting or semantic mark applied to an inline run.</summary>
public class InlineMark
{
    /// <summary>Mark type.</summary>
    public InlineMarkType Type { get; set; }

    /// <summary>Optional link metadata when <see cref="Type"/> is <see cref="InlineMarkType.Link"/>.</summary>
    public LinkMarkData? Link { get; set; }

    /// <summary>Optional comment anchor metadata when <see cref="Type"/> is <see cref="InlineMarkType.CommentAnchor"/>.</summary>
    public CommentAnchorMarkData? CommentAnchor { get; set; }

    /// <summary>Optional revision id when this mark represents a tracked formatting change.</summary>
    public string? RevisionId { get; set; }

    /// <summary>Optional CSS-like value for color and highlight marks.</summary>
    public string? Value { get; set; }
}

/// <summary>Inline mark types supported by the document editor JSON model.</summary>
public enum InlineMarkType
{
    /// <summary>Bold text.</summary>
    Bold,

    /// <summary>Italic text.</summary>
    Italic,

    /// <summary>Underlined text.</summary>
    Underline,

    /// <summary>Strikethrough text.</summary>
    Strikethrough,

    /// <summary>Superscript text.</summary>
    Superscript,

    /// <summary>Subscript text.</summary>
    Subscript,

    /// <summary>Hyperlink.</summary>
    Link,

    /// <summary>Comment anchor.</summary>
    CommentAnchor,

    /// <summary>Revision anchor.</summary>
    Revision,

    /// <summary>Text highlight.</summary>
    Highlight,

    /// <summary>Text color.</summary>
    TextColor,

    /// <summary>Font family.</summary>
    FontFamily,

    /// <summary>Font size.</summary>
    FontSize,

    /// <summary>Named bookmark anchor (R.5.5). The bookmark name is carried in the mark value.</summary>
    Bookmark
}

/// <summary>Hyperlink metadata.</summary>
public class LinkMarkData
{
    /// <summary>Target URL.</summary>
    public string Href { get; set; } = string.Empty;

    /// <summary>Optional tooltip/title.</summary>
    public string? Title { get; set; }
}

/// <summary>Comment anchor metadata for an inline mark.</summary>
public class CommentAnchorMarkData
{
    /// <summary>Referenced comment id.</summary>
    public string CommentId { get; set; } = string.Empty;

    /// <summary>Anchor id used by imported formats or renderer-specific maps.</summary>
    public string? AnchorId { get; set; }
}
