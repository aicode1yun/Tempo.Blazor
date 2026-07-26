using Tempo.Blazor.DocumentEditor.Interfaces;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.DocumentEditor.Services;

/// <summary>Creates read-only document previews with template tokens resolved to display values.</summary>
public sealed class DocumentTemplatePreviewService
{
    private readonly IDocumentTokenValueProvider _tokenValueProvider;

    /// <summary>Creates the service.</summary>
    public DocumentTemplatePreviewService(IDocumentTokenValueProvider tokenValueProvider)
    {
        _tokenValueProvider = tokenValueProvider;
    }

    /// <summary>
    /// Creates a cloned preview document with full document assembly applied: token runs are
    /// replaced by resolved values, conditional block chains are evaluated, repeating sections are
    /// expanded, and computed token expressions are calculated against the resolved values.
    /// </summary>
    public async Task<DocumentEditorDocument> CreatePreviewAsync(
        DocumentEditorDocument document,
        DocumentTokenResolutionContext context,
        CancellationToken cancellationToken = default)
    {
        var preview = Clone(document);
        var tokens = DocumentTokenHelper.ExtractTokens(preview);
        // The provider is always consulted: assembly expressions and conditional blocks may
        // reference values that never appear as token runs in the template body.
        var values = await _tokenValueProvider.ResolveTokenValuesAsync(context, tokens, cancellationToken);
        return new DocumentAssemblyService().Assemble(preview, values);
    }

    private static T Clone<T>(T value)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(value, DocumentEditorJson.Options);
        return System.Text.Json.JsonSerializer.Deserialize<T>(json, DocumentEditorJson.Options)
            ?? throw new System.Text.Json.JsonException("Could not clone document editor value.");
    }
}
