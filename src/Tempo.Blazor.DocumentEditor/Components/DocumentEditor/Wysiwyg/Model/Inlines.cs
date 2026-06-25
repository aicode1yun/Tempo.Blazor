using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Components.DocumentEditor.Wysiwyg.Model;

/// <summary>Abstract base for inline content nodes.</summary>
public abstract class Inline : DocumentNode
{
    /// <summary>Formatting marks applied to this inline.</summary>
    public List<Mark> Marks { get; init; } = new();
}

/// <summary>Text run with optional formatting marks.</summary>
public class TextRun : Inline
{
    /// <summary>Plain text content.</summary>
    public string Text { get; set; } = string.Empty;
}

/// <summary>Hard line break within a block.</summary>
public class HardBreak : Inline
{
}

/// <summary>Tab character inline.</summary>
public class TabInline : Inline
{
}

/// <summary>Drawing object inline anchored in text flow.</summary>
public class DrawingInline : Inline
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

    /// <summary>Additional drawing metadata used by importers, exporters, and editor runtime migrations.</summary>
    public Dictionary<string, string?> Metadata { get; set; } = [];
}
