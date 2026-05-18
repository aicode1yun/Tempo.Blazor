using System.Text.RegularExpressions;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Components.DocumentEditor.Clipboard.Normalizers;

public sealed partial class UrlClipboardNormalizer : IDocumentClipboardNormalizer
{
    /// <inheritdoc/>
    public int Priority => 70;

    [GeneratedRegex(@"^https?://\S+$", RegexOptions.IgnoreCase)]
    private static partial Regex UrlPattern();

    /// <inheritdoc/>
    public bool CanHandle(DocumentClipboardInput input) =>
        string.IsNullOrWhiteSpace(input.Html)
        && !string.IsNullOrWhiteSpace(input.PlainText)
        && UrlPattern().IsMatch(input.PlainText.Trim());

    /// <inheritdoc/>
    public DocumentClipboardOutput Normalize(DocumentClipboardInput input)
    {
        var url = input.PlainText!.Trim();
        var block = new DocumentBlock
        {
            Content = new ParagraphBlockContent
            {
                Inlines =
                [
                    new TextRun
                    {
                        Text = url,
                        Marks = [new InlineMark { Type = InlineMarkType.Link, Link = new LinkMarkData { Href = url } }]
                    }
                ]
            }
        };
        return new DocumentClipboardOutput { Blocks = [block], Source = DocumentClipboardSource.Url };
    }
}
