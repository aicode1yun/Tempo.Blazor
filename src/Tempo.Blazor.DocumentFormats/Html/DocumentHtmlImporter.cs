using System.Text.RegularExpressions;
using System.Xml.Linq;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.DocumentFormats.Html;

/// <summary>Options used when importing HTML into the editor document model.</summary>
public sealed class DocumentHtmlImportOptions
{
    /// <summary>Optional document id assigned to the imported model.</summary>
    public string? DocumentId { get; set; }

    /// <summary>Optional metadata title for the imported document.</summary>
    public string? Title { get; set; }
}

/// <summary>Imports a safe subset of semantic HTML into a <see cref="DocumentEditorDocument"/>.</summary>
public sealed partial class DocumentHtmlImporter
{
    /// <summary>Imports HTML into a document model.</summary>
    public DocumentEditorDocument Import(string html, DocumentHtmlImportOptions? options = null)
    {
        options ??= new DocumentHtmlImportOptions();
        var document = DocumentEditorDocument.Empty(options.DocumentId);
        document.Metadata.Title = options.Title ?? string.Empty;

        if (string.IsNullOrWhiteSpace(html))
        {
            return document;
        }

        var sanitized = DangerousElementRegex().Replace(html, string.Empty);
        var parsed = ParseSanitizedHtml(sanitized);
        if (parsed is null)
        {
            document.Blocks.Add(Paragraph(Regex.Replace(sanitized, "<.*?>", string.Empty), 0));
            return document;
        }

        var contentRoot = parsed.Descendants()
            .FirstOrDefault(element => element.Name.LocalName is "main" or "body")
            ?? parsed.Root!;

        var order = 0d;
        foreach (var node in contentRoot.Nodes())
        {
            AppendBlocks(document.Blocks, node, ref order);
        }

        return document;
    }

    private static XDocument? ParseSanitizedHtml(string html)
    {
        var normalized = VoidElementRegex().Replace(html, "<$1$2 />");
        try
        {
            return XDocument.Parse("<root>" + normalized + "</root>", LoadOptions.PreserveWhitespace);
        }
        catch
        {
            return null;
        }
    }

    private static void AppendBlocks(ICollection<DocumentBlock> blocks, XNode node, ref double order)
    {
        if (node is XText text)
        {
            var value = NormalizeText(text.Value);
            if (!string.IsNullOrWhiteSpace(value))
            {
                blocks.Add(Paragraph(value, order++));
            }

            return;
        }

        if (node is not XElement element)
        {
            return;
        }

        var name = element.Name.LocalName.ToLowerInvariant();
        switch (name)
        {
            case "p":
                var headingLevel = GetWordHeadingLevel(element);
                if (headingLevel > 0)
                {
                    blocks.Add(new DocumentBlock
                    {
                        Type = DocumentBlockType.Heading,
                        Order = order++,
                        Content = new HeadingBlockContent
                        {
                            Level = headingLevel,
                            Inlines = ReadInlines(element).ToList()
                        }
                    });
                    break;
                }

                blocks.Add(new DocumentBlock
                {
                    Type = DocumentBlockType.Paragraph,
                    Order = order++,
                    Content = new ParagraphBlockContent { Inlines = ReadInlines(element).ToList() }
                });
                break;
            case "h1":
            case "h2":
            case "h3":
            case "h4":
            case "h5":
            case "h6":
                blocks.Add(new DocumentBlock
                {
                    Type = DocumentBlockType.Heading,
                    Order = order++,
                    Content = new HeadingBlockContent
                    {
                        Level = int.Parse(name[1].ToString()),
                        Inlines = ReadInlines(element).ToList()
                    }
                });
                break;
            case "ul":
            case "ol":
                foreach (var item in element.Elements().Where(child => child.Name.LocalName.Equals("li", StringComparison.OrdinalIgnoreCase)))
                {
                    blocks.Add(new DocumentBlock
                    {
                        Type = DocumentBlockType.List,
                        Order = order++,
                        Content = new ListBlockContent
                        {
                            Ordered = name == "ol",
                            Inlines = ReadInlines(item).ToList()
                        }
                    });
                }
                break;
            case "blockquote":
                blocks.Add(new DocumentBlock
                {
                    Type = DocumentBlockType.Quote,
                    Order = order++,
                    Content = new QuoteBlockContent { Inlines = ReadInlines(element).ToList() }
                });
                break;
            case "table":
                blocks.Add(new DocumentBlock
                {
                    Type = DocumentBlockType.Table,
                    Order = order++,
                    Content = ReadTable(element)
                });
                break;
            case "img":
                blocks.Add(ReadImage(element, order++));
                break;
            case "hr":
                blocks.Add(new DocumentBlock
                {
                    Type = DocumentBlockType.PageBreak,
                    Order = order++,
                    Content = new PageBreakBlockContent()
                });
                break;
            default:
                foreach (var child in element.Nodes())
                {
                    AppendBlocks(blocks, child, ref order);
                }

                break;
        }
    }

