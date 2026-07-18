using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;
using Tempo.Blazor.DocumentFormats.Docx;

namespace Tempo.Blazor.DocumentFormats.Redline;

/// <summary>
/// Exports a <see cref="DocumentCompareResult"/> as a redline DOCX: the comparison is first turned
/// into a tracked-changes document by <see cref="DocumentRedlineBuilder"/> and then written by the
/// standard DOCX exporter, whose revision support emits real <c>w:ins</c>/<c>w:del</c> elements —
/// Word (and the DOCX importer) see reviewable tracked changes with author and date.
/// </summary>
public sealed class DocumentRedlineDocxExporter
{
    private readonly DocumentRedlineBuilder _builder = new();
    private readonly DocumentDocxExporter _exporter = new();

    /// <summary>Builds the redline document and exports it to DOCX bytes.</summary>
    public async Task<DocumentFormatExportResult> ExportAsync(
        DocumentCompareResult compareResult,
        DocumentRedlineOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var redline = _builder.Build(compareResult, options);
        var export = await _exporter.ExportAsync(redline, options: null, cancellationToken);
        export.FileName = string.IsNullOrWhiteSpace(redline.Metadata.Title)
            ? $"{redline.DocumentId}-redline.docx"
            : $"{redline.Metadata.Title}-redline.docx";
        return export;
    }
}
