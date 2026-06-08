using System.Text.Json;
using Tempo.Blazor.DocumentEditor.Interfaces;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Components.DocumentEditor;

/// <summary>Routes canvas-backed document snapshots into document editor provider boundaries.</summary>
public sealed class CanvasExportBridge
{
    private readonly Func<CancellationToken, Task<DocumentEditorDocument>> _snapshotProvider;

    /// <summary>Creates a provider bridge that requests the current editor snapshot for each operation.</summary>
    public CanvasExportBridge(Func<CancellationToken, Task<DocumentEditorDocument>> snapshotProvider)
    {
        _snapshotProvider = snapshotProvider ?? throw new ArgumentNullException(nameof(snapshotProvider));
    }

    /// <summary>Requests the current snapshot from the live editor boundary.</summary>
    public async Task<DocumentEditorDocument> RequestSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var document = await _snapshotProvider(cancellationToken);
        return Clone(document);
    }

    /// <summary>Exports the current snapshot through an external document format provider.</summary>
    public async Task<DocumentFormatExportProviderResult> ExportFormatAsync(
        IDocumentFormatProvider provider,
        DocumentFormatProviderKind format,
        DocumentEditorAuthor? author,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var document = await RequestSnapshotAsync(cancellationToken);
        return await provider.ExportAsync(new DocumentFormatExportProviderRequest
        {
            DocumentId = document.DocumentId,
            Format = format,
            Document = Clone(document),
            FileName = ResolveFileName(document),
            Author = author
        }, cancellationToken);
    }

    /// <summary>Exports the current snapshot through a PDF provider.</summary>
    public async Task<DocumentPdfExportResult> ExportPdfAsync(
        IDocumentPdfExportProvider provider,
        DocumentEditorAuthor? author,
        Func<DocumentEditorDocument, DocumentPdfExportOptions> optionsFactory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(optionsFactory);

        var document = await RequestSnapshotAsync(cancellationToken);
        return await provider.ExportPdfAsync(new DocumentPdfExportRequest
        {
            DocumentId = document.DocumentId,
            Document = Clone(document),
            FileName = ResolveFileName(document),
            Author = author,
            Options = optionsFactory(document)
        }, cancellationToken);
    }

    /// <summary>Builds a compare source from the current snapshot.</summary>
    public async Task<DocumentCompareSource> CreateCurrentCompareSourceAsync(
        string label,
        CancellationToken cancellationToken = default)
    {
        var document = await RequestSnapshotAsync(cancellationToken);
        return new DocumentCompareSource
        {
            Kind = DocumentCompareSourceKind.Current,
            DocumentId = document.DocumentId,
            Document = Clone(document),
            JsonSnapshot = DocumentEditorJson.Serialize(document),
            Label = label
        };
    }

    /// <summary>Builds the complete debug payload from the current snapshot and optional runtime details.</summary>
    public async Task<string> BuildDebugJsonAsync(
        Func<CancellationToken, Task<string?>> runtimeDebugProvider,
        object? docxDrawingMetadata,
        object? runtimeRecovery,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(runtimeDebugProvider);

        var document = await RequestSnapshotAsync(cancellationToken);
        var runtimeDebugJson = await runtimeDebugProvider(cancellationToken);
        JsonElement? runtimeDebug = string.IsNullOrWhiteSpace(runtimeDebugJson)
            ? null
            : JsonSerializer.Deserialize<JsonElement>(runtimeDebugJson);

        return JsonSerializer.Serialize(new
        {
            canonicalDocument = document,
            runtimeDebug,
            docxDrawingMetadata,
            runtimeRecovery
        }, new JsonSerializerOptions(DocumentEditorJson.Options)
        {
            WriteIndented = true
        });
    }

    private static string ResolveFileName(DocumentEditorDocument document)
        => string.IsNullOrWhiteSpace(document.Metadata.Title)
            ? document.DocumentId
            : document.Metadata.Title;

    private static T Clone<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, DocumentEditorJson.Options);
        return JsonSerializer.Deserialize<T>(json, DocumentEditorJson.Options)
            ?? throw new JsonException("Could not clone document editor value.");
    }
}
