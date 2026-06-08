namespace Tempo.Blazor.DocumentEditor.Models;

/// <summary>Canonical document model consumed by the canvas document engine.</summary>
public sealed class CanvasDocumentModel
{
    /// <summary>Canvas model schema version.</summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>Stable document identifier.</summary>
    public string DocumentId { get; set; } = string.Empty;

    /// <summary>Monotonic source document version.</summary>
    public long Version { get; set; }

    /// <summary>Document metadata copied from the persistence model.</summary>
    public DocumentEditorMetadata Metadata { get; set; } = new();

    /// <summary>Canvas page settings for the document default section.</summary>
    public CanvasPageSettings PageSettings { get; set; } = new();

    /// <summary>Default visual theme and typography.</summary>
    public DocumentEditorTheme Theme { get; set; } = new();

    /// <summary>Hyphenation options consumed by the canvas text layout engine.</summary>
    public DocumentHyphenationOptions Hyphenation { get; set; } = new();

    /// <summary>Page fill, watermark, and page border options consumed by the canvas renderer.</summary>
    public DocumentPageBackgroundOptions PageBackground { get; set; } = new();

    /// <summary>Ordered document sections with their own block trees.</summary>
    public List<CanvasDocumentSection> Sections { get; set; } = [];

    /// <summary>Runtime body view used by the JavaScript canvas engine.</summary>
    public CanvasDocumentBody Body { get; set; } = new();

    /// <summary>Document comments copied into the canvas model boundary.</summary>
    public List<DocumentComment> Comments { get; set; } = [];

    /// <summary>Footnotes and endnotes with converted body blocks.</summary>
    public List<CanvasDocumentNote> Notes { get; set; } = [];

    /// <summary>Header and footer definitions with converted body blocks.</summary>
    public List<CanvasDocumentHeaderFooter> HeadersFooters { get; set; } = [];

    /// <summary>Document-level numbering definitions used by list blocks.</summary>
    public List<DocumentNumberingDefinition> NumberingDefinitions { get; set; } = [];

    /// <summary>Named list styles available to list blocks.</summary>
    public List<DocumentListStyle> ListStyles { get; set; } = [];

    /// <summary>Document-level paragraph, character, table, and list style definitions.</summary>
    public List<DocumentStyleDefinition> Styles { get; set; } = [];

    /// <summary>Bibliography sources available to field rendering.</summary>
    public List<DocumentBibliographySource> BibliographySources { get; set; } = [];

    /// <summary>Citation references inserted in the canvas document.</summary>
    public List<DocumentCitationReference> Citations { get; set; } = [];

    /// <summary>Tracked revisions copied into the canvas model boundary.</summary>
    public List<DocumentRevision> Revisions { get; set; } = [];

    /// <summary>Image and file assets referenced by document content.</summary>
    public List<DocumentImageAsset> Assets { get; set; } = [];

    /// <summary>Named anchors referenced by document content.</summary>
    public List<DocumentAnchor> Anchors { get; set; } = [];

    /// <summary>Whether the document is protected.</summary>
    public bool IsProtected { get; set; }

    /// <summary>Restricted editing markers copied from the persistence model.</summary>
    public List<DocumentRestrictedMarker> RestrictedMarkers { get; set; } = [];

    /// <summary>Runtime revision incremented when heading text or heading hierarchy changes invalidate the outline cache.</summary>
    public long OutlineRevision { get; set; }

    /// <summary>Runtime revision incremented when heading changes invalidate table-of-contents caches.</summary>
    public long TableOfContentsRevision { get; set; }

    /// <summary>Opaque source channel used to restore document properties outside the canvas runtime surface.</summary>
    public CanvasPreserveChannel Preserve { get; set; } = new();
}

/// <summary>Canvas runtime body.</summary>
public sealed class CanvasDocumentBody
{
    /// <summary>Ordered body blocks.</summary>
    public List<CanvasDocumentBlock> Blocks { get; set; } = [];
}

/// <summary>Canvas section containing section page settings and a block tree.</summary>
public sealed class CanvasDocumentSection
{
    /// <summary>Stable section identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Sort order of the section.</summary>
    public int Order { get; set; }

    /// <summary>Optional section title.</summary>
    public string? Title { get; set; }

