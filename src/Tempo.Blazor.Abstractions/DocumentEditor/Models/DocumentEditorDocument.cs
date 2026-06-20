using System.Text.Json.Serialization;

namespace Tempo.Blazor.DocumentEditor.Models;

/// <summary>Root JSON document used by <c>TmDocumentEditor</c>.</summary>
public class DocumentEditorDocument
{
    /// <summary>Current document editor schema version.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>Schema version used to serialize this document.</summary>
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>Stable document identifier.</summary>
    public string DocumentId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Document metadata.</summary>
    public DocumentEditorMetadata Metadata { get; set; } = new();

    /// <summary>Default page settings for the document.</summary>
    public DocumentPageSettings PageSettings { get; set; } = new();

    /// <summary>Default visual theme and typography for the document body.</summary>
    public DocumentEditorTheme Theme { get; set; } = new();

    /// <summary>Document hyphenation behavior for canvas and export-capable renderers.</summary>
    public DocumentHyphenationOptions Hyphenation { get; set; } = new();

    /// <summary>Page background, border, and watermark settings.</summary>
    public DocumentPageBackgroundOptions PageBackground { get; set; } = new();

    /// <summary>Document sections.</summary>
    public List<DocumentSection> Sections { get; set; } = [];

    /// <summary>Ordered document blocks.</summary>
    public List<DocumentBlock> Blocks { get; set; } = [];

    /// <summary>Document comments.</summary>
    public List<DocumentComment> Comments { get; set; } = [];

    /// <summary>Footnotes and endnotes used by the document.</summary>
    public List<DocumentNote> Notes { get; set; } = [];

    /// <summary>Header and footer definitions.</summary>
    public List<DocumentHeaderFooter> HeadersFooters { get; set; } = [];

    /// <summary>Document-level numbering definitions used by list blocks.</summary>
    public List<DocumentNumberingDefinition> NumberingDefinitions { get; set; } = [];

    /// <summary>Named list styles available to list blocks.</summary>
    public List<DocumentListStyle> ListStyles { get; set; } = [];

    /// <summary>Document-level styles used by paragraphs, character runs, tables, and lists.</summary>
    public List<DocumentStyleDefinition> Styles { get; set; } = [];

    /// <summary>Bibliography sources available to citation and bibliography fields.</summary>
    public List<DocumentBibliographySource> BibliographySources { get; set; } = [];

    /// <summary>Citation references inserted in the document body.</summary>
    public List<DocumentCitationReference> Citations { get; set; } = [];

    /// <summary>Tracked revisions stored with the document snapshot.</summary>
    public List<DocumentRevision> Revisions { get; set; } = [];

    /// <summary>Image/file assets referenced by blocks.</summary>
    public List<DocumentImageAsset> Assets { get; set; } = [];

    /// <summary>Named anchors used by tokens, placeholders, and signing-ready renditions.</summary>
    public List<DocumentAnchor> Anchors { get; set; } = [];

    /// <summary>Whether the document is protected (only editable within <see cref="RestrictedMarkers"/>).</summary>
    public bool IsProtected { get; set; }

    /// <summary>Editable regions within a protected document. Empty means the whole document is locked.</summary>
    public List<DocumentRestrictedMarker> RestrictedMarkers { get; set; } = [];

    /// <summary>Monotonic mutation counter incremented by mutators so consumers can detect change without comparing serialized JSON.</summary>
    /// <remarks>
    /// Not serialized: consumers re-derive Version from the live in-memory edits; persisted snapshots
    /// always start at 0. JSON-ignored to avoid coupling clients to an internal counter.
    /// </remarks>
    [JsonIgnore]
    public long Version { get; set; }

    /// <summary>Bumps <see cref="Version"/>. Call after any structural or content change so dependent
    /// systems (snapshot diff, autosave, dirty tracking) observe the new state cheaply.</summary>
    public long BumpVersion()
    {
        Version = unchecked(Version + 1);
        return Version;
    }

    /// <summary>Creates a new empty document with one default section.</summary>
    public static DocumentEditorDocument Empty(string? documentId = null)
    {
        var id = string.IsNullOrWhiteSpace(documentId)
            ? Guid.NewGuid().ToString("N")
            : documentId;

        var sectionId = Guid.NewGuid().ToString("N");
        return new DocumentEditorDocument
        {
            DocumentId = id!,
            Sections =
            [
                new DocumentSection
                {
                    Id = sectionId,
                    Order = 0,
                    Properties = new DocumentSectionProperties()
                }
            ]
        };
    }
}

/// <summary>Document hyphenation behavior.</summary>
public class DocumentHyphenationOptions
{
    /// <summary>Whether automatic hyphenation is enabled.</summary>
    public bool Enabled { get; set; }

    /// <summary>Hyphenation mode: off, manual, or auto.</summary>
    public string Mode { get; set; } = "off";