    private static IEnumerable<InlineContent> ReadInlines(XElement element, IReadOnlyList<InlineMark>? inheritedMarks = null)
    {
        inheritedMarks ??= [];
        foreach (var node in element.Nodes())
        {
            if (node is XText text)
            {
                var value = NormalizeText(text.Value);
                if (!string.IsNullOrEmpty(value))
                {
                    yield return new TextRun { Text = value, Marks = inheritedMarks.Select(CloneMark).ToList() };
                }

                continue;
            }

            if (node is not XElement child)
            {
                continue;
            }

            var name = child.Name.LocalName.ToLowerInvariant();
            if (name == "span" && child.Attribute("data-token-key")?.Value is { Length: > 0 } tokenKey)
            {
                yield return new TokenRun
                {
                    Key = tokenKey,
                    DisplayName = NormalizeText(child.Value),
                    Marks = inheritedMarks.Select(CloneMark).ToList()
                };
                continue;
            }

            var nextMarks = inheritedMarks.Concat(CreateMarks(child)).ToList();

            foreach (var inline in ReadInlines(child, nextMarks))
            {
                yield return inline;
            }
        }
    }

    private static IEnumerable<InlineMark> CreateMarks(XElement element)
    {
        var name = element.Name.LocalName.ToLowerInvariant();
        var style = GetAttribute(element, "style") ?? string.Empty;
        if (name is "strong" or "b" || FontWeightRegex().IsMatch(style))
        {
            yield return new InlineMark { Type = InlineMarkType.Bold };
        }

        if (name is "em" or "i" || FontStyleRegex().IsMatch(style))
        {
            yield return new InlineMark { Type = InlineMarkType.Italic };
        }

        if (name == "u" || TextDecorationUnderlineRegex().IsMatch(style))
        {
            yield return new InlineMark { Type = InlineMarkType.Underline };
        }

        if (name is "s" or "strike" or "del" || TextDecorationStrikeRegex().IsMatch(style))
        {
            yield return new InlineMark { Type = InlineMarkType.Strikethrough };
        }

        if (name == "a" && IsSafeUri(GetAttribute(element, "href")))
        {
            yield return new InlineMark
            {
                Type = InlineMarkType.Link,
                Link = new LinkMarkData { Href = GetAttribute(element, "href")! }
            };
        }
    }

    private static TableBlockContent ReadTable(XElement table)
    {
        var rows = table.Descendants()
            .Where(element => element.Name.LocalName.Equals("tr", StringComparison.OrdinalIgnoreCase))
            .Select(row => new TableRowContent
            {
                Cells = row.Elements()
                    .Where(cell => cell.Name.LocalName is "td" or "th")
                    .Select(ReadCell)
                    .ToList()
            })
            .ToList();

        return new TableBlockContent { Rows = rows };
    }