    /// <summary>Section properties copied from the persistence model.</summary>
    public DocumentSectionProperties Properties { get; set; } = new();

    /// <summary>Canvas page settings for this section.</summary>
    public CanvasPageSettings PageSettings { get; set; } = new();

    /// <summary>Ordered blocks belonging to this section.</summary>
    public List<CanvasDocumentBlock> Blocks { get; set; } = [];

    /// <summary>Opaque source channel for section data outside the canvas runtime surface.</summary>
    public CanvasPreserveChannel Preserve { get; set; } = new();
}

/// <summary>Canvas page settings expressed in CSS pixels for the runtime renderer.</summary>
public sealed class CanvasPageSettings
{
    /// <summary>Page width in CSS pixels.</summary>
    public double Width { get; set; }

    /// <summary>Page height in CSS pixels.</summary>
    public double Height { get; set; }

    /// <summary>Top margin in CSS pixels.</summary>
    public double MarginTop { get; set; }

    /// <summary>Right margin in CSS pixels.</summary>
    public double MarginRight { get; set; }

    /// <summary>Bottom margin in CSS pixels.</summary>
    public double MarginBottom { get; set; }

    /// <summary>Left margin in CSS pixels.</summary>
    public double MarginLeft { get; set; }

    /// <summary>Header distance from the top edge in CSS pixels.</summary>
    public double HeaderDistanceFromTop { get; set; }

    /// <summary>Footer distance from the bottom edge in CSS pixels.</summary>
    public double FooterDistanceFromBottom { get; set; }

    /// <summary>Optional page size name.</summary>
    public string? SizeName { get; set; }

    /// <summary>Whether the page is landscape.</summary>
    public bool Landscape { get; set; }

    /// <summary>Opaque source channel for exact page setting restoration.</summary>
    public CanvasPreserveChannel Preserve { get; set; } = new();
}

/// <summary>Canvas block optimized for layout and runtime editing.</summary>
public sealed class CanvasDocumentBlock
{
    /// <summary>Stable block identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Optional owning section identifier.</summary>
    public string? SectionId { get; set; }

    /// <summary>Canvas block type, for example paragraph, heading, list, quote, table, image, or pageBreak.</summary>
    public string Type { get; set; } = CanvasDocumentModelTypes.Paragraph;

    /// <summary>Sort order inside its parent block collection.</summary>
    public double Order { get; set; }

    /// <summary>Paragraph-level formatting for text-like blocks.</summary>
    public DocumentParagraphProperties ParagraphProperties { get; set; } = new();

    /// <summary>Block content payload.</summary>
    public CanvasBlockContent Content { get; set; } = new();

    /// <summary>Opaque source channel for exact block restoration.</summary>
    public CanvasPreserveChannel Preserve { get; set; } = new();
}

/// <summary>Canvas block content payload.</summary>
public sealed class CanvasBlockContent
{
    /// <summary>Content type matching the owning block type.</summary>
    public string Type { get; set; } = CanvasDocumentModelTypes.Paragraph;

    /// <summary>Inline runs for paragraph-like blocks.</summary>
    public List<CanvasInlineRun> Runs { get; set; } = [];

    /// <summary>Heading level for heading blocks.</summary>
    public int? HeadingLevel { get; set; }

    /// <summary>Optional heading or paragraph style id.</summary>
    public string? StyleId { get; set; }

    /// <summary>Optional style name.</summary>
    public string? StyleName { get; set; }

    /// <summary>Optional outline level used by navigation and TOC generation.</summary>
    public int? OutlineLevel { get; set; }

    /// <summary>List metadata for list blocks.</summary>
    public CanvasListProperties? List { get; set; }

    /// <summary>Table content for table blocks.</summary>
    public CanvasTableContent? Table { get; set; }

    /// <summary>Image content for standalone image blocks.</summary>
    public CanvasImageContent? Image { get; set; }

    /// <summary>Page break content for page break blocks.</summary>
    public CanvasPageBreakContent? PageBreak { get; set; }

    /// <summary>Structured document tag metadata for content-control blocks.</summary>
    public CanvasContentControlBlock? ContentControl { get; set; }

    /// <summary>Optional caption metadata for generated captions and table-of-figures fields.</summary>
    public DocumentCaptionMetadata? Caption { get; set; }

