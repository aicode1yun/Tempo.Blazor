using System.Text.Json.Serialization;

namespace Tempo.Blazor.DocumentEditor.Models;

/// <summary>Block type used by the document editor JSON model.</summary>
public enum DocumentBlockType
{
    /// <summary>Paragraph block.</summary>
    Paragraph,

    /// <summary>Heading block.</summary>
    Heading,

    /// <summary>List block.</summary>
    List,

    /// <summary>Quote block.</summary>
    Quote,

    /// <summary>Table block.</summary>
    Table,

    /// <summary>Image block.</summary>
    Image,

    /// <summary>Page break block.</summary>
    PageBreak
}

/// <summary>Single ordered document block.</summary>
public class DocumentBlock
{
    /// <summary>Stable block identifier.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Optional section identifier.</summary>
    public string? SectionId { get; set; }

    /// <summary>Block type.</summary>
    public DocumentBlockType Type { get; set; } = DocumentBlockType.Paragraph;

    /// <summary>Sort order within the document.</summary>
    public double Order { get; set; }

    /// <summary>Block content.</summary>
    public DocumentBlockContent Content { get; set; } = new ParagraphBlockContent();
}

/// <summary>Base class for block content payloads.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(ParagraphBlockContent), "paragraph")]
[JsonDerivedType(typeof(HeadingBlockContent), "heading")]
[JsonDerivedType(typeof(ListBlockContent), "list")]
[JsonDerivedType(typeof(QuoteBlockContent), "quote")]
[JsonDerivedType(typeof(TableBlockContent), "table")]
[JsonDerivedType(typeof(ImageBlockContent), "image")]
[JsonDerivedType(typeof(PageBreakBlockContent), "pageBreak")]
public abstract class DocumentBlockContent
{
}

/// <summary>Paragraph block content.</summary>
public class ParagraphBlockContent : DocumentBlockContent
{
    /// <summary>Inline paragraph content.</summary>
    public List<InlineContent> Inlines { get; set; } = [];
}

/// <summary>Heading block content.</summary>
public class HeadingBlockContent : DocumentBlockContent
{
    /// <summary>Heading level, usually 1-6.</summary>
    public int Level { get; set; } = 1;

    /// <summary>Inline heading content.</summary>
    public List<InlineContent> Inlines { get; set; } = [];
}

/// <summary>List block content.</summary>
public class ListBlockContent : DocumentBlockContent
{
    /// <summary>Whether the list is ordered.</summary>
    public bool Ordered { get; set; }

    /// <summary>Zero-based nesting level.</summary>
    public int IndentLevel { get; set; }

    /// <summary>Starting number for ordered lists.</summary>
    public int StartNumber { get; set; } = 1;

    /// <summary>Inline list item content.</summary>
    public List<InlineContent> Inlines { get; set; } = [];
}

/// <summary>Quote block content.</summary>
public class QuoteBlockContent : DocumentBlockContent
{
    /// <summary>Inline quote content.</summary>
    public List<InlineContent> Inlines { get; set; } = [];
}

/// <summary>Table block content.</summary>
public class TableBlockContent : DocumentBlockContent
{
    /// <summary>Rows in the table.</summary>
    public List<TableRowContent> Rows { get; set; } = [];
}

/// <summary>Table row content.</summary>
public class TableRowContent
{
    /// <summary>Cells in the row.</summary>
    public List<TableCellContent> Cells { get; set; } = [];
}

/// <summary>Table cell content.</summary>
public class TableCellContent
{
    /// <summary>Stable cell identifier.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Column span for merged cells.</summary>
    public int ColumnSpan { get; set; } = 1;

    /// <summary>Row span for merged cells.</summary>
    public int RowSpan { get; set; } = 1;

    /// <summary>Merge metadata.</summary>
    public TableCellMerge Merge { get; set; } = new();

    /// <summary>Blocks nested in the table cell.</summary>
    public List<DocumentBlock> Blocks { get; set; } = [];
}

/// <summary>Metadata describing how a table cell participates in a merge.</summary>
public class TableCellMerge
{
    /// <summary>Whether this cell is the origin of a merged area.</summary>
    public bool IsOrigin { get; set; } = true;

    /// <summary>Optional origin cell id when this cell is covered by another merged cell.</summary>
    public string? OriginCellId { get; set; }
}

/// <summary>Table cell span value object.</summary>
public class TableCellSpan
{
    /// <summary>Column span.</summary>
    public int Columns { get; set; } = 1;

    /// <summary>Row span.</summary>
    public int Rows { get; set; } = 1;
}

/// <summary>Image block content.</summary>
public class ImageBlockContent : DocumentBlockContent
{
    /// <summary>Image source kind.</summary>
    public DocumentImageSource Source { get; set; } = DocumentImageSource.Url;

    /// <summary>Direct image URL when <see cref="Source"/> is <see cref="DocumentImageSource.Url"/>.</summary>
    public string? Url { get; set; }

    /// <summary>Provider asset id when <see cref="Source"/> is asset-backed.</summary>
    public string? AssetId { get; set; }

    /// <summary>Alternative text.</summary>
    public string? AltText { get; set; }

    /// <summary>Optional caption.</summary>
    public string? Caption { get; set; }

    /// <summary>Image size.</summary>
    public DocumentImageSize Size { get; set; } = new();

    /// <summary>Image alignment.</summary>
    public DocumentImageAlignment Alignment { get; set; } = DocumentImageAlignment.Center;

    /// <summary>Optional floating layout metadata.</summary>
    public DocumentFloatingLayout? FloatingLayout { get; set; }
}

/// <summary>Source kind for a document image.</summary>
public enum DocumentImageSource
{
    /// <summary>Image is loaded from a direct URL.</summary>
    Url,

    /// <summary>Image is loaded from a provider-managed asset.</summary>
    Asset,

    /// <summary>Image is a local/pending clipboard asset.</summary>
    Clipboard
}

/// <summary>Image alignment in the document flow.</summary>
public enum DocumentImageAlignment
{
    /// <summary>Align to start/left.</summary>
    Start,

    /// <summary>Center align.</summary>
    Center,

    /// <summary>Align to end/right.</summary>
    End
}

/// <summary>Document image size metadata.</summary>
public class DocumentImageSize
{
    /// <summary>Image width in points or pixels, depending on renderer context.</summary>
    public double? Width { get; set; }

    /// <summary>Image height in points or pixels, depending on renderer context.</summary>
    public double? Height { get; set; }

    /// <summary>Whether aspect ratio should be preserved during resize.</summary>
    public bool LockAspectRatio { get; set; } = true;
}

/// <summary>Page break block content.</summary>
public class PageBreakBlockContent : DocumentBlockContent
{
    /// <summary>Optional next section identifier.</summary>
    public string? NextSectionId { get; set; }
}
