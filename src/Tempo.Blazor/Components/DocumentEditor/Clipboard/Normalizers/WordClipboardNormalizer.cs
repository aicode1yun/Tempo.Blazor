using System.Text.RegularExpressions;
using AngleSharp;
using AngleSharp.Dom;
using Tempo.Blazor.DocumentEditor.Models;

using AngleSharpConfig = AngleSharp.Configuration;

namespace Tempo.Blazor.Components.DocumentEditor.Clipboard.Normalizers;

/// <summary>
/// Normalizes HTML clipboard content that originated from Microsoft Word or Office applications.
/// Detects Office markup (xmlns:w, MsoNormal class, mso-* styles), strips noise, and maps to document blocks.
/// </summary>
public sealed partial class WordClipboardNormalizer : IDocumentClipboardNormalizer
{
    /// <inheritdoc/>
    public int Priority => 100;

    [GeneratedRegex(@"mso-[a-z\-]+:[^;""']+;?", RegexOptions.IgnoreCase)]
    private static partial Regex MsoStylePattern();

    [GeneratedRegex(@"class=""?Mso[A-Za-z]+""?", RegexOptions.IgnoreCase)]
    private static partial Regex MsoClassPattern();

    /// <inheritdoc/>
    public bool CanHandle(DocumentClipboardInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Html)) return false;
        var html = input.Html;
        return html.Contains("xmlns:w=", StringComparison.OrdinalIgnoreCase)
            || html.Contains("schemas-microsoft-com", StringComparison.OrdinalIgnoreCase)
            || html.Contains("MsoNormal", StringComparison.Ordinal)
            || html.Contains("MsoList", StringComparison.Ordinal)
            || html.Contains("MsoTable", StringComparison.Ordinal);
    }

    /// <inheritdoc/>
    public DocumentClipboardOutput Normalize(DocumentClipboardInput input)
    {
        var cleaned = CleanWordHtml(input.Html!);
        var cleanedInput = new DocumentClipboardInput
        {
            Html = cleaned,
            PlainText = input.PlainText,
            Source = DocumentClipboardSource.Word,
            Files = input.Files
        };

        var output = new RawHtmlClipboardNormalizer().Normalize(cleanedInput);
        return new DocumentClipboardOutput
        {
            Blocks = output.Blocks,
            Source = DocumentClipboardSource.Word,
            Warnings = output.Warnings
        };
    }

    private static string CleanWordHtml(string html)
    {
        // Must rewrite Word lists before stripping mso-* styles, so the
        // "mso-list:Ignore" sentinel is still detectable in span attributes.
        html = NormalizeWordLists(html);

        // Strip mso-* CSS properties from inline styles
        html = MsoStylePattern().Replace(html, string.Empty);

        // Remove Word-specific XML element tags
        html = Regex.Replace(html, @"<o:p[^>]*>.*?</o:p>", string.Empty,
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"</?w:[^>]*>", string.Empty, RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"</?o:[^>]*>", string.Empty, RegexOptions.IgnoreCase);

        // Strip remaining Mso* class names so they don't interfere with RawHtmlNormalizer
        html = MsoClassPattern().Replace(html, string.Empty);

        return html;
    }

    private static string NormalizeWordLists(string html)
    {
        // Word list paragraphs use p.MsoListParagraph* with bullet characters embedded.
        // Convert them to <ul><li>...</li></ul> so RawHtmlNormalizer picks them up.
        var context = BrowsingContext.New(AngleSharpConfig.Default);
        var doc = context.OpenAsync(req => req.Content(html)).GetAwaiter().GetResult();
        var body = doc.Body!;

        var listParas = body.QuerySelectorAll("p[class*='MsoListParagraph']").ToList();
        if (listParas.Count == 0) return body.InnerHtml;

        foreach (var para in listParas)
        {
            // Strip the leading bullet span — identified by mso-list:Ignore in style
            var ignore = para.QuerySelector("span[style*='mso-list:Ignore']")
                         ?? para.QuerySelector("span:first-child");
            if (ignore is not null)
            {
                // Only remove if it contains a bullet character or is a list marker
                var ignoreText = ignore.TextContent;
                if (string.IsNullOrWhiteSpace(ignoreText.Replace(" ", "").Replace("•", "").Replace("·", "")))
                    para.RemoveChild(ignore);
            }

            var li = doc.CreateElement("li");
            li.InnerHtml = para.InnerHtml.TrimStart();
            para.Parent?.ReplaceChild(li, para);
        }

        // Wrap consecutive <li> elements in a <ul>
        var allLi = body.QuerySelectorAll("li").ToList();
        if (allLi.Count > 0)
        {
            var ul = doc.CreateElement("ul");
            var firstLi = allLi[0];
            firstLi.Parent?.InsertBefore(ul, firstLi);

            foreach (var li in allLi)
            {
                li.Parent?.RemoveChild(li);
                ul.AppendChild(li);
            }
        }

        return body.InnerHtml;
    }
}