    /// <summary>Optional generated table-of-contents metadata for semantic TOC entry paragraphs.</summary>
    public DocumentTableOfContentsMetadata? TableOfContents { get; set; }
}

/// <summary>Canvas list metadata.</summary>
public sealed class CanvasListProperties
{
    /// <summary>Whether the list item uses ordered numbering.</summary>
    public bool Ordered { get; set; }

    /// <summary>Zero-based nesting level.</summary>
    public int IndentLevel { get; set; }

    /// <summary>Starting number for ordered lists.</summary>
    public int StartNumber { get; set; } = 1;

    /// <summary>Optional concrete numbering instance identifier.</summary>
    public string? NumberingId { get; set; }

    /// <summary>Optional abstract numbering definition identifier.</summary>
    public string? AbstractNumberingId { get; set; }

    /// <summary>Optional list style identifier.</summary>
    public string? ListStyleId { get; set; }

    /// <summary>Optional numbering format override for this item level.</summary>
    public string? NumberFormat { get; set; }

    /// <summary>Optional numbering text template override.</summary>
    public string? LevelText { get; set; }

    /// <summary>Optional suffix after the list label: tab, space, or none.</summary>
    public string? Suffix { get; set; }

    /// <summary>Optional absolute label indent for this list item in CSS pixels.</summary>
    public double? LabelIndent { get; set; }

    /// <summary>Optional hanging indent for this list item in CSS pixels.</summary>
    public double? HangingIndent { get; set; }

    /// <summary>Whether this item restarts numbering at its level.</summary>
    public bool RestartNumbering { get; set; }

    /// <summary>Whether this item explicitly continues the previous numbering sequence.</summary>
    public bool ContinueNumbering { get; set; }

    /// <summary>Optional explicit numbering value for this item.</summary>
    public int? NumberingValue { get; set; }
}

/// <summary>Canvas inline run.</summary>
public sealed class CanvasInlineRun
{
    /// <summary>Stable run identifier.</summary>
    public string? Id { get; set; }

    /// <summary>Run type, for example text, field, drawing, token, or noteReference.</summary>
    public string Type { get; set; } = CanvasDocumentModelTypes.TextRun;

    /// <summary>Text value for text runs.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Marks applied to this run.</summary>
    public List<CanvasInlineMark> Marks { get; set; } = [];

    /// <summary>Field payload for field runs.</summary>
    public CanvasFieldRun? Field { get; set; }

    /// <summary>Token payload for token runs.</summary>
    public CanvasTokenRun? Token { get; set; }

    /// <summary>Note reference payload for note reference runs.</summary>
    public CanvasNoteReferenceRun? NoteReference { get; set; }

    /// <summary>Drawing payload for drawing runs.</summary>
    public CanvasDrawingRun? Drawing { get; set; }

    /// <summary>Math payload for equation runs.</summary>
    public CanvasMathRun? Math { get; set; }

    /// <summary>Structured document tag payload for content-control runs.</summary>
    public CanvasContentControlRun? ContentControl { get; set; }

    /// <summary>Opaque source channel for exact inline restoration.</summary>
    public CanvasPreserveChannel Preserve { get; set; } = new();
}

/// <summary>Canvas inline mark.</summary>
public sealed class CanvasInlineMark
{
    /// <summary>Mark type name.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Optional CSS-like mark value.</summary>
    public string? Value { get; set; }

    /// <summary>Optional hyperlink metadata.</summary>
    public LinkMarkData? Link { get; set; }

    /// <summary>Optional comment anchor metadata.</summary>
    public CommentAnchorMarkData? CommentAnchor { get; set; }

    /// <summary>Optional revision id for revision marks.</summary>
    public string? RevisionId { get; set; }

    /// <summary>Opaque source channel for exact mark restoration.</summary>
    public CanvasPreserveChannel Preserve { get; set; } = new();
}

/// <summary>Canvas field run payload.</summary>
public sealed class CanvasFieldRun
{
    /// <summary>Field type.</summary>
    public DocumentFieldType FieldType { get; set; } = DocumentFieldType.PageNumber;

    /// <summary>Optional formatting hint.</summary>
    public string? Format { get; set; }

    /// <summary>Fallback display text.</summary>
    public string? FallbackText { get; set; }

