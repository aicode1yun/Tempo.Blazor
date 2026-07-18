using System.Collections.Concurrent;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Demo.Api.Services;

/// <summary>
/// Keeps the last PDF produced by the POST export endpoint per document so demo surfaces
/// (TmPdfViewer via <c>/pdf-viewer?url=…</c>) and E2E tests can open the real exported file.
/// </summary>
public sealed class DemoDocumentPdfExportCache
{
    private readonly ConcurrentDictionary<string, DocumentPdfExportResult> _lastExports = new(StringComparer.Ordinal);

    /// <summary>Stores the last export result for a document.</summary>
    public void Store(string documentId, DocumentPdfExportResult result)
        => _lastExports[documentId] = result;

    /// <summary>Returns the last export result for a document, or null when none exists.</summary>
    public DocumentPdfExportResult? Get(string documentId)
        => _lastExports.TryGetValue(documentId, out var result) ? result : null;
}
