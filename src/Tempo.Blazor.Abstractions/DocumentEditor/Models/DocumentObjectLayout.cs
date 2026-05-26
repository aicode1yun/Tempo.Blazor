using System.Text.Json.Serialization;

namespace Tempo.Blazor.DocumentEditor.Models;

/// <summary>Object layout mode used by images and other positioned document objects.</summary>
public enum DocumentObjectLayoutKind
{
    /// <summary>The object participates directly in the text flow.</summary>
    Inline,

    /// <summary>The object is anchored to document content and can affect text flow.</summary>
    Anchored,

    /// <summary>The object is fixed on a page and does not move with surrounding text.</summary>
    Fixed
}

/// <summary>Vertical alignment preset for a positioned document object.</summary>
public enum DocumentObjectVerticalAlignment
{
    /// <summary>No preset vertical alignment.</summary>
    None,

    /// <summary>Align to the top of the reference frame.</summary>
    Top,

    /// <summary>Align to the vertical center of the reference frame.</summary>
    Middle,

    /// <summary>Align to the bottom of the reference frame.</summary>
    Bottom
}

/// <summary>Complete layout metadata for an image or another document object.</summary>
public class DocumentObjectLayout
{
    /// <summary>Layout kind.</summary>
    public DocumentObjectLayoutKind Kind { get; set; } = DocumentObjectLayoutKind.Inline;

    /// <summary>Anchor behavior and anchor identity.</summary>
    public DocumentObjectAnchor Anchor { get; set; } = new();

    /// <summary>Position relative to page, margin, column, paragraph, character, or line.</summary>
    public DocumentObjectPosition Position { get; set; } = new();

    /// <summary>Text wrapping behavior and wrap distances.</summary>
    public DocumentObjectWrap Wrap { get; set; } = new();

    /// <summary>Object transform including user-controlled size, rotation, and crop.</summary>
    public DocumentObjectTransform Transform { get; set; } = new();

    /// <summary>Z-order and overlap behavior.</summary>
    public DocumentObjectStacking Stacking { get; set; } = new();

    /// <summary>Whether this layout behaves as an inline object.</summary>
    [JsonIgnore]
    public bool IsInline => Kind == DocumentObjectLayoutKind.Inline || Wrap.Mode == DocumentWrapMode.Inline;

    /// <summary>Creates the default inline layout.</summary>
    public static DocumentObjectLayout Inline() => new();

    /// <summary>Creates an anchored layout with the given wrap mode.</summary>
    public static DocumentObjectLayout Anchored(
        DocumentWrapMode wrapMode,
        DocumentImageHorizontalPosition? horizontalAlignment = null)
        => new()
        {
            Kind = DocumentObjectLayoutKind.Anchored,
            Position = new DocumentObjectPosition
            {
                HorizontalAlignment = horizontalAlignment
            },
            Wrap = new DocumentObjectWrap
            {
                Mode = wrapMode
            }
        };

    /// <summary>Creates a fixed page layout with no text wrapping.</summary>
    public static DocumentObjectLayout Fixed()
        => new()
        {
            Kind = DocumentObjectLayoutKind.Fixed,
            Anchor = new DocumentObjectAnchor
            {
                MoveWithText = false,
                FixedOnPage = true
            },
            Wrap = new DocumentObjectWrap
            {
                Mode = DocumentWrapMode.InFrontOfText
            }
        };

    /// <summary>Creates a new object layout from the previous floating layout DTO.</summary>
    public static DocumentObjectLayout FromFloatingLayout(DocumentFloatingLayout? layout)
    {
        if (layout is null)
        {
            return Inline();
        }

        return new DocumentObjectLayout
        {
            Kind = layout.Inline
                ? DocumentObjectLayoutKind.Inline
                : DocumentObjectLayoutKind.Anchored,
            Anchor = new DocumentObjectAnchor
            {
                LockAnchor = layout.LockAnchor,
                MoveWithText = !layout.Inline,
                FixedOnPage = false
            },
            Position = new DocumentObjectPosition
            {
                HorizontalRelativeTo = layout.HorizontalRelativeTo,
                VerticalRelativeTo = layout.VerticalRelativeTo,
                X = layout.X,
                Y = layout.Y,
                HorizontalAlignment = layout.HorizontalPosition
            },
            Wrap = new DocumentObjectWrap
            {
                Mode = layout.WrapMode,
                DistanceLeft = layout.DistanceLeft,
                DistanceRight = layout.DistanceRight,
                DistanceTop = layout.DistanceTop,
                DistanceBottom = layout.DistanceBottom
            },
            Stacking = new DocumentObjectStacking
            {
                ZIndex = layout.ZIndex
            }
        };
    }