    /// <summary>Last display text captured by a runtime.</summary>
    public string? DisplayText { get; set; }

    /// <summary>Canonical field instruction text.</summary>
    public string? InstrText { get; set; }

    /// <summary>Last calculated result persisted with the field.</summary>
    public string? CachedResult { get; set; }

    /// <summary>Optional target identifier used by cross-reference and generated fields.</summary>
    public string? TargetId { get; set; }

    /// <summary>Optional target kind such as heading, bookmark, caption, numberedItem, figure, table, or equation.</summary>
    public string? ReferenceKind { get; set; }

    /// <summary>Optional reference display format such as text, number, page, or full.</summary>
    public string? ReferenceFormat { get; set; }

    /// <summary>Optional SEQ sequence id.</summary>
    public string? SequenceId { get; set; }

    /// <summary>Optional SEQ label rendered before the generated number.</summary>
    public string? SequenceLabel { get; set; }

    /// <summary>Optional citation source id.</summary>
    public string? CitationId { get; set; }

    /// <summary>Supplemental field metadata preserved across runtimes.</summary>
    public Dictionary<string, string?> Metadata { get; set; } = [];
}

/// <summary>Canvas content-control inline payload.</summary>
public sealed class CanvasContentControlRun
{
    /// <summary>Structured document tag metadata and value.</summary>
    public DocumentContentControl Control { get; set; } = new();

    /// <summary>Rich inline content owned by rich-text content controls.</summary>
    public List<CanvasInlineRun> Runs { get; set; } = [];
}

/// <summary>Canvas content-control block payload.</summary>
public sealed class CanvasContentControlBlock
{
    /// <summary>Structured document tag metadata and value.</summary>
    public DocumentContentControl Control { get; set; } = new();

    /// <summary>Nested blocks owned by the content control.</summary>
    public List<CanvasDocumentBlock> Blocks { get; set; } = [];
}

/// <summary>Canvas token run payload.</summary>
public sealed class CanvasTokenRun
{
    /// <summary>Stable token key.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Display label shown in the editor.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Optional token type metadata.</summary>
    public string? TokenType { get; set; }

    /// <summary>Optional short token type label.</summary>
    public string? TypeLabel { get; set; }

    /// <summary>Optional CSS class copied from the token catalog.</summary>
    public string? ColorClass { get; set; }

    /// <summary>Optional token description.</summary>
    public string? Description { get; set; }

    /// <summary>Optional fallback text.</summary>
    public string? FallbackText { get; set; }
}

/// <summary>Canvas note reference payload.</summary>
public sealed class CanvasNoteReferenceRun
{
    /// <summary>Referenced note id.</summary>
    public string NoteId { get; set; } = string.Empty;

    /// <summary>Referenced note type.</summary>
    public DocumentNoteType NoteType { get; set; } = DocumentNoteType.Footnote;

    /// <summary>Optional displayed marker.</summary>
    public string? DisplayMarker { get; set; }
}

/// <summary>Canvas drawing run payload.</summary>
public sealed class CanvasDrawingRun
{
    /// <summary>Stable drawing object identifier.</summary>
    public string ObjectId { get; set; } = string.Empty;

    /// <summary>Drawing kind.</summary>
    public DocumentDrawingKind Kind { get; set; } = DocumentDrawingKind.Image;

    /// <summary>Image source kind.</summary>
    public DocumentImageSource Source { get; set; } = DocumentImageSource.Url;

    /// <summary>Direct image URL.</summary>
    public string? Url { get; set; }

    /// <summary>Provider asset id.</summary>
    public string? AssetId { get; set; }

    /// <summary>Alternative text.</summary>
    public string? AltText { get; set; }

    /// <summary>Whether the drawing is decorative.</summary>
    public bool IsDecorative { get; set; }

    /// <summary>Optional caption.</summary>
    public string? Caption { get; set; }

    /// <summary>Source or default size.</summary>
    public DocumentImageSize Size { get; set; } = new();

    /// <summary>Natural asset size.</summary>
    public DocumentImageSize NaturalSize { get; set; } = new();

    /// <summary>Canonical object layout.</summary>
    public DocumentObjectLayout Layout { get; set; } = DocumentObjectLayout.Inline();

    /// <summary>Optional hyperlink URL wrapping the drawing.</summary>
    public string? LinkUrl { get; set; }

