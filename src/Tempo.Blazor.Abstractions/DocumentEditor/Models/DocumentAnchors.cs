using Tempo.Blazor.Abstractions.Models;

namespace Tempo.Blazor.DocumentEditor.Models;

/// <summary>Named document anchor used for placeholders, floating objects, and signing-ready maps.</summary>
public class DocumentAnchor
{
    /// <summary>Stable anchor id.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Anchor type.</summary>
    public DocumentAnchorType Type { get; set; } = DocumentAnchorType.Block;

    /// <summary>Optional target block id.</summary>
    public string? BlockId { get; set; }

    /// <summary>Optional floating object block id when <see cref="BlockId"/> points to the paragraph anchor.</summary>
    public string? ObjectBlockId { get; set; }

    /// <summary>Optional token key or placeholder name.</summary>
    public string? Key { get; set; }

    /// <summary>Optional inline index.</summary>
    public int? InlineIndex { get; set; }

    /// <summary>Optional character offset.</summary>
    public int? Offset { get; set; }

    /// <summary>Optional anchor scope when the anchor belongs to a non-body document region.</summary>
    public DocumentRenditionAnchorScope Scope { get; set; } = DocumentRenditionAnchorScope.Body;

    /// <summary>Optional header/footer id when the anchor belongs to a header or footer.</summary>
    public string? HeaderFooterId { get; set; }

    /// <summary>Optional table cell id when the anchor belongs to a table cell.</summary>
    public string? TableCellId { get; set; }

    /// <summary>Optional signing placeholder metadata.</summary>
    public DocumentSigningPlaceholder? SigningPlaceholder { get; set; }

    /// <summary>Floating layout when the anchor belongs to a positioned object.</summary>
    public DocumentFloatingLayout? FloatingLayout { get; set; }
}

/// <summary>Signing placeholder metadata stored in the editable document before rendition finalization.</summary>
public class DocumentSigningPlaceholder
{
    /// <summary>Stable placeholder id.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Placeholder key used to map the anchor to a signing field.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>User-facing label for the generated signing field.</summary>
    public string? Label { get; set; }

    /// <summary>Signer role identifier that should own the generated field.</summary>
    public string? SubmitterUuid { get; set; }

    /// <summary>Generated signing field type.</summary>
    public SigningFieldType FieldType { get; set; } = SigningFieldType.Signature;

    /// <summary>Whether the generated signing field is required.</summary>
    public bool Required { get; set; } = true;

    /// <summary>Preferred normalized field width on the rendition page.</summary>
    public double Width { get; set; } = 0.22;

    /// <summary>Preferred normalized field height on the rendition page.</summary>
    public double Height { get; set; } = 0.045;
}

/// <summary>Document anchor type.</summary>
public enum DocumentAnchorType
{
    /// <summary>Inline anchor.</summary>
    Inline,

    /// <summary>Block anchor.</summary>
    Block,

    /// <summary>Floating object anchor.</summary>
    FloatingObject,

    /// <summary>Signing placeholder anchor.</summary>
    SigningPlaceholder,

    /// <summary>Token/merge-field anchor.</summary>
    Token,

    /// <summary>Finalized rendition anchor.</summary>
    Rendition
}

/// <summary>Floating or anchored layout metadata, modeled closely enough for DOCX/ODT round-tripping.</summary>
public class DocumentFloatingLayout
{
    /// <summary>Whether the object participates inline in the text flow.</summary>
    public bool Inline { get; set; } = true;

    /// <summary>Horizontal reference frame.</summary>
    public DocumentRelativePosition HorizontalRelativeTo { get; set; } = DocumentRelativePosition.Page;

    /// <summary>Vertical reference frame.</summary>
    public DocumentRelativePosition VerticalRelativeTo { get; set; } = DocumentRelativePosition.Paragraph;

    /// <summary>Horizontal offset from the reference frame.</summary>
    public double X { get; set; }

    /// <summary>Vertical offset from the reference frame.</summary>
    public double Y { get; set; }

    /// <summary>Text wrapping mode.</summary>
    public DocumentWrapMode WrapMode { get; set; } = DocumentWrapMode.Inline;

    /// <summary>Z-order for multiple floating objects.</summary>
    public int ZIndex { get; set; }

    /// <summary>Whether the object must keep its current paragraph anchor when moved.</summary>
    public bool LockAnchor { get; set; }

    /// <summary>Optional original wrap mode when an importer normalizes an unsupported mode.</summary>
    public string? PreservedWrapMode { get; set; }
}

/// <summary>Text wrapping mode for anchored objects.</summary>
public enum DocumentWrapMode
{
    /// <summary>Inline in text flow.</summary>
    Inline,

    /// <summary>Square wrapping.</summary>
    Square,

    /// <summary>Tight wrapping, preserved even if a renderer later falls back.</summary>
    Tight,

    /// <summary>Through wrapping.</summary>
    Through,

    /// <summary>Top and bottom wrapping.</summary>
    TopBottom,

    /// <summary>Object is behind text.</summary>
    BehindText,

    /// <summary>Object is in front of text.</summary>
    InFrontOfText
}

/// <summary>Reference frame for relative positioning.</summary>
public enum DocumentRelativePosition
{
    /// <summary>Relative to page.</summary>
    Page,

    /// <summary>Relative to page margins.</summary>
    Margin,

    /// <summary>Relative to column.</summary>
    Column,

    /// <summary>Relative to paragraph.</summary>
    Paragraph,

    /// <summary>Relative to character.</summary>
    Character,

    /// <summary>Relative to line.</summary>
    Line
}
