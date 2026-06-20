using System.Text.Json.Serialization;

namespace Tempo.Blazor.DocumentEditor.Models;

/// <summary>Rectangular area in document layout coordinates.</summary>
public class DocumentLayoutRect
{
    /// <summary>X coordinate from the page origin.</summary>
    public double X { get; set; }

    /// <summary>Y coordinate from the page origin.</summary>
    public double Y { get; set; }

    /// <summary>Rectangle width.</summary>
    public double Width { get; set; }

    /// <summary>Rectangle height.</summary>
    public double Height { get; set; }

    /// <summary>Right edge coordinate.</summary>
    [JsonIgnore]
    public double Right => X + Width;

    /// <summary>Bottom edge coordinate.</summary>
    [JsonIgnore]
    public double Bottom => Y + Height;

    /// <summary>Horizontal center coordinate.</summary>
    [JsonIgnore]
    public double CenterX => X + (Width / 2);

    /// <summary>Vertical center coordinate.</summary>
    [JsonIgnore]
    public double CenterY => Y + (Height / 2);

    /// <summary>Whether the rectangle has no usable area.</summary>
    [JsonIgnore]
    public bool IsEmpty => Width <= 0 || Height <= 0;

    /// <summary>Creates a rectangle from left, top, right, and bottom coordinates.</summary>
    public static DocumentLayoutRect FromBounds(double left, double top, double right, double bottom)
        => new()
        {
            X = left,
            Y = top,
            Width = Math.Max(0, right - left),
            Height = Math.Max(0, bottom - top)
        };

    /// <summary>Creates a copy of this rectangle.</summary>
    public DocumentLayoutRect Clone()
        => new()
        {
            X = X,
            Y = Y,
            Width = Width,
            Height = Height
        };
}

/// <summary>Horizontal interval available for text on a line.</summary>
public class DocumentLayoutInterval
{
    /// <summary>Start X coordinate.</summary>
    public double X { get; set; }

    /// <summary>Interval width.</summary>
    public double Width { get; set; }

    /// <summary>End X coordinate.</summary>
    [JsonIgnore]
    public double End => X + Width;
}

/// <summary>Point in document layout coordinates.</summary>
public class DocumentLayoutPoint
{
    /// <summary>X coordinate from the page origin.</summary>
    public double X { get; set; }

    /// <summary>Y coordinate from the page origin.</summary>
    public double Y { get; set; }
}

/// <summary>Full layout snapshot for a document or layout pass.</summary>
public class DocumentPageLayoutSnapshot
{
    /// <summary>Optional source document id.</summary>
    public string? DocumentId { get; set; }

    /// <summary>Layout generation timestamp.</summary>
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Document page boxes in visual order.</summary>
    public List<DocumentPageLayoutBox> Pages { get; set; } = [];

    /// <summary>Debug messages emitted by the layout pass.</summary>
    public List<string> Diagnostics { get; set; } = [];

    /// <summary>Debug metadata for each processed source block.</summary>
    public List<DocumentBlockLayoutDebugInfo> DebugBlockLayouts { get; set; } = [];

    /// <summary>Debug metadata for each positioned object layout.</summary>
    public List<DocumentObjectLayoutDebugInfo> DebugObjectLayouts { get; set; } = [];

    /// <summary>Debug metadata for each visual text line.</summary>
    public List<DocumentLineLayoutDebugInfo> DebugLineLayouts { get; set; } = [];

    /// <summary>Performance and invalidation telemetry for the layout pass.</summary>
    public DocumentLayoutPerformanceMetrics Performance { get; set; } = new();
}

/// <summary>Debug metadata describing how one document block moved the layout cursor.</summary>
public class DocumentBlockLayoutDebugInfo
{
    /// <summary>Source document block id.</summary>
    public string BlockId { get; set; } = string.Empty;

    /// <summary>Source document block type.</summary>
    public DocumentBlockType BlockType { get; set; }

    /// <summary>Source document block order.</summary>
    public double Order { get; set; }

    /// <summary>Page index where the block started layout.</summary>
    public int PageIndex { get; set; }

    /// <summary>Layout cursor Y before the block was processed.</summary>
    public double CurrentYBefore { get; set; }

    /// <summary>Layout cursor Y after the block was processed.</summary>
    public double CurrentYAfter { get; set; }

    /// <summary>Visual start Y used for the block.</summary>
    public double StartY { get; set; }

