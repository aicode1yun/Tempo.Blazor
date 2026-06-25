using AngleSharp;
using AngleSharp.Dom;
using Tempo.Blazor.DocumentEditor.Models;

using AngleSharpConfig = AngleSharp.Configuration;

namespace Tempo.Blazor.Components.DocumentEditor.Clipboard.Normalizers;

/// <summary>
/// Normalizes HTML clipboard content from Google Docs.
/// Detects the docs-internal-guid marker, converts font-weight/font-style CSS spans to semantic marks,
/// and delegates final block extraction to RawHtmlClipboardNormalizer.
/// </summary>
public sealed class GoogleDocsClipboardNormalizer : IDocumentClipboardNormalizer
{
    /// <inheritdoc/>
    public int Priority => 90;

    /// <inheritdoc/>
    public bool CanHandle(DocumentClipboardInput input) =>
        !string.IsNullOrWhiteSpace(input.Html)
        && input.Html.Contains("docs-internal-guid", StringComparison.Ordinal);

    /// <inheritdoc/>
    public DocumentClipboardOutput Normalize(DocumentClipboardInput input)
    {
        var html = ConvertStyleSpansToSemanticTags(input.Html!);
        var cleaned = new DocumentClipboardInput
        {
            Html = html,
            PlainText = input.PlainText,
            Source = DocumentClipboardSource.GoogleDocs,
            Files = input.Files
        };

        var output = new RawHtmlClipboardNormalizer().Normalize(cleaned);
        return new DocumentClipboardOutput
        {
            Blocks = output.Blocks,
            Source = DocumentClipboardSource.GoogleDocs,
            Warnings = output.Warnings
        };
    }

    /// <summary>
    /// Google Docs uses inline CSS (font-weight:700, font-style:italic) on span elements.
    /// This method replaces those spans with semantic &lt;strong&gt;, &lt;em&gt;, &lt;u&gt; tags
    /// so the RawHtmlClipboardNormalizer can apply the corresponding marks.
    /// </summary>
    private static string ConvertStyleSpansToSemanticTags(string html)
    {
        var context = BrowsingContext.New(AngleSharpConfig.Default);
        var doc = context.OpenAsync(req => req.Content(html)).GetAwaiter().GetResult();
        var body = doc.Body!;

        // Walk all spans and lift formatting into semantic wrappers
        foreach (var span in body.QuerySelectorAll("span").ToList())
        {
            var style = span.GetAttribute("style") ?? string.Empty;
            span.RemoveAttribute("style");
            span.RemoveAttribute("class");

            if (style.Contains("font-weight:700", StringComparison.OrdinalIgnoreCase)
                || style.Contains("font-weight: 700", StringComparison.OrdinalIgnoreCase)
                || style.Contains("font-weight:bold", StringComparison.OrdinalIgnoreCase))
            {
                WrapContentInTag(doc, span, "strong");
            }

            if (style.Contains("font-style:italic", StringComparison.OrdinalIgnoreCase)
                || style.Contains("font-style: italic", StringComparison.OrdinalIgnoreCase))
            {
                WrapContentInTag(doc, span, "em");
            }

            if (style.Contains("text-decoration:underline", StringComparison.OrdinalIgnoreCase)
                || style.Contains("text-decoration: underline", StringComparison.OrdinalIgnoreCase))
            {
                WrapContentInTag(doc, span, "u");
            }
        }

        return body.InnerHtml;
    }

    private static void WrapContentInTag(IDocument doc, IElement el, string tagName)
    {
        var wrapper = doc.CreateElement(tagName);
        // Move children into wrapper
        while (el.ChildNodes.Length > 0)
            wrapper.AppendChild(el.ChildNodes[0]);
        el.AppendChild(wrapper);
    }
}