    /// <summary>Distance from the right edge where hyphenation may be applied, in CSS pixels.</summary>
    public double Zone { get; set; }

    /// <summary>Maximum number of consecutive hyphenated lines.</summary>
    public int ConsecutiveLimit { get; set; } = 2;

    /// <summary>Minimum characters kept before a hyphen.</summary>
    public int MinPrefix { get; set; } = 3;

    /// <summary>Minimum characters kept after a hyphen.</summary>
    public int MinSuffix { get; set; } = 3;
}

/// <summary>Page background, border, and watermark settings.</summary>
public class DocumentPageBackgroundOptions
{
    /// <summary>Optional page fill color.</summary>
    public string? Color { get; set; }

    /// <summary>Optional watermark settings.</summary>
    public DocumentWatermarkOptions Watermark { get; set; } = new();

    /// <summary>Optional page border settings.</summary>
    public DocumentPageBorderOptions Border { get; set; } = new();
}

/// <summary>Text or image watermark settings.</summary>
public class DocumentWatermarkOptions
{
    /// <summary>Whether the watermark should be rendered.</summary>
    public bool Enabled { get; set; }

    /// <summary>Watermark kind: text or image.</summary>
    public string Kind { get; set; } = "text";

    /// <summary>Watermark text.</summary>
    public string? Text { get; set; }

    /// <summary>Watermark image URL.</summary>
    public string? ImageUrl { get; set; }

    /// <summary>Watermark opacity from 0 to 1.</summary>
    public double Opacity { get; set; } = 0.16;

    /// <summary>Watermark rotation angle in degrees.</summary>
    public double Rotation { get; set; } = -36;

    /// <summary>Watermark color used for text watermarks.</summary>
    public string? Color { get; set; }
}

/// <summary>Page border settings.</summary>
public class DocumentPageBorderOptions
{
    /// <summary>Whether the page border should be rendered.</summary>
    public bool Enabled { get; set; }

    /// <summary>Border stroke color.</summary>
    public string? Color { get; set; }

    /// <summary>Border width in CSS pixels.</summary>
    public double Width { get; set; } = 1;

    /// <summary>Border margin in CSS pixels.</summary>
    public double Margin { get; set; }

    /// <summary>Border alignment: page or margin.</summary>
    public string AlignTo { get; set; } = "page";

    /// <summary>Optional dash pattern.</summary>
    public List<double> Dash { get; set; } = [];
}

/// <summary>Metadata associated with a document.</summary>
public class DocumentEditorMetadata
{
    /// <summary>Document title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Optional document description.</summary>
    public string? Description { get; set; }

    /// <summary>Document author.</summary>
    public DocumentEditorAuthor? Author { get; set; }

    /// <summary>Document creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Document last modification timestamp.</summary>
    public DateTimeOffset? ModifiedAt { get; set; }

    /// <summary>Current document status.</summary>
    public DocumentEditorStatus Status { get; set; } = DocumentEditorStatus.Draft;

    /// <summary>Arbitrary tags associated with the document.</summary>
    public List<string> Tags { get; set; } = [];
}

/// <summary>Document author or contributor metadata.</summary>
public class DocumentEditorAuthor
{
    /// <summary>Stable author identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Displayed author name.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Optional email address.</summary>
    public string? Email { get; set; }

    /// <summary>Optional avatar URL.</summary>
    public string? AvatarUrl { get; set; }
}

/// <summary>Document lifecycle status.</summary>
public enum DocumentEditorStatus
{
    /// <summary>Document is being drafted.</summary>
    Draft,

    /// <summary>Document is under review.</summary>
    Review,

    /// <summary>Document is finalized but not archived.</summary>
    Final,

    /// <summary>Document is archived and normally read-only.</summary>
    Archived
}

/// <summary>Bibliography source metadata used by citation and bibliography fields.</summary>
public sealed class DocumentBibliographySource
{
    /// <summary>Stable source identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Source type, for example book, article, report, or web.</summary>
    public string SourceType { get; set; } = "book";

    /// <summary>Primary author display name.</summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>Source title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Optional source container, publisher, journal, or website name.</summary>
    public string? Container { get; set; }

    /// <summary>Optional source publication year.</summary>
    public int? Year { get; set; }

    /// <summary>Optional source URL.</summary>
    public string? Url { get; set; }

    /// <summary>Additional provider-specific metadata.</summary>
    public Dictionary<string, string?> Metadata { get; set; } = [];
}

/// <summary>Document citation reference persisted outside individual field runs for provider interoperability.</summary>
public sealed class DocumentCitationReference
{
    /// <summary>Stable citation identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Referenced bibliography source id.</summary>
    public string SourceId { get; set; } = string.Empty;

    /// <summary>Optional inline field run id that renders this citation.</summary>
    public string? RunId { get; set; }