    /// <summary>Visual end Y reached by the block.</summary>
    public double EndY { get; set; }
}

/// <summary>Debug metadata describing one positioned object and its wrapping footprint.</summary>
public class DocumentObjectLayoutDebugInfo
{
    /// <summary>Layout object id.</summary>
    public string ObjectId { get; set; } = string.Empty;

    /// <summary>Source image block id.</summary>
    public string BlockId { get; set; } = string.Empty;

    /// <summary>Source anchor block id.</summary>
    public string? AnchorBlockId { get; set; }

    /// <summary>Page index containing the object.</summary>
    public int PageIndex { get; set; }

    /// <summary>Image media rectangle.</summary>
    public DocumentLayoutRect MediaRect { get; set; } = new();

    /// <summary>Caption rectangle, if visible.</summary>
    public DocumentLayoutRect CaptionRect { get; set; } = new();

    /// <summary>Media plus caption footprint.</summary>
    public DocumentLayoutRect FootprintRect { get; set; } = new();

    /// <summary>Text wrap rectangle including wrap distances.</summary>
    public DocumentLayoutRect WrapRect { get; set; } = new();

    /// <summary>Wrap mode used by the object.</summary>
    public DocumentWrapMode WrapMode { get; set; }

    /// <summary>Whether the object can overlap same-layer objects.</summary>
    public bool AllowOverlap { get; set; }

    /// <summary>Object z-index in its layout layer.</summary>
    public int ZIndex { get; set; }
}

/// <summary>Debug metadata describing one visual text line and the exclusions used for it.</summary>
public class DocumentLineLayoutDebugInfo
{
    /// <summary>Visual line id.</summary>
    public string LineId { get; set; } = string.Empty;

    /// <summary>Source document block id.</summary>
    public string BlockId { get; set; } = string.Empty;

    /// <summary>Page index containing the line.</summary>
    public int PageIndex { get; set; }

    /// <summary>Zero-based visual line index within the paragraph.</summary>
    public int LineIndex { get; set; }

    /// <summary>Line rectangle.</summary>
    public DocumentLayoutRect LineRect { get; set; } = new();

    /// <summary>Intervals available after subtracting active exclusions.</summary>
    public List<DocumentLayoutInterval> AvailableIntervals { get; set; } = [];

    /// <summary>Exclusion rectangles that overlapped the line's vertical range.</summary>
    public List<DocumentLayoutRect> ExclusionRects { get; set; } = [];

    /// <summary>Text segments placed on this line.</summary>
    public List<DocumentTextSegmentBox> Segments { get; set; } = [];
}

/// <summary>Performance and invalidation telemetry for document layout.</summary>
public class DocumentLayoutPerformanceMetrics
{
    /// <summary>Measured duration of the whole layout pass in milliseconds.</summary>
    public double LayoutPassMs { get; set; }

    /// <summary>Measured duration of a layout reflow caused by image drag in milliseconds.</summary>
    public double ReflowAfterDragMs { get; set; }

    /// <summary>Measured duration of a layout reflow caused by image resize in milliseconds.</summary>
    public double ReflowAfterResizeMs { get; set; }

    /// <summary>Number of pages invalidated by the change that caused this layout pass.</summary>
    public int InvalidatedPageCount { get; set; }

    /// <summary>Zero-based indexes of invalidated pages.</summary>
    public List<int> InvalidatedPageIndices { get; set; } = [];

    /// <summary>Number of text measurements that missed the cache.</summary>
    public int TextMeasureCount { get; set; }

    /// <summary>Number of text measurement cache hits.</summary>
    public int TextMeasureCacheHits { get; set; }

    /// <summary>Number of text measurement cache invalidations.</summary>
    public int TextMeasureInvalidations { get; set; }

    /// <summary>Current text measurement cache entry count.</summary>
    public int TextMeasureCacheSize { get; set; }

    /// <summary>Ratio of text measurement cache hits to all text measurement lookups.</summary>
    public double TextMeasureCacheHitRatio { get; set; }

    /// <summary>Whether the invalidation affects persisted document model data.</summary>
    public bool InvalidatesModel { get; set; }

    /// <summary>Whether the invalidation affects only rendering or measurement state.</summary>
    public bool InvalidatesMeasurementsOnly { get; set; }

    /// <summary>Human-readable reason of the invalidation or layout pass.</summary>
    public string Reason { get; set; } = string.Empty;
}