    /// <summary>Shape geometry for vector drawings.</summary>
    public DocumentDrawingShape? Shape { get; set; }

    /// <summary>Text body hosted by a shape or text box.</summary>
    public DocumentDrawingTextBody? TextBody { get; set; }

    /// <summary>Chart payload hosted by this drawing.</summary>
    public DocumentDrawingChart? Chart { get; set; }

    /// <summary>Grouped child drawing references and transform metadata.</summary>
    public DocumentDrawingGroup? Group { get; set; }

    /// <summary>Optional DOCX DrawingML metadata.</summary>
    public DocumentDocxDrawingMetadata? Docx { get; set; }

    /// <summary>Supplemental drawing metadata.</summary>
    public Dictionary<string, string?> Metadata { get; set; } = [];
}

/// <summary>Canvas math run payload.</summary>
public sealed class CanvasMathRun
{
    /// <summary>Stable equation identifier.</summary>
    public string MathId { get; set; } = string.Empty;

    /// <summary>Whether the equation is laid out as inline or display math.</summary>
    public DocumentMathDisplayMode DisplayMode { get; set; } = DocumentMathDisplayMode.Inline;

    /// <summary>Root math content sequence.</summary>
    public DocumentMathContent Content { get; set; } = new();

    /// <summary>Accessible spoken or textual description.</summary>
    public string? AltText { get; set; }

    /// <summary>Optional MathML mirror.</summary>
    public string? MathML { get; set; }

    /// <summary>Optional OMML XML payload for provider or DOCX roundtrips.</summary>
    public string? OmmlXml { get; set; }

    /// <summary>Supplemental math metadata.</summary>
    public Dictionary<string, string?> Metadata { get; set; } = [];
}

/// <summary>Canvas standalone image content.</summary>
public sealed class CanvasImageContent
{
    /// <summary>Image source kind.</summary>
    public DocumentImageSource Source { get; set; } = DocumentImageSource.Url;

    /// <summary>Direct image URL.</summary>
    public string? Url { get; set; }

    /// <summary>Provider asset id.</summary>
    public string? AssetId { get; set; }

    /// <summary>Alternative text.</summary>
    public string? AltText { get; set; }

    /// <summary>Whether the image is decorative.</summary>
    public bool IsDecorative { get; set; }

    /// <summary>Optional caption.</summary>
    public string? Caption { get; set; }

    /// <summary>Source or default image size.</summary>
    public DocumentImageSize Size { get; set; } = new();

    /// <summary>Natural asset size.</summary>
    public DocumentImageSize NaturalSize { get; set; } = new();

    /// <summary>Image alignment.</summary>
    public DocumentImageAlignment Alignment { get; set; } = DocumentImageAlignment.Center;

    /// <summary>Canonical object layout.</summary>
    public DocumentObjectLayout Layout { get; set; } = DocumentObjectLayout.Inline();

    /// <summary>Optional hyperlink URL wrapping the image.</summary>
    public string? LinkUrl { get; set; }
}

/// <summary>Canvas table content.</summary>
public sealed class CanvasTableContent
{
    /// <summary>Rows in the table.</summary>
    public List<CanvasTableRow> Rows { get; set; } = [];

    /// <summary>Presentation properties for the table.</summary>
    public TableLayoutContent Layout { get; set; } = new();
}

/// <summary>Canvas table row.</summary>
public sealed class CanvasTableRow
{
    /// <summary>Stable row identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Cells in the row.</summary>
    public List<CanvasTableCell> Cells { get; set; } = [];
}

/// <summary>Canvas table cell.</summary>
public sealed class CanvasTableCell
{
    /// <summary>Stable cell identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Column span for merged cells.</summary>
    public int ColumnSpan { get; set; } = 1;

    /// <summary>Row span for merged cells.</summary>
    public int RowSpan { get; set; } = 1;

    /// <summary>Whether this cell is rendered as a header cell.</summary>
    public bool IsHeader { get; set; }

    /// <summary>Merge metadata.</summary>
    public TableCellMerge Merge { get; set; } = new();

    /// <summary>Optional cell width.</summary>
    public double? Width { get; set; }

    /// <summary>Optional background color.</summary>
    public string? BackgroundColor { get; set; }