    /// <summary>Optional page locator or pinpoint reference.</summary>
    public string? Locator { get; set; }

    /// <summary>Optional rendered citation text.</summary>
    public string? DisplayText { get; set; }
}

/// <summary>Editor interaction mode.</summary>
public enum DocumentEditorMode
{
    /// <summary>Full editing mode.</summary>
    Edit,

    /// <summary>Review mode with comments and revisions.</summary>
    Review,

    /// <summary>Read-only viewing mode.</summary>
    ReadOnly,

    /// <summary>Read-only view of a finalized rendition.</summary>
    RenditionPreview
}

/// <summary>Default page settings for a document or section.</summary>
public class DocumentPageSettings
{
    /// <summary>Page size.</summary>
    public DocumentPageSize Size { get; set; } = DocumentPageSize.A4;

    /// <summary>Page margins.</summary>
    public DocumentPageMargins Margins { get; set; } = DocumentPageMargins.Default;

    /// <summary>Whether pages are landscape instead of portrait.</summary>
    public bool Landscape { get; set; }

    /// <summary>Header distance from the top edge of the page in points.</summary>
    public double HeaderDistanceFromTop { get; set; } = 36;

    /// <summary>Footer distance from the bottom edge of the page in points.</summary>
    public double FooterDistanceFromBottom { get; set; } = 36;
}

/// <summary>Physical page size in points.</summary>
public class DocumentPageSize
{
    /// <summary>A4 page size in points.</summary>
    public static DocumentPageSize A4 => new() { Name = "A4", Width = 595.276, Height = 841.89 };

    /// <summary>US Letter page size in points.</summary>
    public static DocumentPageSize Letter => new() { Name = "Letter", Width = 612, Height = 792 };

    /// <summary>Optional page size name.</summary>
    public string? Name { get; set; }

    /// <summary>Page width in points.</summary>
    public double Width { get; set; }

    /// <summary>Page height in points.</summary>
    public double Height { get; set; }
}

/// <summary>Page margins in points.</summary>
public class DocumentPageMargins
{
    /// <summary>Default 72 pt margins.</summary>
    public static DocumentPageMargins Default => new() { Top = 72, Right = 72, Bottom = 72, Left = 72 };

    /// <summary>Top margin in points.</summary>
    public double Top { get; set; }

    /// <summary>Right margin in points.</summary>
    public double Right { get; set; }

    /// <summary>Bottom margin in points.</summary>
    public double Bottom { get; set; }

    /// <summary>Left margin in points.</summary>
    public double Left { get; set; }
}

/// <summary>Default document typography used when individual runs or paragraphs do not override it.</summary>
public class DocumentEditorTheme
{
    /// <summary>Default body font family CSS value.</summary>
    public string BodyFontFamily { get; set; } = "Aptos, Arial, sans-serif";

    /// <summary>Default body font size in points.</summary>
    public double BodyFontSize { get; set; } = 11;

    /// <summary>Default paragraph line-height multiplier.</summary>
    public double BodyLineHeight { get; set; } = 1.15;

    /// <summary>Default spacing after body paragraphs in points.</summary>
    public double ParagraphSpacingAfter { get; set; } = 8;
}

/// <summary>Document section with independent page settings and headers/footers.</summary>
public class DocumentSection
{
    /// <summary>Stable section identifier.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Sort order of the section.</summary>
    public int Order { get; set; }

    /// <summary>Section display title.</summary>
    public string? Title { get; set; }

    /// <summary>Section properties.</summary>
    public DocumentSectionProperties Properties { get; set; } = new();
}

/// <summary>Section-level page, header/footer, and note settings.</summary>
public class DocumentSectionProperties
{
    /// <summary>Page settings for this section.</summary>
    public DocumentPageSettings PageSettings { get; set; } = new();

    /// <summary>Column layout used by body text in this section.</summary>
    public DocumentSectionColumns Columns { get; set; } = new();

    /// <summary>Line numbering settings used by this section.</summary>
    public DocumentLineNumbering LineNumbering { get; set; } = new();

    /// <summary>Whether the section has a different first page header/footer.</summary>
    public bool DifferentFirstPage { get; set; }

    /// <summary>Whether the section uses different odd and even headers/footers.</summary>
    public bool DifferentOddAndEvenPages { get; set; }

    /// <summary>Header and footer references used by this section.</summary>
    public List<DocumentHeaderFooterReference> HeaderFooterReferences { get; set; } = [];

    /// <summary>Footnote/endnote numbering settings.</summary>
    public DocumentNoteNumbering NoteNumbering { get; set; } = new();
}

/// <summary>Reference from a section to a header or footer definition.</summary>
public class DocumentHeaderFooterReference
{
    /// <summary>Header/footer definition identifier.</summary>
    public string HeaderFooterId { get; set; } = string.Empty;