/// <summary>Layout information for one document page.</summary>
public class DocumentPageLayoutBox
{
    /// <summary>Zero-based page index.</summary>
    public int PageIndex { get; set; }

    /// <summary>One-based page number displayed to users.</summary>
    public int PageNumber { get; set; }

    /// <summary>Whole page rectangle.</summary>
    public DocumentLayoutRect PageRect { get; set; } = new();

    /// <summary>Editable page body rectangle inside margins and header/footer space.</summary>
    public DocumentLayoutRect BodyRect { get; set; } = new();

    /// <summary>Header rectangle.</summary>
    public DocumentLayoutRect HeaderRect { get; set; } = new();

    /// <summary>Footer rectangle.</summary>
    public DocumentLayoutRect FooterRect { get; set; } = new();

    /// <summary>Paragraph boxes placed on this page.</summary>
    public List<DocumentParagraphLayoutBox> Paragraphs { get; set; } = [];

    /// <summary>Positioned object boxes placed on this page.</summary>
    public List<DocumentObjectLayoutBox> Objects { get; set; } = [];

    /// <summary>Text exclusion zones contributed by positioned objects.</summary>
    public List<DocumentExclusionZone> Exclusions { get; set; } = [];
}

/// <summary>Layout box for a document paragraph or paragraph-like block.</summary>
public class DocumentParagraphLayoutBox
{
    /// <summary>Stable layout box id.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Source document block id.</summary>
    public string BlockId { get; set; } = string.Empty;

    /// <summary>Zero-based page index containing the paragraph.</summary>
    public int PageIndex { get; set; }

    /// <summary>Paragraph visual rectangle.</summary>
    public DocumentLayoutRect Rect { get; set; } = new();

    /// <summary>Line boxes belonging to this paragraph.</summary>
    public List<DocumentLineBox> Lines { get; set; } = [];
}

/// <summary>Layout box for one visual text line.</summary>
public class DocumentLineBox
{
    /// <summary>Stable line id.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Source document block id.</summary>
    public string BlockId { get; set; } = string.Empty;

    /// <summary>Zero-based page index containing the line.</summary>
    public int PageIndex { get; set; }

    /// <summary>Zero-based visual line index inside the paragraph.</summary>
    public int LineIndex { get; set; }

    /// <summary>Line visual rectangle.</summary>
    public DocumentLayoutRect Rect { get; set; } = new();

    /// <summary>Baseline Y coordinate used for text painting and caret placement.</summary>
    public double BaselineY { get; set; }

    /// <summary>Horizontal intervals available to this line after exclusions are applied.</summary>
    public List<DocumentLayoutInterval> AvailableIntervals { get; set; } = [];

    /// <summary>Text segments placed on this visual line.</summary>
    public List<DocumentTextSegmentBox> Segments { get; set; } = [];
}

/// <summary>Kind of inline layout segment placed on a visual line.</summary>
public enum DocumentInlineLayoutSegmentKind
{
    /// <summary>Text content measured and painted by the text renderer.</summary>
    Text,

    /// <summary>Inline document object that participates in text flow as a single box.</summary>
    Object
}

/// <summary>Layout box for a text segment, inline object, or inline run fragment.</summary>
public class DocumentTextSegmentBox
{
    /// <summary>Stable segment id.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Kind of inline segment represented by this box.</summary>
    public DocumentInlineLayoutSegmentKind Kind { get; set; } = DocumentInlineLayoutSegmentKind.Text;

    /// <summary>Source document block id.</summary>
    public string BlockId { get; set; } = string.Empty;

    /// <summary>Optional source inline id.</summary>
    public string? InlineId { get; set; }

    /// <summary>Optional object id when this segment represents an inline object.</summary>
    public string? ObjectId { get; set; }

    /// <summary>Zero-based inline index inside the source block.</summary>
    public int InlineIndex { get; set; }

    /// <summary>Character start offset inside the source inline text.</summary>
    public int StartOffset { get; set; }

    /// <summary>Character start offset inside the source block text.</summary>
    public int BlockStartOffset { get; set; }

    /// <summary>Character length inside the source inline text.</summary>
    public int Length { get; set; }

    /// <summary>Debug text content for this segment.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Inline marks that apply to this rendered segment.</summary>
    public List<InlineMark> Marks { get; set; } = [];

    /// <summary>Segment visual rectangle.</summary>
    public DocumentLayoutRect Rect { get; set; } = new();

