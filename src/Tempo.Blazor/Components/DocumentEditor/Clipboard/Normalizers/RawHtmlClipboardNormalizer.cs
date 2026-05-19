using AngleSharp;
using AngleSharp.Dom;
using System.Globalization;
using Tempo.Blazor.DocumentEditor.Models;

// Alias to avoid conflict with project's Tempo.Blazor.Configuration namespace
using AngleSharpConfig = AngleSharp.Configuration;

namespace Tempo.Blazor.Components.DocumentEditor.Clipboard.Normalizers;

/// <summary>
/// Parses arbitrary HTML clipboard content into document blocks using an allowlist approach.
/// Strips dangerous elements (script, iframe, form) and maps safe elements to the document model.
/// </summary>
public sealed class RawHtmlClipboardNormalizer : IDocumentClipboardNormalizer
{
    private static readonly HashSet<string> BlockTags =
        new(StringComparer.OrdinalIgnoreCase) { "p", "div", "h1", "h2", "h3", "h4", "h5", "h6", "blockquote", "ul", "ol", "table" };

    private static readonly HashSet<string> StripTags =
        new(StringComparer.OrdinalIgnoreCase) { "script", "style", "iframe", "frame", "form", "input", "textarea", "button", "select", "object", "embed", "applet", "head", "meta", "link" };

    /// <inheritdoc/>
    public int Priority => 10;

    /// <inheritdoc/>
    public bool CanHandle(DocumentClipboardInput input) =>
        !string.IsNullOrWhiteSpace(input.Html);

    /// <inheritdoc/>
    public DocumentClipboardOutput Normalize(DocumentClipboardInput input)
    {
        var html = input.Html!;
        var context = BrowsingContext.New(AngleSharpConfig.Default);
        var document = context.OpenAsync(req => req.Content(html)).GetAwaiter().GetResult();
        var body = document.Body ?? document.DocumentElement;

        var blocks = new List<DocumentBlock>();
        var warnings = new List<DocumentClipboardWarning>();
        CollectBlocks(body, blocks, warnings);

        return new DocumentClipboardOutput
        {
            Blocks = blocks,
            Source = input.Source == DocumentClipboardSource.Unknown ? DocumentClipboardSource.RawHtml : input.Source,
            Warnings = warnings
        };
    }

    private static void CollectBlocks(IElement parent, List<DocumentBlock> blocks, List<DocumentClipboardWarning> warnings)
    {
        foreach (var node in parent.ChildNodes)
        {
            if (node is IElement el)
            {
                var tag = el.TagName.ToLowerInvariant();

                if (StripTags.Contains(tag))
                {
                    warnings.Add(new DocumentClipboardWarning
                    {
                        Code = "stripped-element",
                        Message = $"Removed unsupported <{tag}> element from pasted HTML."
                    });
                    continue;
                }

                switch (tag)
                {
                    case "p":
                        var paraInlines = CollectInlines(el, warnings);
                        if (HasVisibleContent(paraInlines))
                            blocks.Add(new DocumentBlock { Content = new ParagraphBlockContent { Inlines = paraInlines } });
                        break;

                    case "div":
                        // If the div contains block-level children, recurse; otherwise treat as a paragraph.
                        if (el.Children.Any(c => BlockTags.Contains(c.TagName.ToLowerInvariant())))
                        {
                            CollectBlocks(el, blocks, warnings);
                        }
                        else
                        {
                            var divInlines = CollectInlines(el, warnings);
                            if (HasVisibleContent(divInlines))
                                blocks.Add(new DocumentBlock { Content = new ParagraphBlockContent { Inlines = divInlines } });
                        }
                        break;

                    case "h1": case "h2": case "h3": case "h4": case "h5": case "h6":
                        var level = int.Parse(tag[1..]);
                        var headingInlines = CollectInlines(el, warnings);
                        blocks.Add(new DocumentBlock
                        {
                            Type = DocumentBlockType.Heading,
                            Content = new HeadingBlockContent { Level = level, Inlines = headingInlines }
                        });
                        break;

                    case "blockquote":
                        var quoteInlines = CollectInlines(el, warnings);
                        if (quoteInlines.Count > 0)
                            blocks.Add(new DocumentBlock
                            {
                                Type = DocumentBlockType.Quote,
                                Content = new QuoteBlockContent { Inlines = quoteInlines }
                            });
                        break;

                    case "ul":
                        CollectListItems(el, ordered: false, blocks, warnings);
                        break;

                    case "ol":
                        CollectListItems(el, ordered: true, blocks, warnings);
                        break;

                    case "table":
                        var tableBlock = ParseTable(el, warnings);
                        if (tableBlock is not null)
                            blocks.Add(tableBlock);
                        break;

                    default:
                        // Unknown block-like container: recurse
                        CollectBlocks(el, blocks, warnings);
                        break;
                }
            }
            else if (node is IText text && !string.IsNullOrWhiteSpace(text.TextContent))
            {
                blocks.Add(new DocumentBlock
                {
                    Content = new ParagraphBlockContent
                    {
                        Inlines = [new TextRun { Text = text.TextContent.Trim() }]
                    }
                });
            }
        }
    }

