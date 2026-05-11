using System.Text.Json.Serialization;

namespace Tempo.Blazor.DocumentEditor.Models;

/// <summary>Base class for inline content inside text-based document blocks.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(TextRun), "text")]
[JsonDerivedType(typeof(TokenRun), "token")]
[JsonDerivedType(typeof(DocumentNoteReferenceRun), "noteReference")]
public abstract class InlineContent
{
    /// <summary>Inline marks applied to this content run.</summary>
    public List<InlineMark> Marks { get; set; } = [];
}

/// <summary>Plain text run.</summary>
public class TextRun : InlineContent
{
    /// <summary>Text value.</summary>
    public string Text { get; set; } = string.Empty;
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
    TextColor
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