    /// <summary>Object visual rectangle when this segment represents an inline object.</summary>
    public DocumentLayoutRect? ObjectRect { get; set; }
}

/// <summary>Layout box for an inline, anchored, or fixed document object.</summary>
public class DocumentObjectLayoutBox
{
    /// <summary>Stable layout object id.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Source object block id.</summary>
    public string BlockId { get; set; } = string.Empty;

    /// <summary>Source anchor block id.</summary>
    public string? AnchorBlockId { get; set; }

    /// <summary>Zero-based page index containing the object.</summary>
    public int PageIndex { get; set; }

    /// <summary>Object visual rectangle without wrap distances.</summary>
    public DocumentLayoutRect ObjectRect { get; set; } = new();

    /// <summary>Media rectangle without caption or wrap distances.</summary>
    public DocumentLayoutRect MediaRect { get; set; } = new();

    /// <summary>Caption rectangle when the object renders a visible caption.</summary>
    public DocumentLayoutRect CaptionRect { get; set; } = new();

    /// <summary>Visual footprint rectangle including media and caption, without wrap distances.</summary>
    public DocumentLayoutRect FootprintRect { get; set; } = new();

    /// <summary>Object rectangle expanded by wrap distances.</summary>
    public DocumentLayoutRect WrapRect { get; set; } = new();

    /// <summary>Canonical object layout that produced this box.</summary>
    public DocumentObjectLayout Layout { get; set; } = DocumentObjectLayout.Inline();

    /// <summary>Z-index copied from the object layout for quick sorting and debug rendering.</summary>
    public int ZIndex { get; set; }

    /// <summary>Whether this object is allowed to overlap other objects in the same layout layer.</summary>
    public bool AllowOverlap { get; set; }
}

/// <summary>Area where text cannot be placed because of a wrapping object.</summary>
public class DocumentExclusionZone
{
    /// <summary>Stable exclusion id.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Object layout box id that produced the exclusion.</summary>
    public string ObjectId { get; set; } = string.Empty;

    /// <summary>Source object block id.</summary>
    public string BlockId { get; set; } = string.Empty;

    /// <summary>Zero-based page index containing the exclusion.</summary>
    public int PageIndex { get; set; }

    /// <summary>Wrap mode that produced the exclusion.</summary>
    public DocumentWrapMode WrapMode { get; set; }

    /// <summary>Side where text may flow around the exclusion.</summary>
    public DocumentObjectWrapSide WrapSide { get; set; } = DocumentObjectWrapSide.BothSides;

    /// <summary>Excluded rectangle in page coordinates.</summary>
    public DocumentLayoutRect Rect { get; set; } = new();

    /// <summary>Optional polygonal exclusion contour in page coordinates.</summary>
    public List<DocumentLayoutPoint> Polygon { get; set; } = [];

    /// <summary>Whether this zone actively blocks text placement.</summary>
    public bool BlocksText { get; set; } = true;

    /// <summary>Whether tight or through wrapping is currently represented by a square placeholder.</summary>
    public bool IsContourPlaceholder { get; set; }
}

/// <summary>Text caret position projected into layout coordinates.</summary>
public class DocumentCaretPosition
{
    /// <summary>Zero-based page index.</summary>
    public int PageIndex { get; set; }

    /// <summary>Source document block id.</summary>
    public string BlockId { get; set; } = string.Empty;

    /// <summary>Optional visual line id.</summary>
    public string? LineId { get; set; }

    /// <summary>Optional text segment id.</summary>
    public string? SegmentId { get; set; }

    /// <summary>Character offset in the source inline or block text.</summary>
    public int Offset { get; set; }

    /// <summary>Caret X coordinate.</summary>
    public double X { get; set; }

    /// <summary>Caret Y coordinate.</summary>
    public double Y { get; set; }

    /// <summary>Caret height.</summary>
    public double Height { get; set; }

    /// <summary>Visual affinity used at line boundaries.</summary>
    public DocumentCaretAffinity Affinity { get; set; } = DocumentCaretAffinity.Downstream;
}

/// <summary>Visual caret affinity at ambiguous text positions.</summary>
public enum DocumentCaretAffinity
{
    /// <summary>Caret prefers the preceding visual position.</summary>
    Upstream,

    /// <summary>Caret prefers the following visual position.</summary>
    Downstream
}

/// <summary>Kind of layout item hit by a pointer coordinate.</summary>
public enum DocumentLayoutHitTargetKind
{
    /// <summary>No layout target was hit.</summary>
    None,

