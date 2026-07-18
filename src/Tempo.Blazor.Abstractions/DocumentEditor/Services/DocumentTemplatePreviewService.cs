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
        var tokens = ExtractTokensRecursive(preview);
        // The provider is always consulted: assembly expressions and conditional blocks may
        // reference values that never appear as token runs in the template body.
        var values = await _tokenValueProvider.ResolveTokenValuesAsync(context, tokens, cancellationToken);
        return new DocumentAssemblyService().Assemble(preview, values);
    }

    private static IReadOnlyList<TokenRun> ExtractTokensRecursive(DocumentEditorDocument document)
    {
        var tokens = new List<TokenRun>();
        void Walk(DocumentBlock block)
        {
            switch (block.Content)
            {
                case ParagraphBlockContent paragraph:
                    tokens.AddRange(paragraph.Inlines.OfType<TokenRun>());
                    break;
                case HeadingBlockContent heading:
                    tokens.AddRange(heading.Inlines.OfType<TokenRun>());
                    break;
                case ListBlockContent list:
                    tokens.AddRange(list.Inlines.OfType<TokenRun>());
                    break;
                case QuoteBlockContent quote:
                    tokens.AddRange(quote.Inlines.OfType<TokenRun>());
                    break;
                case ContentControlBlockContent control:
                    control.Blocks.ForEach(Walk);
                    break;
                case TableBlockContent table:
                    foreach (var cellBlock in table.Rows.SelectMany(row => row.Cells).SelectMany(cell => cell.Blocks))
                    {
                        Walk(cellBlock);
                    }

                    break;
            }
        }

        document.Blocks.ForEach(Walk);
        return tokens;
    }

    private static T Clone<T>(T value)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(value, DocumentEditorJson.Options);
        return System.Text.Json.JsonSerializer.Deserialize<T>(json, DocumentEditorJson.Options)
            ?? throw new System.Text.Json.JsonException("Could not clone document editor value.");
    }
}
