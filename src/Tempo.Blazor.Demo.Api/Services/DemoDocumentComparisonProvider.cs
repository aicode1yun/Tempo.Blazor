using Tempo.Blazor.DocumentEditor.Interfaces;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;

namespace Tempo.Blazor.Demo.Api.Services;

/// <summary>Server-side demo comparison provider for arbitrary document sources.</summary>
public sealed class DemoDocumentComparisonProvider : IDocumentComparisonProvider
{
    private readonly DemoDocumentEditorStore _store;
    private readonly DocumentComparisonService _comparison = new();

    /// <summary>Creates the provider.</summary>
    public DemoDocumentComparisonProvider(DemoDocumentEditorStore store)
    {
        _store = store;
    }

    /// <inheritdoc />
    public async Task<DocumentCompareResult> CompareAsync(
        DocumentCompareRequest request,
        CancellationToken cancellationToken = default)
    {
        var baseDocument = await ResolveSourceAsync(request.BaseSource, request.CurrentDocument, cancellationToken);
        var compareDocument = await ResolveSourceAsync(request.CompareSource, request.CurrentDocument, cancellationToken);
        return _comparison.Compare(baseDocument, compareDocument);
    }

    private async Task<DocumentEditorDocument> ResolveSourceAsync(
        DocumentCompareSource source,
        DocumentEditorDocument? currentDocument,
        CancellationToken cancellationToken)
    {
        if (source.Kind == DocumentCompareSourceKind.Current)
        {
            return Clone(currentDocument ?? source.Document ?? DocumentEditorDocument.Empty());
        }

        if (source.Document is not null)
        {
            return Clone(source.Document);
        }

        if (!string.IsNullOrWhiteSpace(source.JsonSnapshot))
        {
            return DocumentEditorJson.Deserialize(source.JsonSnapshot);
        }

        if (source.Kind == DocumentCompareSourceKind.DocumentId && !string.IsNullOrWhiteSpace(source.DocumentId))
        {
            var loaded = await _store.LoadAsync(source.DocumentId, new DocumentEditorLoadOptions { IncludeDocument = true }, cancellationToken);
            if (loaded.Found && loaded.Document is not null)
            {
                return loaded.Document;
            }
        }

        throw new InvalidOperationException("Document compare source could not be resolved.");
    }

    private static T Clone<T>(T value)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(value, DocumentEditorJson.Options);
        return System.Text.Json.JsonSerializer.Deserialize<T>(json, DocumentEditorJson.Options)!;
    }
}
