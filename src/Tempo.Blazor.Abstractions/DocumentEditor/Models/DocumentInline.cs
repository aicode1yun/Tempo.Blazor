using System.Text.Json.Serialization;

namespace Tempo.Blazor.DocumentEditor.Models;

/// <summary>Base class for inline content inside text-based document blocks.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(TextRun), "text")]
[JsonDerivedType(typeof(TokenRun), "token")]
[JsonDerivedType(typeof(DocumentFieldRun), "field")]
[JsonDerivedType(typeof(DocumentNoteReferenceRun), "noteReference")]
[JsonDerivedType(typeof(DocumentDrawingRun), "drawing")]
[JsonDerivedType(typeof(DocumentMathRun), "math")]
[JsonDerivedType(typeof(DocumentContentControlRun), "contentControl")]
[JsonDerivedType(typeof(DocumentSigningFieldRun), "signingField")]
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

/// <summary>
/// Inline signing field placed in the document (plan S2). An atomic box a signer later fills; its
/// areas are derived from the layout (body field → one, header/footer field → one per page).
/// </summary>
public class DocumentSigningFieldRun : InlineContent
{
    /// <summary>Stable signing field identifier (shared across all areas/pages of the field).</summary>
    public string Uuid { get; set; } = string.Empty;

    /// <summary>Field type name (camelCase, mirrors <c>SigningFieldType</c>).</summary>
    public string FieldType { get; set; } = "text";

    /// <summary>Signer role identifier the field belongs to.</summary>
    public string SubmitterUuid { get; set; } = string.Empty;

    /// <summary>Whether the signer must provide a value.</summary>
    public bool Required { get; set; }

    /// <summary>User-facing field label.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Inline box width in document units.</summary>
    public double BoxWidth { get; set; }

    /// <summary>Inline box height in document units.</summary>
    public double BoxHeight { get; set; }
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

    /// <summary>Shape geometry for auto-shapes, freeform shapes, text boxes, lines, connectors, and groups.</summary>
    public DocumentDrawingShape? Shape { get; set; }

    /// <summary>Text body hosted by a shape or text box.</summary>
    public DocumentDrawingTextBody? TextBody { get; set; }

    /// <summary>Chart payload hosted by this drawing.</summary>
    public DocumentDrawingChart? Chart { get; set; }

    /// <summary>Grouped child drawing references and group transform metadata.</summary>
    public DocumentDrawingGroup? Group { get; set; }

    /// <summary>Optional DOCX DrawingML metadata used for high-fidelity import, export, and preserve-only roundtrips.</summary>
    public DocumentDocxDrawingMetadata? Docx { get; set; }

    /// <summary>Supplemental drawing metadata used by importers, exporters, and editor runtime migrations.</summary>
    public Dictionary<string, string?> Metadata { get; set; } = [];
}

/// <summary>Structured mathematical equation run using a clean-room OMML-like tree.</summary>
public class DocumentMathRun : InlineContent
{
    /// <summary>Stable equation identifier.</summary>
    public string MathId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Whether the equation is laid out as inline text or display math.</summary>
    public DocumentMathDisplayMode DisplayMode { get; set; } = DocumentMathDisplayMode.Inline;

    /// <summary>Root math content sequence.</summary>
    public DocumentMathContent Content { get; set; } = new();

    /// <summary>Accessible spoken or textual description.</summary>
    public string? AltText { get; set; }

    /// <summary>Optional MathML mirror generated by importers or exporters.</summary>
    public string? MathML { get; set; }

    /// <summary>Optional OMML XML payload for high-fidelity provider or DOCX roundtrips.</summary>
    public string? OmmlXml { get; set; }

    /// <summary>Preserved metadata for unknown math properties.</summary>
    public Dictionary<string, string?> Metadata { get; set; } = [];
}

/// <summary>Equation display mode.</summary>
public enum DocumentMathDisplayMode
{
    /// <summary>Equation participates in the current text line.</summary>
    Inline,

    /// <summary>Equation is centered and laid out as its own display item.</summary>
    Display
}

/// <summary>Sequence of math elements.</summary>
public sealed class DocumentMathContent
{
    /// <summary>Math elements in visual order.</summary>
    public List<DocumentMathElement> Elements { get; set; } = [];
}

/// <summary>Math element in an equation tree.</summary>
public sealed class DocumentMathElement
{
    /// <summary>Element type such as run, fraction, radical, sup, sub, subSup, nary, delimiter, function, accent, bar, limit, matrix, box, or borderBox.</summary>
    public string Type { get; set; } = "run";