    /// <summary>Page background or margin was hit.</summary>
    Page,

    /// <summary>Editable body area was hit.</summary>
    Body,

    /// <summary>Paragraph area was hit.</summary>
    Paragraph,

    /// <summary>Text line was hit.</summary>
    Line,

    /// <summary>Text segment was hit.</summary>
    TextSegment,

    /// <summary>Document object was hit.</summary>
    Object
}

/// <summary>Result of hit-testing layout coordinates.</summary>
public class DocumentLayoutHitTarget
{
    /// <summary>Hit target kind.</summary>
    public DocumentLayoutHitTargetKind Kind { get; set; } = DocumentLayoutHitTargetKind.None;

    /// <summary>Zero-based page index.</summary>
    public int PageIndex { get; set; }

    /// <summary>Optional source block id.</summary>
    public string? BlockId { get; set; }

    /// <summary>Optional visual line id.</summary>
    public string? LineId { get; set; }

    /// <summary>Optional text segment id.</summary>
    public string? SegmentId { get; set; }

    /// <summary>Optional object id.</summary>
    public string? ObjectId { get; set; }

    /// <summary>Hit X coordinate.</summary>
    public double X { get; set; }

    /// <summary>Hit Y coordinate.</summary>
    public double Y { get; set; }

    /// <summary>Optional caret position resolved from the hit target.</summary>
    public DocumentCaretPosition? Caret { get; set; }
}

/// <summary>Serializable debug snapshot with primitive layout counts and full page boxes.</summary>
public class DocumentPageLayoutDebugSnapshot
{
    /// <summary>Optional source document id.</summary>
    public string? DocumentId { get; set; }

    /// <summary>Number of pages in the snapshot.</summary>
    public int PageCount { get; set; }

    /// <summary>Total paragraph count.</summary>
    public int ParagraphCount { get; set; }

    /// <summary>Total line count.</summary>
    public int LineCount { get; set; }

    /// <summary>Total text segment count.</summary>
    public int SegmentCount { get; set; }

    /// <summary>Total object count.</summary>
    public int ObjectCount { get; set; }

    /// <summary>Total exclusion zone count.</summary>
    public int ExclusionCount { get; set; }

    /// <summary>Full serializable page layout boxes for visual inspection.</summary>
    public List<DocumentPageLayoutBox> Pages { get; set; } = [];

    /// <summary>Debug diagnostics copied from the layout snapshot.</summary>
    public List<string> Diagnostics { get; set; } = [];

    /// <summary>Performance and invalidation telemetry copied from the layout snapshot.</summary>
    public DocumentLayoutPerformanceMetrics Performance { get; set; } = new();

    /// <summary>Debug metadata for source blocks.</summary>
    public List<DocumentBlockLayoutDebugInfo> DebugBlockLayouts { get; set; } = [];

    /// <summary>Debug metadata for positioned objects.</summary>
    public List<DocumentObjectLayoutDebugInfo> DebugObjectLayouts { get; set; } = [];

    /// <summary>Debug metadata for visual text lines.</summary>
    public List<DocumentLineLayoutDebugInfo> DebugLineLayouts { get; set; } = [];

    /// <summary>Creates a debug snapshot from a layout snapshot.</summary>
    public static DocumentPageLayoutDebugSnapshot FromSnapshot(DocumentPageLayoutSnapshot snapshot)
        => new()
        {
            DocumentId = snapshot.DocumentId,
            PageCount = snapshot.Pages.Count,
            ParagraphCount = snapshot.Pages.Sum(page => page.Paragraphs.Count),
            LineCount = snapshot.Pages.Sum(page => page.Paragraphs.Sum(paragraph => paragraph.Lines.Count)),
            SegmentCount = snapshot.Pages.Sum(page => page.Paragraphs.Sum(paragraph => paragraph.Lines.Sum(line => line.Segments.Count))),
            ObjectCount = snapshot.Pages.Sum(page => page.Objects.Count),
            ExclusionCount = snapshot.Pages.Sum(page => page.Exclusions.Count),
            Pages = snapshot.Pages,
            Diagnostics = snapshot.Diagnostics,
            Performance = snapshot.Performance,
            DebugBlockLayouts = snapshot.DebugBlockLayouts,
            DebugObjectLayouts = snapshot.DebugObjectLayouts,
            DebugLineLayouts = snapshot.DebugLineLayouts
        };
}