    /// <summary>Creates a previous-layout DTO for integrations that have not moved to the nested model yet.</summary>
    public DocumentFloatingLayout ToFloatingLayout()
        => new()
        {
            Inline = IsInline,
            HorizontalRelativeTo = Position.HorizontalRelativeTo,
            VerticalRelativeTo = Position.VerticalRelativeTo,
            X = Position.X,
            Y = Position.Y,
            WrapMode = Wrap.Mode,
            ZIndex = Stacking.ZIndex,
            LockAnchor = Anchor.LockAnchor,
            HorizontalPosition = Position.HorizontalAlignment,
            DistanceLeft = Wrap.DistanceLeft,
            DistanceRight = Wrap.DistanceRight,
            DistanceTop = Wrap.DistanceTop,
            DistanceBottom = Wrap.DistanceBottom
        };
}

/// <summary>Anchor behavior for a positioned document object.</summary>
public class DocumentObjectAnchor
{
    /// <summary>Optional block id that owns this object anchor.</summary>
    public string? BlockId { get; set; }

    /// <summary>Optional inline index when the object is anchored inside inline content.</summary>
    public int? InlineIndex { get; set; }

    /// <summary>Optional character offset when the object is anchored inside a text run.</summary>
    public int? Offset { get; set; }

    /// <summary>Document region that owns this anchor.</summary>
    public DocumentRenditionAnchorScope Region { get; set; } = DocumentRenditionAnchorScope.Body;

    /// <summary>Optional table id when the anchor points inside a table cell.</summary>
    public string? TableId { get; set; }

    /// <summary>Optional table cell id when the anchor points inside a table cell.</summary>
    public string? CellId { get; set; }

    /// <summary>Optional header/footer id when the anchor points inside page header or footer content.</summary>
    public string? HeaderFooterId { get; set; }

    /// <summary>Zero-based page index computed by the current layout pass for diagnostics.</summary>
    [JsonIgnore]
    public int? PageIndex { get; set; }

    /// <summary>Whether the object should move when its anchor text moves.</summary>
    public bool MoveWithText { get; set; } = true;

    /// <summary>Whether the object is fixed on the page instead of moving with text.</summary>
    public bool FixedOnPage { get; set; }

    /// <summary>Whether the user is prevented from changing the current anchor.</summary>
    public bool LockAnchor { get; set; }
}

/// <summary>Relative position of a document object.</summary>
public class DocumentObjectPosition
{
    /// <summary>Horizontal reference frame.</summary>
    public DocumentRelativePosition HorizontalRelativeTo { get; set; } = DocumentRelativePosition.Page;

    /// <summary>Vertical reference frame.</summary>
    public DocumentRelativePosition VerticalRelativeTo { get; set; } = DocumentRelativePosition.Paragraph;

    /// <summary>Horizontal offset from the reference frame.</summary>
    public double X { get; set; }

    /// <summary>Vertical offset from the reference frame.</summary>
    public double Y { get; set; }

    /// <summary>Optional horizontal alignment preset.</summary>
    public DocumentImageHorizontalPosition? HorizontalAlignment { get; set; }

    /// <summary>Optional vertical alignment preset.</summary>
    public DocumentObjectVerticalAlignment VerticalAlignment { get; set; } = DocumentObjectVerticalAlignment.None;
}

/// <summary>Text wrapping settings for a document object.</summary>
public class DocumentObjectWrap
{
    /// <summary>Text wrapping mode.</summary>
    public DocumentWrapMode Mode { get; set; } = DocumentWrapMode.Inline;

    /// <summary>Side of the object where surrounding text is allowed to flow.</summary>
    public DocumentObjectWrapSide Side { get; set; } = DocumentObjectWrapSide.BothSides;

    /// <summary>Distance from object to surrounding text on the left side.</summary>
    public double DistanceLeft { get; set; }

    /// <summary>Distance from object to surrounding text on the right side.</summary>
    public double DistanceRight { get; set; }

    /// <summary>Distance from object to surrounding text on the top side.</summary>
    public double DistanceTop { get; set; }

    /// <summary>Distance from object to surrounding text on the bottom side.</summary>
    public double DistanceBottom { get; set; }

    /// <summary>Optional custom contour points used by tight or through wrapping.</summary>
    public List<DocumentObjectWrapPoint> WrapContourPoints { get; set; } = [];
}