    /// <summary>Text for run, operator, accent, delimiter, or function-name elements.</summary>
    public string? Text { get; set; }

    /// <summary>Math style such as normal, italic, bold, or boldItalic.</summary>
    public string Style { get; set; } = "italic";

    /// <summary>Fraction style such as bar, skewed, linear, or noBar.</summary>
    public string FractionType { get; set; } = "bar";

    /// <summary>Optional opening delimiter.</summary>
    public string? Open { get; set; }

    /// <summary>Optional closing delimiter.</summary>
    public string? Close { get; set; }

    /// <summary>Optional separator delimiter.</summary>
    public string? Separator { get; set; }

    /// <summary>Optional n-ary operator such as ∑, ∏, or ∫.</summary>
    public string? Operator { get; set; }

    /// <summary>Whether n-ary limits are placed above and below the operator in display style.</summary>
    public bool LimitsAboveBelow { get; set; } = true;

    /// <summary>Optional accent character.</summary>
    public string? Accent { get; set; }

    /// <summary>Optional bar position such as over or under.</summary>
    public string? Position { get; set; }

    /// <summary>Base slot.</summary>
    public DocumentMathContent? Base { get; set; }

    /// <summary>Numerator slot.</summary>
    public DocumentMathContent? Numerator { get; set; }

    /// <summary>Denominator slot.</summary>
    public DocumentMathContent? Denominator { get; set; }

    /// <summary>Radicand slot.</summary>
    public DocumentMathContent? Radicand { get; set; }

    /// <summary>Degree slot for radicals.</summary>
    public DocumentMathContent? Degree { get; set; }

    /// <summary>Superscript slot.</summary>
    public DocumentMathContent? Superscript { get; set; }

    /// <summary>Subscript slot.</summary>
    public DocumentMathContent? Subscript { get; set; }

    /// <summary>Lower limit slot.</summary>
    public DocumentMathContent? LowerLimit { get; set; }

    /// <summary>Upper limit slot.</summary>
    public DocumentMathContent? UpperLimit { get; set; }

    /// <summary>Function name slot.</summary>
    public DocumentMathContent? FunctionName { get; set; }

    /// <summary>Generic content slot.</summary>
    public DocumentMathContent? Content { get; set; }

    /// <summary>Matrix rows.</summary>
    public List<DocumentMathMatrixRow> Rows { get; set; } = [];

    /// <summary>Preserved metadata for unknown element properties.</summary>
    public Dictionary<string, string?> Metadata { get; set; } = [];
}

/// <summary>Matrix row in a math equation.</summary>
public sealed class DocumentMathMatrixRow
{
    /// <summary>Cells in visual order.</summary>
    public List<DocumentMathContent> Cells { get; set; } = [];
}

/// <summary>Drawing object kinds supported by document inline content.</summary>
public enum DocumentDrawingKind
{
    /// <summary>Bitmap or vector image drawing.</summary>
    Image,

    /// <summary>Preset or freeform vector shape.</summary>
    Shape,

    /// <summary>Shape with editable text body.</summary>
    TextBox,

    /// <summary>Line drawing with optional arrowheads.</summary>
    Line,

    /// <summary>Connector line that can reference other drawing objects.</summary>
    Connector,

    /// <summary>Embedded chart drawing.</summary>
    Chart,

    /// <summary>Group of drawings with a shared transform.</summary>
    Group
}

/// <summary>Vector drawing shape definition.</summary>
public sealed class DocumentDrawingShape
{
    /// <summary>Preset geometry name such as rectangle, roundRectangle, ellipse, triangle, diamond, star, rightArrow, line, or connector.</summary>
    public string Preset { get; set; } = "rectangle";

    /// <summary>Fill style applied to closed shapes.</summary>
    public DocumentDrawingFill Fill { get; set; } = new();

    /// <summary>Stroke style applied to shape outlines and lines.</summary>
    public DocumentDrawingStroke Stroke { get; set; } = new();

    /// <summary>Optional shadow effect.</summary>
    public DocumentDrawingShadow? Shadow { get; set; }

    /// <summary>Shape rotation in degrees.</summary>
    public double Rotation { get; set; }

    /// <summary>Optional geometry adjustment values keyed by preset-specific names.</summary>
    public Dictionary<string, double> Adjustments { get; set; } = [];

