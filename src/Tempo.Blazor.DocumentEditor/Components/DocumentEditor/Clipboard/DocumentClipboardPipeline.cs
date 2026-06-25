using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;

namespace Tempo.Blazor.Components.DocumentEditor.Clipboard;

/// <summary>Runs clipboard input through detection, normalization, conversion, insertion policy stages.</summary>
public sealed class DocumentClipboardPipeline
{
    private readonly IReadOnlyList<IDocumentClipboardNormalizer> _normalizers;

    /// <summary>Initializes the pipeline with an ordered list of normalizers.</summary>
    public DocumentClipboardPipeline(IEnumerable<IDocumentClipboardNormalizer> normalizers)
    {
        _normalizers = [.. normalizers.OrderByDescending(n => n.Priority)];
    }

    /// <summary>Processes clipboard input and returns normalized blocks.</summary>
    public DocumentClipboardOutput Process(DocumentClipboardInput input)
    {
        var detected = input.Source == DocumentClipboardSource.Unknown
            ? Detect(new DocumentClipboardRawInput
            {
                Html = input.Html,
                PlainText = input.PlainText,
                Files = input.Files
            })
            : new DocumentClipboardSourceDetectionResult { Source = input.Source, Confidence = 1, Reason = "explicit-source" };

        var detectedInput = new DocumentClipboardInput
        {
            Html = input.Html,
            PlainText = input.PlainText,
            Source = detected.Source,
            Files = input.Files
        };

        var warnings = new List<DocumentClipboardWarning>();
        foreach (var normalizer in _normalizers)
        {
            if (!normalizer.CanHandle(detectedInput))
                continue;

            var output = normalizer.Normalize(detectedInput);
            warnings.AddRange(output.Warnings);
            if (output.Blocks.Count > 0)
            {
                return new DocumentClipboardOutput
                {
                    Blocks = output.Blocks,
                    Source = output.Source == DocumentClipboardSource.Unknown ? detected.Source : output.Source,
                    Warnings = warnings
                };
            }
        }

        var fallback = FallbackToPlainText(detectedInput);
        return new DocumentClipboardOutput
        {
            Blocks = fallback.Blocks,
            Source = fallback.Source == DocumentClipboardSource.Unknown ? detected.Source : fallback.Source,
            Warnings = warnings.Concat(fallback.Warnings).ToList()
        };
    }

    /// <summary>Processes raw clipboard input and applies insertion policy for the target region.</summary>
    public DocumentClipboardInsertionResult ProcessForInsertion(DocumentClipboardRawInput raw, DocumentEditorRegion region)
    {
        var detected = Detect(raw);
        var output = Process(new DocumentClipboardInput
        {
            Html = raw.Html,
            PlainText = raw.PlainText,
            Source = detected.Source,
            Files = raw.Files
        });
        var policy = new DocumentInsertionPolicy().Apply(output.Blocks, region);
        return DocumentClipboardInsertionResult.FromPolicy(output, policy, region);
    }

    /// <summary>Detects the likely source for raw clipboard payload.</summary>
    public static DocumentClipboardSourceDetectionResult Detect(DocumentClipboardRawInput raw)
    {
        var html = raw.Html ?? string.Empty;
        var plain = raw.PlainText?.Trim() ?? string.Empty;
        var mimeTypes = raw.MimeTypes;

        if (mimeTypes.Contains("application/x-tempo-document-fragment", StringComparer.OrdinalIgnoreCase)
            || html.Contains("data-tempo-clipboard", StringComparison.OrdinalIgnoreCase))
        {
            return new() { Source = DocumentClipboardSource.Internal, Confidence = 1, Reason = "tempo-marker" };
        }

        if (html.Contains("xmlns:w=", StringComparison.OrdinalIgnoreCase)
            || html.Contains("schemas-microsoft-com", StringComparison.OrdinalIgnoreCase)
            || html.Contains("MsoNormal", StringComparison.Ordinal)
            || html.Contains("MsoList", StringComparison.Ordinal)
            || html.Contains("MsoTable", StringComparison.Ordinal))
        {
            return new() { Source = DocumentClipboardSource.Word, Confidence = 0.95, Reason = "office-html" };
        }

        if (html.Contains("docs-internal-guid", StringComparison.Ordinal))
        {
            return new() { Source = DocumentClipboardSource.GoogleDocs, Confidence = 0.95, Reason = "google-docs-marker" };
        }

        if (html.Contains("google-sheets-html-origin", StringComparison.Ordinal)
            || html.Contains("data-sheets-value", StringComparison.Ordinal)
            || (!string.IsNullOrWhiteSpace(plain) && plain.Contains('\t')))
        {
            return new() { Source = DocumentClipboardSource.GoogleSheets, Confidence = 0.9, Reason = "spreadsheet-shape" };
        }

        if (string.IsNullOrWhiteSpace(html)
            && Uri.TryCreate(plain, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return new() { Source = DocumentClipboardSource.Url, Confidence = 0.9, Reason = "url-only-text" };
        }

        if (!string.IsNullOrWhiteSpace(html))
        {
            return new() { Source = DocumentClipboardSource.RawHtml, Confidence = 0.5, Reason = "html-fallback" };
        }

        return new() { Source = DocumentClipboardSource.PlainText, Confidence = string.IsNullOrWhiteSpace(plain) ? 0 : 0.4, Reason = "plain-text-fallback" };
    }

    private static DocumentClipboardOutput FallbackToPlainText(DocumentClipboardInput input)
    {
        var text = input.PlainText ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
            return new DocumentClipboardOutput();

        var lines = text.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
        var blocks = lines
            .Select(line => new DocumentBlock
            {
                Content = new ParagraphBlockContent
                {
                    Inlines = [new TextRun { Text = line }]
                }
            })
            .ToList();

        return new DocumentClipboardOutput { Blocks = blocks, Source = DocumentClipboardSource.PlainText };
    }
}
