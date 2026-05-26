namespace Tempo.Blazor.DocumentEditor.Models;

/// <summary>DOCX DrawingML metadata preserved alongside a document drawing run.</summary>
public class DocumentDocxDrawingMetadata
{
    /// <summary>Identifier from wp:docPr.</summary>
    public uint? DocPrId { get; set; }

    /// <summary>Name from wp:docPr.</summary>
    public string? DocPrName { get; set; }

    /// <summary>Title from wp:docPr.</summary>
    public string? DocPrTitle { get; set; }

    /// <summary>Description from wp:docPr.</summary>
    public string? DocPrDescription { get; set; }

    /// <summary>Picture non-visual identifier from pic:cNvPr.</summary>
    public uint? PictureNonVisualId { get; set; }

    /// <summary>Picture name from pic:cNvPr.</summary>
    public string? PictureName { get; set; }

    /// <summary>Picture description from pic:cNvPr.</summary>
    public string? PictureDescription { get; set; }

    /// <summary>Embedded image relationship id from a:blip r:embed.</summary>
    public string? RelationshipId { get; set; }

    /// <summary>External image relationship id from a:blip r:link.</summary>
    public string? BlipLinkRelationshipId { get; set; }

    /// <summary>Whether the image source is embedded in the package or referenced externally.</summary>
    public DocumentDocxImageReferenceMode ImageReferenceMode { get; set; } = DocumentDocxImageReferenceMode.Embedded;

    /// <summary>Compression state from a:blip cstate.</summary>
    public string? BlipCompressionState { get; set; }

    /// <summary>Picture fill mode from pic:blipFill.</summary>
    public DocumentDocxBlipFillMode BlipFillMode { get; set; } = DocumentDocxBlipFillMode.Stretch;

    /// <summary>Raw pic:blipFill XML preserved when the fill mode is unsupported by the editor UI.</summary>
    public string? RawBlipFillXml { get; set; }

    /// <summary>Preset geometry from pic:spPr/a:prstGeom.</summary>
    public string? PresetGeometry { get; set; }

    /// <summary>Raw pic:spPr XML preserved when the geometry is unsupported by the editor UI.</summary>
    public string? RawShapePropertiesXml { get; set; }

    /// <summary>Package media metadata for the referenced image part.</summary>
    public DocumentImageMediaInfo Media { get; set; } = new();

    /// <summary>Effect extent from wp:effectExtent.</summary>
    public DocumentObjectEffectExtent EffectExtent { get; set; } = new();

    /// <summary>Whether the anchor is allowed to layout inside table cells.</summary>
    public bool? LayoutInCell { get; set; }

    /// <summary>Whether the DrawingML object is hidden.</summary>
    public bool? Hidden { get; set; }

    /// <summary>Whether wp:anchor uses the simplePos positioning mode.</summary>
    public bool? UsesSimplePosition { get; set; }

    /// <summary>Simple position coordinates from wp:simplePos.</summary>
    public DocumentObjectPoint? SimplePosition { get; set; }

    /// <summary>Opaque Office anchor id from wp14:anchorId.</summary>
    public string? AnchorId { get; set; }

    /// <summary>Opaque Office edit id from wp14:editId.</summary>
    public string? EditId { get; set; }

    /// <summary>Relative width metadata from wp14:sizeRelH.</summary>
    public DocumentObjectRelativeSize? RelativeWidth { get; set; }

    /// <summary>Relative height metadata from wp14:sizeRelV.</summary>
    public DocumentObjectRelativeSize? RelativeHeight { get; set; }

    /// <summary>Raw DrawingML XML fallback for unsupported preserve-only scenarios.</summary>
    public string? RawDrawingXml { get; set; }
}

/// <summary>DOCX image reference mode for a DrawingML blip.</summary>
public enum DocumentDocxImageReferenceMode
{
    /// <summary>The image is embedded as a package part.</summary>
    Embedded,

    /// <summary>The image is referenced by an external relationship.</summary>
    External
}

/// <summary>DOCX picture fill mode for a DrawingML blip fill.</summary>
public enum DocumentDocxBlipFillMode
{
    /// <summary>The picture is stretched to fill its shape rectangle.</summary>
    Stretch,

    /// <summary>The picture uses DrawingML tile fill.</summary>
    Tile,

    /// <summary>The picture fill could not be mapped to a supported fill mode.</summary>
    Unknown
}

/// <summary>Media package metadata for an image referenced by a document drawing.</summary>
public class DocumentImageMediaInfo
{
    /// <summary>Source package part URI that contained the drawing relationship.</summary>
    public string? SourcePartUri { get; set; }

    /// <summary>Image package part URI.</summary>
    public string? ImagePartUri { get; set; }

    /// <summary>Image MIME content type.</summary>
    public string? ContentType { get; set; }

    /// <summary>Original file name when known from import or upload.</summary>
    public string? OriginalFileName { get; set; }

    /// <summary>Normalized file extension including the leading dot when known.</summary>
    public string? Extension { get; set; }
}