    /// <summary>Optional connector start binding.</summary>
    public DocumentDrawingConnection? StartConnection { get; set; }

    /// <summary>Optional connector end binding.</summary>
    public DocumentDrawingConnection? EndConnection { get; set; }

    /// <summary>Optional connector control points in local shape coordinates.</summary>
    public List<DocumentDrawingPoint> Points { get; set; } = [];
}

/// <summary>Fill style for vector drawings.</summary>
public sealed class DocumentDrawingFill
{
    /// <summary>Fill type such as solid, none, linearGradient, or pattern.</summary>
    public string Type { get; set; } = "solid";

    /// <summary>Primary CSS color value.</summary>
    public string Color { get; set; } = "#ffffff";

    /// <summary>Secondary CSS color value for gradients and patterns.</summary>
    public string? SecondaryColor { get; set; }

    /// <summary>Fill opacity from 0 to 1.</summary>
    public double Opacity { get; set; } = 1;

    /// <summary>Gradient angle in degrees.</summary>
    public double Angle { get; set; }
}

/// <summary>Stroke style for vector drawings.</summary>
public sealed class DocumentDrawingStroke
{
    /// <summary>Stroke CSS color value.</summary>
    public string Color { get; set; } = "#64748b";

    /// <summary>Stroke width in CSS pixels.</summary>
    public double Width { get; set; } = 1.5;

    /// <summary>Dash style such as solid, dash, dot, or dashDot.</summary>
    public string Dash { get; set; } = "solid";

    /// <summary>Stroke opacity from 0 to 1.</summary>
    public double Opacity { get; set; } = 1;

    /// <summary>Line cap such as butt, round, or square.</summary>
    public string LineCap { get; set; } = "round";

    /// <summary>Line join such as miter, round, or bevel.</summary>
    public string LineJoin { get; set; } = "round";

    /// <summary>Optional arrowhead at the start of a line or connector.</summary>
    public string? StartArrow { get; set; }

    /// <summary>Optional arrowhead at the end of a line or connector.</summary>
    public string? EndArrow { get; set; }
}

/// <summary>Shadow effect for vector drawings.</summary>
public sealed class DocumentDrawingShadow
{
    /// <summary>Shadow CSS color value.</summary>
    public string Color { get; set; } = "rgba(15, 23, 42, 0.22)";

    /// <summary>Horizontal shadow offset in CSS pixels.</summary>
    public double OffsetX { get; set; } = 0;

    /// <summary>Vertical shadow offset in CSS pixels.</summary>
    public double OffsetY { get; set; } = 2;

    /// <summary>Shadow blur radius in CSS pixels.</summary>
    public double Blur { get; set; } = 6;
}

/// <summary>Point in local drawing coordinates.</summary>
public sealed class DocumentDrawingPoint
{
    /// <summary>X coordinate.</summary>
    public double X { get; set; }

    /// <summary>Y coordinate.</summary>
    public double Y { get; set; }
}

/// <summary>Connector binding to another drawing object.</summary>
public sealed class DocumentDrawingConnection
{
    /// <summary>Target drawing object id.</summary>
    public string ObjectId { get; set; } = string.Empty;

    /// <summary>Connection site key such as top, right, bottom, left, or center.</summary>
    public string Site { get; set; } = "center";
}

/// <summary>Text body hosted inside a shape or text box.</summary>
public sealed class DocumentDrawingTextBody
{
    /// <summary>Paragraphs inside the drawing text body.</summary>
    public List<DocumentDrawingTextParagraph> Paragraphs { get; set; } = [];

    /// <summary>Text inset from the left edge in CSS pixels.</summary>
    public double InsetLeft { get; set; } = 8;

    /// <summary>Text inset from the top edge in CSS pixels.</summary>
    public double InsetTop { get; set; } = 6;

    /// <summary>Text inset from the right edge in CSS pixels.</summary>
    public double InsetRight { get; set; } = 8;

    /// <summary>Text inset from the bottom edge in CSS pixels.</summary>
    public double InsetBottom { get; set; } = 6;

    /// <summary>Vertical alignment such as top, middle, or bottom.</summary>
    public string VerticalAlignment { get; set; } = "top";

    /// <summary>Whether text wraps inside the drawing bounds.</summary>
    public bool WrapText { get; set; } = true;

    /// <summary>Optional text auto-fit mode such as none, shrinkText, or resizeShape.</summary>
    public string AutoFit { get; set; } = "none";
}