    private static TableCellContent ReadCell(XElement cell)
    {
        var order = 0d;
        var blocks = new List<DocumentBlock>();
        foreach (var node in cell.Nodes())
        {
            AppendBlocks(blocks, node, ref order);
        }

        if (blocks.Count == 0)
        {
            blocks.Add(Paragraph(NormalizeText(cell.Value), 0));
        }

        return new TableCellContent
        {
            ColumnSpan = int.TryParse(GetAttribute(cell, "colspan"), out var columns) ? Math.Max(1, columns) : 1,
            RowSpan = int.TryParse(GetAttribute(cell, "rowspan"), out var rows) ? Math.Max(1, rows) : 1,
            Blocks = blocks
        };
    }

    private static DocumentBlock ReadImage(XElement image, double order)
    {
        var src = GetAttribute(image, "src");
        return new DocumentBlock
        {
            Type = DocumentBlockType.Image,
            Order = order,
            Content = new ImageBlockContent
            {
                Source = DocumentImageSource.Url,
                Url = IsSafeUri(src, allowImageDataUri: true) ? src : null,
                AltText = GetAttribute(image, "alt")
            }
        };
    }

    private static DocumentBlock Paragraph(string text, double order)
    {
        return new DocumentBlock
        {
            Type = DocumentBlockType.Paragraph,
            Order = order,
            Content = new ParagraphBlockContent
            {
                Inlines = [new TextRun { Text = NormalizeText(text) }]
            }
        };
    }

    private static InlineMark CloneMark(InlineMark mark)
    {
        return new InlineMark
        {
            Type = mark.Type,
            Link = mark.Link is null ? null : new LinkMarkData { Href = mark.Link.Href, Title = mark.Link.Title },
            CommentAnchor = mark.CommentAnchor is null ? null : new CommentAnchorMarkData { CommentId = mark.CommentAnchor.CommentId, AnchorId = mark.CommentAnchor.AnchorId },
            RevisionId = mark.RevisionId,
            Value = mark.Value
        };
    }

    private static string NormalizeText(string value)
    {
        return Regex.Replace(value, @"\s+", " ").Trim();
    }

    private static string? GetAttribute(XElement element, string localName)
    {
        return element.Attributes()
            .FirstOrDefault(attribute => attribute.Name.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase))
            ?.Value;
    }

    private static int GetWordHeadingLevel(XElement element)
    {
        var text = string.Join(" ", GetAttribute(element, "class"), GetAttribute(element, "style"));
        var match = WordHeadingRegex().Match(text);
        return match.Success && int.TryParse(match.Groups[1].Value, out var level)
            ? Math.Clamp(level, 1, 6)
            : 0;
    }

    private static bool IsSafeUri(string? value, bool allowImageDataUri = false)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (allowImageDataUri && value.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && uri.Scheme is "http" or "https" or "mailto";
    }

    [GeneratedRegex("<\\s*(script|style|iframe|object|embed)[^>]*>.*?<\\s*/\\s*\\1\\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex DangerousElementRegex();

    [GeneratedRegex("<(br|hr|img)([^>/]*?)>", RegexOptions.IgnoreCase)]
    private static partial Regex VoidElementRegex();

    [GeneratedRegex("font-weight\\s*:\\s*(bold|[7-9]00)", RegexOptions.IgnoreCase)]
    private static partial Regex FontWeightRegex();

    [GeneratedRegex("font-style\\s*:\\s*italic", RegexOptions.IgnoreCase)]
    private static partial Regex FontStyleRegex();

    [GeneratedRegex("text-decoration[^;]*underline", RegexOptions.IgnoreCase)]
    private static partial Regex TextDecorationUnderlineRegex();

    [GeneratedRegex("text-decoration[^;]*line-through", RegexOptions.IgnoreCase)]
    private static partial Regex TextDecorationStrikeRegex();

    [GeneratedRegex("heading\\s*([1-6])", RegexOptions.IgnoreCase)]
    private static partial Regex WordHeadingRegex();
}
