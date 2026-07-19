using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Reporting.Engine.Pdf;

namespace Tempo.Blazor.DocumentFormats.HeadlessLayout;

/// <summary>
/// Server-side document layout: lays out a <see cref="DocumentEditorDocument"/> with the SAME
/// JavaScript layout chain the canvas editor paints with (the embedded headless bundle) and
/// produces the schema v1 layout snapshot JSON — the exact contract of
/// <see cref="DocumentPdfExportRequest.LayoutSnapshotJson"/>, consumable by
/// <c>TempoDocumentPdfRenderer</c> for WYSIWYG-parity PDF output without a browser.
/// Implementations are engine-hosting details (<see cref="JintDocumentLayoutEngine"/> today,
/// swappable for another JS host or a native port without touching consumers).
/// </summary>
public interface ITempoDocumentLayoutService
{
    /// <summary>
    /// Generates the layout snapshot JSON (schema v1) for a document.
    /// </summary>
    /// <param name="document">Document to lay out.</param>
    /// <param name="pageSetup">Optional page setup override (size, orientation, margins in points);
    /// null keeps the document's own page settings.</param>
    /// <param name="fonts">Font faces to measure with — the same bytes the PDF renderer embeds.
    /// Required: layout fails closed without fonts, and when the document references a family or
    /// glyph the fonts cannot measure (<see cref="TempoDocumentLayoutException"/> carries the
    /// diagnostics).</param>
    /// <param name="reviewDisplayMode">Tracked-changes display mode for the snapshot
    /// (redline printing in markup modes).</param>
    string GenerateLayoutSnapshotJson(
        DocumentEditorDocument document,
        DocumentPdfPageSetupOptions? pageSetup = null,
        IReadOnlyList<ReportPdfFontFace>? fonts = null,
        DocumentReviewDisplayMode reviewDisplayMode = DocumentReviewDisplayMode.AllMarkup);
}

/// <summary>A glyph the provided fonts cannot measure.</summary>
/// <param name="Family">Resolved font family whose table lacks the glyph.</param>
/// <param name="CodePoint">Unicode code point of the unmeasurable character.</param>
public sealed record TempoDocumentLayoutMissingGlyph(string Family, int CodePoint);

/// <summary>
/// Fail-closed headless layout error: the layout could not be produced with WYSIWYG parity.
/// Carries measurement diagnostics — families the font tables do not cover and glyphs missing
/// from resolved faces.
/// </summary>
public sealed class TempoDocumentLayoutException : Exception
{
    /// <summary>Creates a layout exception with optional measurement diagnostics.</summary>
    public TempoDocumentLayoutException(
        string message,
        IReadOnlyList<string>? unknownFontFamilies = null,
        IReadOnlyList<TempoDocumentLayoutMissingGlyph>? missingGlyphs = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        UnknownFontFamilies = unknownFontFamilies ?? [];
        MissingGlyphs = missingGlyphs ?? [];
    }

    /// <summary>Font families the document references that the provided fonts cannot resolve.</summary>
    public IReadOnlyList<string> UnknownFontFamilies { get; }

    /// <summary>Glyphs missing from the resolved font faces.</summary>
    public IReadOnlyList<TempoDocumentLayoutMissingGlyph> MissingGlyphs { get; }
}