/// <summary>Paragraph inside a drawing text body.</summary>
public sealed class DocumentDrawingTextParagraph
{
    /// <summary>Paragraph text.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Text style.</summary>
    public DocumentDrawingTextStyle Style { get; set; } = new();

    /// <summary>Paragraph alignment such as left, center, right, or justify.</summary>
    public string Alignment { get; set; } = "left";
}

/// <summary>Text style inside a drawing text body.</summary>
public sealed class DocumentDrawingTextStyle
{
    /// <summary>Font family.</summary>
    public string FontFamily { get; set; } = "Aptos, Arial, sans-serif";

    /// <summary>Font size in CSS pixels.</summary>
    public double FontSize { get; set; } = 14;

    /// <summary>CSS color value.</summary>
    public string Color { get; set; } = "#0f172a";

    /// <summary>Whether the text is bold.</summary>
    public bool Bold { get; set; }

    /// <summary>Whether the text is italic.</summary>
    public bool Italic { get; set; }
}

/// <summary>Embedded chart payload.</summary>
public sealed class DocumentDrawingChart
{
    /// <summary>Chart type such as bar, line, pie, or doughnut.</summary>
    public string Type { get; set; } = "bar";

    /// <summary>Optional chart title.</summary>
    public string? Title { get; set; }

    /// <summary>Category labels.</summary>
    public List<string> Categories { get; set; } = [];

    /// <summary>Chart series.</summary>
    public List<DocumentDrawingChartSeries> Series { get; set; } = [];

    /// <summary>Whether to show the legend.</summary>
    public bool ShowLegend { get; set; } = true;

    /// <summary>Chart palette colors.</summary>
    public List<string> Palette { get; set; } = [];
}

/// <summary>Chart data series.</summary>
public sealed class DocumentDrawingChartSeries
{
    /// <summary>Series name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Series values.</summary>
    public List<double> Values { get; set; } = [];

    /// <summary>Optional series color.</summary>
    public string? Color { get; set; }
}

/// <summary>Grouped drawing metadata.</summary>
public sealed class DocumentDrawingGroup
{
    /// <summary>Child drawing object ids in visual order.</summary>
    public List<string> ChildObjectIds { get; set; } = [];

    /// <summary>Local group coordinate origin.</summary>
    public DocumentDrawingPoint Origin { get; set; } = new();

    /// <summary>Group coordinate size.</summary>
    public DocumentDrawingPoint Size { get; set; } = new();
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

/// <summary>Inline structured document tag or form control run.</summary>
public class DocumentContentControlRun : InlineContent
{
    /// <summary>Structured document tag metadata and value.</summary>
    public DocumentContentControl Control { get; set; } = new();

    /// <summary>Rich inline content owned by rich-text content controls.</summary>
    public List<InlineContent> Inlines { get; set; } = [];
}

/// <summary>Structured document tag metadata and value shared by inline and block controls.</summary>
public class DocumentContentControl
{
    /// <summary>Stable structured document tag identifier.</summary>
    public string ControlId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Content control kind.</summary>
    public DocumentContentControlKind Kind { get; set; } = DocumentContentControlKind.PlainText;

    /// <summary>Content control scope.</summary>
    public DocumentContentControlScope Scope { get; set; } = DocumentContentControlScope.Inline;

    /// <summary>User-facing content control alias.</summary>
    public string? Alias { get; set; }

    /// <summary>Machine-readable content control tag.</summary>
    public string? Tag { get; set; }

    /// <summary>Placeholder text rendered when the value is empty.</summary>
    public string? PlaceholderText { get; set; }

    /// <summary>Whether the field must contain a value before the form is considered complete.</summary>
    public bool IsRequired { get; set; }

    /// <summary>Whether changing the control value is locked.</summary>
    public bool LockContent { get; set; }

    /// <summary>Whether deleting the control is locked.</summary>
    public bool LockDeletion { get; set; }

    /// <summary>Optional value format mask, for example a date or phone pattern.</summary>
    public string? FormatMask { get; set; }

    /// <summary>Current content control value.</summary>
    public DocumentContentControlValue Value { get; set; } = new();

    /// <summary>Choices for combo box and drop-down controls.</summary>
    public List<DocumentContentControlItem> Items { get; set; } = [];