/// <summary>A point in an object wrap contour.</summary>
public class DocumentObjectWrapPoint
{
    /// <summary>X coordinate relative to the object bounds.</summary>
    public double X { get; set; }

    /// <summary>Y coordinate relative to the object bounds.</summary>
    public double Y { get; set; }
}

/// <summary>Text wrapping side for positioned document objects.</summary>
public enum DocumentObjectWrapSide
{
    /// <summary>Allow text on both sides of the object.</summary>
    BothSides,

    /// <summary>Allow text only on the left side of the object.</summary>
    Left,

    /// <summary>Allow text only on the right side of the object.</summary>
    Right,

    /// <summary>Allow text on the side with the most available space.</summary>
    Largest
}

/// <summary>A normalized point in an object wrap contour.</summary>
public class WrapContourPoint : DocumentObjectWrapPoint
{
}

/// <summary>User-controlled transform of a document object.</summary>
public class DocumentObjectTransform
{
    /// <summary>User-set width in renderer units.</summary>
    public double? Width { get; set; }

    /// <summary>User-set height in renderer units.</summary>
    public double? Height { get; set; }

    /// <summary>Natural source width reported by the image asset.</summary>
    public double? NaturalWidth { get; set; }

    /// <summary>Natural source height reported by the image asset.</summary>
    public double? NaturalHeight { get; set; }

    /// <summary>Whether aspect ratio should be preserved during resize.</summary>
    public bool LockAspectRatio { get; set; } = true;

    /// <summary>Clockwise object rotation in degrees.</summary>
    public double Rotation { get; set; }

    /// <summary>Crop rectangle expressed as distances from object edges.</summary>
    public DocumentObjectCrop Crop { get; set; } = new();

    /// <summary>Optional horizontal and vertical flip flags for the transformed object.</summary>
    public DocumentObjectFlip? Flip { get; set; }
}

/// <summary>Crop rectangle for a document object, expressed as normalized percentages from each object edge.</summary>
public class DocumentObjectCrop
{
    /// <summary>Crop percentage from the left edge.</summary>
    public double Left { get; set; }

    /// <summary>Crop percentage from the top edge.</summary>
    public double Top { get; set; }

    /// <summary>Crop percentage from the right edge.</summary>
    public double Right { get; set; }

    /// <summary>Crop percentage from the bottom edge.</summary>
    public double Bottom { get; set; }
}

/// <summary>Horizontal and vertical flip flags for a document object transform.</summary>
public class DocumentObjectFlip
{
    /// <summary>Whether the object is mirrored horizontally.</summary>
    public bool Horizontal { get; set; }

    /// <summary>Whether the object is mirrored vertically.</summary>
    public bool Vertical { get; set; }
}

/// <summary>Effect extent around a DrawingML object expressed in EMUs.</summary>
public class DocumentObjectEffectExtent
{
    /// <summary>Left effect extent in EMUs.</summary>
    public long Left { get; set; }

    /// <summary>Top effect extent in EMUs.</summary>
    public long Top { get; set; }

    /// <summary>Right effect extent in EMUs.</summary>
    public long Right { get; set; }

    /// <summary>Bottom effect extent in EMUs.</summary>
    public long Bottom { get; set; }
}

/// <summary>Absolute DrawingML point expressed in EMUs.</summary>
public class DocumentObjectPoint
{
    /// <summary>X coordinate in EMUs.</summary>
    public long X { get; set; }

    /// <summary>Y coordinate in EMUs.</summary>
    public long Y { get; set; }
}

/// <summary>Relative DrawingML size metadata from wp14:sizeRelH or wp14:sizeRelV.</summary>
public class DocumentObjectRelativeSize
{
    /// <summary>WordprocessingML relative reference, for example page, margin, or paragraph.</summary>
    public string? RelativeFrom { get; set; }

    /// <summary>Normalized percentage value when it can be parsed from the source XML.</summary>
    public double? Percent { get; set; }

    /// <summary>Raw percentage value from the source XML, preserved for exact DOCX roundtrips.</summary>
    public string? RawValue { get; set; }
}

/// <summary>Stacking behavior for positioned document objects.</summary>
public class DocumentObjectStacking
{
    /// <summary>Z-order for multiple objects.</summary>
    public int ZIndex { get; set; }

    /// <summary>Whether the object may overlap other floating objects.</summary>
    public bool AllowOverlap { get; set; }
}
