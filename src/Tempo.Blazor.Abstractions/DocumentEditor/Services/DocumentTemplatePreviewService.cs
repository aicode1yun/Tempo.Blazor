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

    /// <summary>Creates a cloned preview document with token runs replaced by text values.</summary>
    public async Task<DocumentEditorDocument> CreatePreviewAsync(
        DocumentEditorDocument document,
        DocumentTokenResolutionContext context,
        CancellationToken cancellationToken = default)
    {
        var preview = Clone(document);
        var tokens = DocumentTokenHelper.ExtractTokens(preview);
        if (tokens.Count == 0)
        {
            return preview;
        }

        var values = await _tokenValueProvider.ResolveTokenValuesAsync(context, tokens, cancellationToken);
        ReplaceTokens(preview, values);
        return preview;
    }

    private static void ReplaceTokens(
        DocumentEditorDocument document,
        IReadOnlyDictionary<string, DocumentTokenValue> values)
    {
        foreach (var block in document.Blocks)
        {
            var inlines = GetInlineList(block.Content);
            if (inlines is null)
            {
                continue;
            }

            for (var i = 0; i < inlines.Count; i++)
            {
                if (inlines[i] is not TokenRun token)
                {
                    continue;
                }

                values.TryGetValue(token.Key, out var value);
                inlines[i] = new TextRun
                {
                    Text = GetPreviewText(token, value),
                    Marks = token.Marks.Select(mark => Clone(mark)).ToList()
                };
            }
        }
    }

    private static string GetPreviewText(TokenRun token, DocumentTokenValue? value)
    {
        if (value?.HasValue == true)
        {
            return value.DisplayValue ?? value.Value ?? string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(token.FallbackText))
        {
            return token.FallbackText!;
        }

        return $"{{{{{token.Key}}}}}";
    }

    private static List<InlineContent>? GetInlineList(DocumentBlockContent content)
    {
        return content switch
        {
            ParagraphBlockContent paragraph => paragraph.Inlines,
            HeadingBlockContent heading => heading.Inlines,
            ListBlockContent list => list.Inlines,
            QuoteBlockContent quote => quote.Inlines,
            _ => null
        };
    }

    private static T Clone<T>(T value)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(value, DocumentEditorJson.Options);
        return System.Text.Json.JsonSerializer.Deserialize<T>(json, DocumentEditorJson.Options)
            ?? throw new System.Text.Json.JsonException("Could not clone document editor value.");
    }
}