    /// <summary>Supplemental control metadata preserved across runtimes.</summary>
    public Dictionary<string, string?> Metadata { get; set; } = [];
}

/// <summary>Supported structured document tag kinds.</summary>
public enum DocumentContentControlKind
{
    /// <summary>Single plain-text value.</summary>
    PlainText,

    /// <summary>Rich text content containing nested inline or block content.</summary>
    RichText,

    /// <summary>Editable combo box with optional predefined values.</summary>
    ComboBox,

    /// <summary>Drop-down list with predefined values.</summary>
    DropDown,

    /// <summary>Date picker value.</summary>
    Date,

    /// <summary>Boolean checkbox value.</summary>
    Checkbox,

    /// <summary>Picture placeholder or selected image.</summary>
    Picture,

    /// <summary>Repeating section containing a set of block items.</summary>
    RepeatingSection
}

/// <summary>Structured document tag scope.</summary>
public enum DocumentContentControlScope
{
    /// <summary>Inline control embedded in text.</summary>
    Inline,

    /// <summary>Block-level control containing document blocks.</summary>
    Block
}

/// <summary>Current value of a structured document tag.</summary>
public class DocumentContentControlValue
{
    /// <summary>Text value for plain text, rich text fallback, combo, drop-down, and date controls.</summary>
    public string? Text { get; set; }

    /// <summary>Selected item value for combo box and drop-down controls.</summary>
    public string? SelectedValue { get; set; }

    /// <summary>Boolean value for checkbox controls.</summary>
    public bool? Checked { get; set; }

    /// <summary>ISO date value for date controls.</summary>
    public string? DateIso { get; set; }

    /// <summary>Referenced image asset for picture controls.</summary>
    public string? AssetId { get; set; }
}

/// <summary>Selectable item for combo box and drop-down content controls.</summary>
public class DocumentContentControlItem
{
    /// <summary>Displayed choice text.</summary>
    public string DisplayText { get; set; } = string.Empty;

    /// <summary>Stored choice value.</summary>
    public string Value { get; set; } = string.Empty;
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

    /// <summary>Canonical field instruction text, for example <c>REF heading-1 \h</c> or <c>SEQ Figure</c>.</summary>
    public string? InstrText { get; set; }

    /// <summary>Last calculated field result persisted with the document.</summary>
    public string? CachedResult { get; set; }

    /// <summary>Optional target id used by REF, STYLEREF, caption, citation, and table-of-figures fields.</summary>
    public string? TargetId { get; set; }

    /// <summary>Optional target kind such as heading, bookmark, caption, numberedItem, figure, table, or equation.</summary>
    public string? ReferenceKind { get; set; }

    /// <summary>Optional reference display format such as text, number, page, or full.</summary>
    public string? ReferenceFormat { get; set; }

    /// <summary>Optional SEQ sequence id, for example figure or table.</summary>
    public string? SequenceId { get; set; }

    /// <summary>Optional SEQ label rendered before the generated number.</summary>
    public string? SequenceLabel { get; set; }

    /// <summary>Optional citation source id.</summary>
    public string? CitationId { get; set; }

    /// <summary>Supplemental field metadata preserved across runtimes.</summary>
    public Dictionary<string, string?> Metadata { get; set; } = [];
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
    RevisionNumber,

    /// <summary>Current time.</summary>
    Time,

    /// <summary>Nearest style reference, usually a heading text.</summary>
    StyleRef,

    /// <summary>Cross-reference to a heading, bookmark, caption, or numbered item.</summary>
    Ref,

    /// <summary>Sequence field used by captions.</summary>
    Seq,

    /// <summary>Generated table of figures, tables, or equations.</summary>
    TableOfFigures,

    /// <summary>Generated bibliography output.</summary>
    Bibliography,

    /// <summary>Inline citation reference.</summary>
    Citation,

    /// <summary>Field instruction not mapped to a first-class Tempo field type.</summary>
    Unknown
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

    /// <summary>Small caps text.</summary>
    SmallCaps,

    /// <summary>All caps text.</summary>
    AllCaps,

    /// <summary>Double strikethrough text.</summary>
    DoubleStrikethrough,

    /// <summary>Additional character spacing. The spacing value is stored in <see cref="InlineMark.Value"/>.</summary>
    CharacterSpacing,

    /// <summary>Horizontal character scale. The percentage value is stored in <see cref="InlineMark.Value"/>.</summary>
    CharacterScale,

    /// <summary>Kerning override. The enabled state is stored in <see cref="InlineMark.Value"/>.</summary>
    Kerning,

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