    private static void CollectListItems(IElement listEl, bool ordered, List<DocumentBlock> blocks, List<DocumentClipboardWarning> warnings)
    {
        foreach (var child in listEl.Children)
        {
            if (!child.TagName.Equals("LI", StringComparison.OrdinalIgnoreCase))
                continue;

            var inlines = CollectInlines(child, warnings);
            blocks.Add(new DocumentBlock
            {
                Type = DocumentBlockType.List,
                Content = new ListBlockContent { Ordered = ordered, Inlines = inlines }
            });
        }
    }

    private static DocumentBlock? ParseTable(IElement tableEl, List<DocumentClipboardWarning> warnings)
    {
        var rows = new List<TableRowContent>();
        foreach (var rowEl in tableEl.QuerySelectorAll("tr"))
        {
            var cells = new List<TableCellContent>();
            foreach (var cellEl in rowEl.Children)
            {
                var tag = cellEl.TagName.ToLowerInvariant();
                if (tag is not "td" and not "th") continue;
                var inlines = CollectInlines(cellEl, warnings);
                var cellBlock = new DocumentBlock { Content = new ParagraphBlockContent { Inlines = inlines } };
                cells.Add(new TableCellContent
                {
                    Blocks = [cellBlock],
                    ColumnSpan = Math.Max(1, ReadPositiveIntAttribute(cellEl, "colspan")),
                    RowSpan = Math.Max(1, ReadPositiveIntAttribute(cellEl, "rowspan"))
                });
            }
            if (cells.Count > 0)
                rows.Add(new TableRowContent { Cells = cells });
        }

        if (rows.Count == 0) return null;
        return new DocumentBlock
        {
            Type = DocumentBlockType.Table,
            Content = new TableBlockContent { Rows = rows }
        };
    }

    private static int ReadPositiveIntAttribute(IElement element, string name)
    {
        return int.TryParse(element.GetAttribute(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0
            ? value
            : 1;
    }

    private static bool HasVisibleContent(List<InlineContent> inlines) =>
        inlines.Count > 0 && inlines.Any(i => i is not TextRun tr || !string.IsNullOrWhiteSpace(tr.Text));

    private static List<InlineContent> CollectInlines(IElement el, List<DocumentClipboardWarning> warnings)
    {
        var result = new List<InlineContent>();
        CollectInlineNodes(el.ChildNodes, result, [], warnings);
        return result;
    }

    private static void CollectInlineNodes(
        INodeList nodes,
        List<InlineContent> result,
        List<InlineMark> inheritedMarks,
        List<DocumentClipboardWarning> warnings)
    {
        foreach (var node in nodes)
        {
            if (node is IText text)
            {
                var value = text.TextContent;
                if (string.IsNullOrEmpty(value)) continue;
                result.Add(new TextRun { Text = value, Marks = [.. inheritedMarks] });
            }
            else if (node is IElement el)
            {
                var tag = el.TagName.ToLowerInvariant();
                if (StripTags.Contains(tag))
                {
                    warnings.Add(new DocumentClipboardWarning
                    {
                        Code = "stripped-element",
                        Message = $"Removed unsupported <{tag}> element from pasted HTML."
                    });
                    continue;
                }

                var childMarks = new List<InlineMark>(inheritedMarks);
                switch (tag)
                {
                    case "strong": case "b":
                        childMarks.Add(new InlineMark { Type = InlineMarkType.Bold });
                        break;
                    case "em": case "i":
                        childMarks.Add(new InlineMark { Type = InlineMarkType.Italic });
                        break;
                    case "u":
                        childMarks.Add(new InlineMark { Type = InlineMarkType.Underline });
                        break;
                    case "s": case "strike": case "del":
                        childMarks.Add(new InlineMark { Type = InlineMarkType.Strikethrough });
                        break;
                    case "sub":
                        childMarks.Add(new InlineMark { Type = InlineMarkType.Subscript });
                        break;
                    case "sup":
                        childMarks.Add(new InlineMark { Type = InlineMarkType.Superscript });
                        break;
                    case "a":
                        var href = el.GetAttribute("href");
                        if (!string.IsNullOrEmpty(href) && IsSafeHref(href))
                            childMarks.Add(new InlineMark { Type = InlineMarkType.Link, Link = new LinkMarkData { Href = href } });
                        else if (!string.IsNullOrEmpty(href))
                            warnings.Add(new DocumentClipboardWarning
                            {
                                Code = "unsafe-link-removed",
                                Message = "Removed an unsafe link from pasted HTML."
                            });
                        break;
                }

                // Recurse into inline/unknown elements
                CollectInlineNodes(el.ChildNodes, result, childMarks, warnings);
            }
        }
    }

    private static bool IsSafeHref(string href) =>
        Uri.TryCreate(href, UriKind.Absolute, out var uri)
            ? uri.Scheme is "http" or "https" or "mailto"
            : !href.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase)
              && !href.StartsWith("data:", StringComparison.OrdinalIgnoreCase);
}
