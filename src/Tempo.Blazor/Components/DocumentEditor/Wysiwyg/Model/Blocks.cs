namespace Tempo.Blazor.Components.DocumentEditor.Wysiwyg.Model;

/// <summary>Abstract base for all block-level nodes.</summary>
public abstract class Block : DocumentNode
{
    /// <summary>Block type discriminator.</summary>
    public abstract string Type { get; }

    /// <summary>Inline content within the block.</summary>
    public List<Inline> Inlines { get; init; } = new();

    /// <summary>Paragraph-level properties (alignment, spacing, indentation).</summary>
    public ParagraphProperties? Properties { get; set; } = new ParagraphProperties();
}

/// <summary>Standard paragraph block.</summary>
public class ParagraphBlock : Block
{
    /// <inheritdoc />
    public override string Type => "paragraph";
}

/// <summary>Heading block (H1–H6).</summary>
public class HeadingBlock : Block
{
    /// <inheritdoc />
    public override string Type => "heading";

    /// <summary>Heading level (1–6).</summary>
    public int Level { get; set; } = 1;
}

/// <summary>List item block (ordered or unordered).</summary>
public class ListItemBlock : Block
{
    /// <inheritdoc />
    public override string Type => "listItem";

    /// <summary>True for numbered (ordered) list, false for bulleted.</summary>
    public bool Ordered { get; set; }

    /// <summary>Nesting indent level (0 = top level).</summary>
    public int IndentLevel { get; set; }
}

/// <summary>Table block.</summary>
public class TableBlock : Block
{
    /// <inheritdoc />
    public override string Type => "table";

    /// <summary>Table rows.</summary>
    public List<TableRow> Rows { get; init; } = new();

    /// <summary>Optional table properties.</summary>
    public TableProperties? TableProperties { get; set; }
}

/// <summary>Table row.</summary>
public class TableRow
{
    /// <summary>Row identifier.</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>Cells in this row.</summary>
    public List<TableCell> Cells { get; init; } = new();
}

/// <summary>Table cell.</summary>
public class TableCell
{
    /// <summary>Cell identifier.</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>Row span (default 1).</summary>
    public int RowSpan { get; set; } = 1;

    /// <summary>Column span (default 1).</summary>
    public int ColumnSpan { get; set; } = 1;

    /// <summary>Cell content blocks.</summary>
    public List<Block> Blocks { get; init; } = new();
}

/// <summary>Table properties.</summary>
public class TableProperties
{
    /// <summary>Table width (CSS value or auto).</summary>
    public string? Width { get; set; }

    /// <summary>Border style.</summary>
    public string? BorderStyle { get; set; }
}

/// <summary>Image block.</summary>
public class ImageBlock : Block
{
    /// <inheritdoc />
    public override string Type => "image";

    /// <summary>Image source URL or data URI.</summary>
    public string Src { get; set; } = string.Empty;

    /// <summary>Alternative text.</summary>
    public string Alt { get; set; } = string.Empty;

    /// <summary>Image dimensions.</summary>
    public ImageSize Size { get; set; } = new();

    /// <summary>Image layout mode.</summary>
    public ImageLayout Layout { get; set; } = ImageLayout.Inline;

    /// <summary>Floating position (when Layout is Floating).</summary>
    public ImagePosition? Position { get; set; }

    /// <summary>Text wrapping mode (when Layout is Floating).</summary>
    public ImageWrapMode WrapMode { get; set; } = ImageWrapMode.Square;
}

/// <summary>Image size.</summary>
public class ImageSize
{
    /// <summary>Width in pixels or CSS value.</summary>
    public string? Width { get; set; }

    /// <summary>Height in pixels or CSS value.</summary>
    public string? Height { get; set; }
}

/// <summary>Image layout mode.</summary>
public enum ImageLayout
{
    Inline,
    Floating
}

/// <summary>Image floating position.</summary>
public class ImagePosition
{
    /// <summary>Horizontal offset.</summary>
    public string X { get; set; } = "0";

    /// <summary>Vertical offset.</summary>
    public string Y { get; set; } = "0";
}

/// <summary>Image text wrap mode.</summary>
public enum ImageWrapMode
{
    Square,
    Tight,
    Through,
    TopAndBottom,
    BehindText,
    InFrontOfText
}

/// <summary>Page break block.</summary>
public class PageBreakBlock : Block
{
    /// <inheritdoc />
    public override string Type => "pageBreak";
}

/// <summary>Section break block.</summary>
public class SectionBreakBlock : Block
{
    /// <inheritdoc />
    public override string Type => "sectionBreak";

    /// <summary>New section properties applied after this break.</summary>
    public SectionProperties NewSectionProperties { get; set; } = new();
}