    /// <summary>Referenced header/footer type.</summary>
    public DocumentHeaderFooterType Type { get; set; } = DocumentHeaderFooterType.Header;

    /// <summary>Scope where this reference applies.</summary>
    public DocumentHeaderFooterScope Scope { get; set; } = DocumentHeaderFooterScope.Primary;
}

/// <summary>Section column layout settings.</summary>
public class DocumentSectionColumns
{
    /// <summary>Number of text columns in the section.</summary>
    public int Count { get; set; } = 1;

    /// <summary>Default spacing between columns in points.</summary>
    public double Spacing { get; set; } = 36;

    /// <summary>Whether a separator line is rendered between columns.</summary>
    public bool SeparatorLine { get; set; }

    /// <summary>Whether short multi-column section content is visually balanced across columns.</summary>
    public bool Balance { get; set; }

    /// <summary>Preset column layout name such as one, two, three, left, right, or custom.</summary>
    public string Preset { get; set; } = "one";

    /// <summary>Optional explicit custom column definitions.</summary>
    public List<DocumentSectionColumn> Items { get; set; } = [];
}

/// <summary>Explicit section column definition.</summary>
public class DocumentSectionColumn
{
    /// <summary>Column width in points.</summary>
    public double? Width { get; set; }

    /// <summary>Spacing after this column in points.</summary>
    public double? SpacingAfter { get; set; }
}

/// <summary>Line numbering settings for a document section.</summary>
public class DocumentLineNumbering
{
    /// <summary>Whether line numbering is enabled.</summary>
    public bool Enabled { get; set; }

    /// <summary>Number used for the first rendered line number.</summary>
    public int StartAt { get; set; } = 1;

    /// <summary>Line number increment.</summary>
    public int Increment { get; set; } = 1;

    /// <summary>Distance between the number and text column in points.</summary>
    public double DistanceFromText { get; set; } = 18;

    /// <summary>Restart behavior: continuous, page, or section.</summary>
    public DocumentLineNumberingRestart Restart { get; set; } = DocumentLineNumberingRestart.Continuous;
}

/// <summary>Line numbering restart behavior.</summary>
public enum DocumentLineNumberingRestart
{
    /// <summary>Line numbering continues across the full document.</summary>
    Continuous,

    /// <summary>Line numbering restarts on each page.</summary>
    Page,

    /// <summary>Line numbering restarts at each section.</summary>
    Section
}

/// <summary>Reusable document style definition for paragraph, character, table, or list content.</summary>
public class DocumentStyleDefinition
{
    /// <summary>Stable style identifier.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Human-readable style name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Style category.</summary>
    public DocumentStyleType Type { get; set; } = DocumentStyleType.Paragraph;

    /// <summary>Optional parent style identifier or name used for inheritance.</summary>
    public string? BasedOn { get; set; }

    /// <summary>Optional next paragraph style identifier or name.</summary>
    public string? Next { get; set; }

    /// <summary>Whether this style is shown in the quick style gallery.</summary>
    public bool IsQuickStyle { get; set; }

    /// <summary>Whether this style is a primary built-in style.</summary>
    public bool IsPrimary { get; set; }

    /// <summary>Optional heading level associated with this style.</summary>
    public int? HeadingLevel { get; set; }

    /// <summary>Optional outline level associated with this style.</summary>
    public int? OutlineLevel { get; set; }

    /// <summary>Paragraph-level formatting values.</summary>
    public Dictionary<string, object?> ParagraphFormat { get; set; } = [];

    /// <summary>Character-level formatting values.</summary>
    public Dictionary<string, object?> CharacterFormat { get; set; } = [];

    /// <summary>Table-level formatting values.</summary>
    public Dictionary<string, object?> TableFormat { get; set; } = [];

    /// <summary>List-level formatting values.</summary>
    public Dictionary<string, object?> ListFormat { get; set; } = [];
}

/// <summary>Document style category.</summary>
public enum DocumentStyleType
{
    /// <summary>Paragraph style.</summary>
    Paragraph,

    /// <summary>Character style.</summary>
    Character,

    /// <summary>Table style.</summary>
    Table,

    /// <summary>List style.</summary>
    List
}

/// <summary>Combined page setup payload for applying section page, column, and line-numbering settings.</summary>
public class DocumentSectionPageSetup
{
    /// <summary>Optional section identifier that should receive the setup.</summary>
    public string? SectionId { get; set; }

    /// <summary>Page settings for the target section.</summary>
    public DocumentPageSettings PageSettings { get; set; } = new();

    /// <summary>Column layout for the target section.</summary>
    public DocumentSectionColumns Columns { get; set; } = new();

    /// <summary>Line numbering settings for the target section.</summary>
    public DocumentLineNumbering LineNumbering { get; set; } = new();
}