    /// <summary>Cell border styles.</summary>
    public TableCellBorders Borders { get; set; } = new();

    /// <summary>Vertical alignment for content inside the cell.</summary>
    public TableCellVerticalAlignment VerticalAlignment { get; set; } = TableCellVerticalAlignment.Top;

    /// <summary>Optional cell padding.</summary>
    public double? Padding { get; set; }

    /// <summary>Nested cell blocks.</summary>
    public List<CanvasDocumentBlock> Blocks { get; set; } = [];

    /// <summary>Opaque source channel for exact cell restoration.</summary>
    public CanvasPreserveChannel Preserve { get; set; } = new();
}

/// <summary>Canvas page break payload.</summary>
public sealed class CanvasPageBreakContent
{
    /// <summary>Section or column break behavior represented by this break block.</summary>
    public DocumentSectionBreakType BreakType { get; set; } = DocumentSectionBreakType.Page;

    /// <summary>Optional next section identifier.</summary>
    public string? NextSectionId { get; set; }
}

/// <summary>Canvas note with converted body blocks.</summary>
public sealed class CanvasDocumentNote
{
    /// <summary>Stable note id.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Note type.</summary>
    public DocumentNoteType Type { get; set; } = DocumentNoteType.Footnote;

    /// <summary>Optional owning section id.</summary>
    public string? SectionId { get; set; }

    /// <summary>Optional marker displayed in the document.</summary>
    public string? Marker { get; set; }

    /// <summary>Converted note blocks.</summary>
    public List<CanvasDocumentBlock> Blocks { get; set; } = [];

    /// <summary>Ids of inline references pointing to this note.</summary>
    public List<string> ReferenceIds { get; set; } = [];

    /// <summary>Opaque source channel for exact note restoration.</summary>
    public CanvasPreserveChannel Preserve { get; set; } = new();
}

/// <summary>Canvas header or footer with converted body blocks.</summary>
public sealed class CanvasDocumentHeaderFooter
{
    /// <summary>Stable header/footer id.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Header or footer type.</summary>
    public DocumentHeaderFooterType Type { get; set; } = DocumentHeaderFooterType.Header;

    /// <summary>Header/footer scope.</summary>
    public DocumentHeaderFooterScope Scope { get; set; } = DocumentHeaderFooterScope.Primary;

    /// <summary>Optional owning section id.</summary>
    public string? SectionId { get; set; }

    /// <summary>Converted header/footer blocks.</summary>
    public List<CanvasDocumentBlock> Blocks { get; set; } = [];

    /// <summary>Opaque source channel for exact header/footer restoration.</summary>
    public CanvasPreserveChannel Preserve { get; set; } = new();
}

/// <summary>Opaque JSON preserve channel for fields not directly owned by the canvas runtime.</summary>
public sealed class CanvasPreserveChannel
{
    /// <summary>Original source object JSON.</summary>
    public string? SourceJson { get; set; }
}

/// <summary>String constants used by the canvas document model.</summary>
public static class CanvasDocumentModelTypes
{
    /// <summary>Paragraph block type.</summary>
    public const string Paragraph = "paragraph";

    /// <summary>Heading block type.</summary>
    public const string Heading = "heading";

    /// <summary>List block type.</summary>
    public const string List = "list";

    /// <summary>Quote block type.</summary>
    public const string Quote = "quote";

    /// <summary>Table block type.</summary>
    public const string Table = "table";

    /// <summary>Image block type.</summary>
    public const string Image = "image";

    /// <summary>Page break block type.</summary>
    public const string PageBreak = "pageBreak";

    /// <summary>Structured document tag block type.</summary>
    public const string ContentControl = "contentControl";

    /// <summary>Text inline run type.</summary>
    public const string TextRun = "text";

    /// <summary>Field inline run type.</summary>
    public const string FieldRun = "field";

    /// <summary>Token inline run type.</summary>
    public const string TokenRun = "token";

    /// <summary>Note reference inline run type.</summary>
    public const string NoteReferenceRun = "noteReference";

    /// <summary>Drawing inline run type.</summary>
    public const string DrawingRun = "drawing";

    /// <summary>Math equation inline run type.</summary>
    public const string MathRun = "math";

    /// <summary>Structured document tag inline run type.</summary>
    public const string ContentControlRun = "contentControl";
}
