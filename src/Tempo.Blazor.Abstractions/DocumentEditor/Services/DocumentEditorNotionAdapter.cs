using System.Net;
using System.Text.RegularExpressions;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.NotionEditor.Enums;
using NotionBlock = Tempo.Blazor.NotionEditor.Interfaces.PageBlock;
using NotionHeadingContent = Tempo.Blazor.NotionEditor.Models.HeadingBlockContent;
using NotionImageContent = Tempo.Blazor.NotionEditor.Models.ImageBlockContent;
using NotionListContent = Tempo.Blazor.NotionEditor.Models.ListBlockContent;
using NotionTableContent = Tempo.Blazor.NotionEditor.Models.TableBlockContent;
using NotionTableRowContent = Tempo.Blazor.NotionEditor.Models.TableRowBlockContent;
using NotionTextContent = Tempo.Blazor.NotionEditor.Models.TextBlockContent;

namespace Tempo.Blazor.DocumentEditor.Services;

/// <summary>Adapter between the document editor JSON model and the existing Notion block model.</summary>
public class DocumentEditorNotionAdapter
{
    private static readonly Regex CommentAnchorRegex = new(
        "data-comment-id=\"(?<id>[^\"]+)\"",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Converts a document editor document to Notion blocks.</summary>
    public IReadOnlyList<NotionBlock> ToNotionBlocks(DocumentEditorDocument document, Guid pageId)
    {
        var blocks = new List<NotionBlock>();

        foreach (var block in document.Blocks.OrderBy(item => item.Order))
        {
            blocks.Add(ToNotionBlock(block, pageId));
        }

        return blocks;
    }

    /// <summary>Converts Notion blocks to a document editor document.</summary>
    public DocumentEditorDocument FromNotionBlocks(
        string documentId,
        IReadOnlyList<NotionBlock> blocks)
    {
        var document = DocumentEditorDocument.Empty(documentId);

        foreach (var block in blocks.OrderBy(item => item.Order))
        {
            var documentBlock = FromNotionBlock(block);
            document.Blocks.Add(documentBlock);

            foreach (var commentId in ExtractCommentIds(block))
            {
                document.Comments.Add(new DocumentComment
                {
                    Id = commentId,
                    Anchor = new DocumentCommentAnchor
                    {
                        Type = DocumentCommentAnchorType.Block,
                        BlockId = documentBlock.Id
                    }
                });
            }
        }

        return document;
    }

    private static NotionBlock ToNotionBlock(DocumentBlock block, Guid pageId)
    {
        return new NotionBlock
        {
            Id = TryParseGuid(block.Id),
            PageId = pageId,
            Type = ToNotionBlockType(block),
            Order = Convert.ToInt32(block.Order),
            Content = ToNotionContent(block)
        };
    }

    private static DocumentBlock FromNotionBlock(NotionBlock block)
    {
        return new DocumentBlock
        {
            Id = block.Id == Guid.Empty ? Guid.NewGuid().ToString("N") : block.Id.ToString("N"),
            Type = FromNotionBlockType(block.Type),
            Order = block.Order,
            Content = FromNotionContent(block)
        };
    }

    private static BlockType ToNotionBlockType(DocumentBlock block)
    {
        return block.Content switch
        {
            HeadingBlockContent heading when heading.Level <= 1 => BlockType.Heading1,
            HeadingBlockContent heading when heading.Level == 2 => BlockType.Heading2,
            HeadingBlockContent => BlockType.Heading3,
            ListBlockContent list when list.Ordered => BlockType.NumberedList,
            ListBlockContent => BlockType.BulletList,
            QuoteBlockContent => BlockType.Quote,
            TableBlockContent => BlockType.Table,
            ImageBlockContent => BlockType.Image,
            PageBreakBlockContent => BlockType.Divider,
            _ => BlockType.Paragraph
        };
    }

    private static DocumentBlockType FromNotionBlockType(BlockType type)
    {
        return type switch
        {
            BlockType.Heading1 or BlockType.Heading2 or BlockType.Heading3 => DocumentBlockType.Heading,
            BlockType.BulletList or BlockType.NumberedList => DocumentBlockType.List,
            BlockType.Quote => DocumentBlockType.Quote,
            BlockType.Table or BlockType.TableRow => DocumentBlockType.Table,
            BlockType.Image => DocumentBlockType.Image,
            BlockType.Divider => DocumentBlockType.PageBreak,
            _ => DocumentBlockType.Paragraph
        };
    }

    private static Tempo.Blazor.NotionEditor.Models.IBlockContent ToNotionContent(DocumentBlock block)
    {
        return block.Content switch
        {
            HeadingBlockContent heading => new NotionHeadingContent
            {
                Level = Math.Clamp(heading.Level, 1, 3),
                Html = ToHtml(heading.Inlines)
            },
            ListBlockContent list => new NotionListContent
            {
                IndentLevel = list.IndentLevel,
                Html = ToHtml(list.Inlines)
            },
            QuoteBlockContent quote => new NotionTextContent
            {
                Html = ToHtml(quote.Inlines)
            },
            TableBlockContent table => new NotionTableContent
            {
                ColumnCount = table.Rows.FirstOrDefault()?.Cells.Count ?? 0
            },
            ImageBlockContent image => new NotionImageContent
            {
                Url = image.Url ?? image.AssetId ?? string.Empty,
                FileId = image.AssetId,
                AltText = image.AltText,
                Caption = image.Caption,
                Width = image.Size.Width is null ? null : Convert.ToInt32(image.Size.Width.Value)
            },
            PageBreakBlockContent => new Tempo.Blazor.NotionEditor.Models.DividerBlockContent(),
            _ => new NotionTextContent
            {
                Html = block.Content is ParagraphBlockContent paragraph ? ToHtml(paragraph.Inlines) : string.Empty
            }
        };
    }

    private static DocumentBlockContent FromNotionContent(NotionBlock block)
    {
        return block.Content switch
        {
            NotionHeadingContent heading => new HeadingBlockContent
            {
                Level = heading.Level,
                Inlines = [new TextRun { Text = ToPlainText(heading.Html) }]
            },
            NotionListContent list => new ListBlockContent
            {
                Ordered = block.Type == BlockType.NumberedList,
                IndentLevel = list.IndentLevel,
                Inlines = [new TextRun { Text = ToPlainText(list.Html) }]
            },
            NotionImageContent image => new ImageBlockContent
            {
                Source = string.IsNullOrWhiteSpace(image.FileId) ? DocumentImageSource.Url : DocumentImageSource.Asset,
                Url = string.IsNullOrWhiteSpace(image.FileId) ? image.Url : null,
                AssetId = image.FileId,
                AltText = image.AltText,
                Caption = image.Caption,
                Size = new DocumentImageSize { Width = image.Width }
            },
            NotionTableContent => new TableBlockContent(),
            NotionTableRowContent row => new TableBlockContent
            {
                Rows =
                [
                    new TableRowContent
                    {
                        Cells = row.Cells.Select(cell => new TableCellContent
                        {
                            Blocks =
                            [
                                new DocumentBlock
                                {
                                    Type = DocumentBlockType.Paragraph,
                                    Content = new ParagraphBlockContent
                                    {
                                        Inlines = [new TextRun { Text = cell }]
                                    }
                                }
                            ]
                        }).ToList()
                    }
                ]
            },
            Tempo.Blazor.NotionEditor.Models.DividerBlockContent => new PageBreakBlockContent(),
            NotionTextContent text when block.Type == BlockType.Quote => new QuoteBlockContent
            {
                Inlines = [new TextRun { Text = ToPlainText(text.Html) }]
            },
            NotionTextContent text => new ParagraphBlockContent
            {
                Inlines = [new TextRun { Text = ToPlainText(text.Html) }]
            },
            _ => new ParagraphBlockContent()
        };
    }

    private static IEnumerable<string> ExtractCommentIds(NotionBlock block)
    {
        var html = block.Content switch
        {
            NotionTextContent text => text.Html,
            NotionHeadingContent heading => heading.Html,
            NotionListContent list => list.Html,
            _ => string.Empty
        };

        return CommentAnchorRegex.Matches(html)
            .Select(match => match.Groups["id"].Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal);
    }

    private static string ToHtml(IReadOnlyList<InlineContent> inlines)
    {
        return string.Concat(inlines.Select(ToHtml));
    }

    private static string ToHtml(InlineContent inline)
    {
        var text = inline switch
        {
            TextRun textRun => textRun.Text,
            TokenRun tokenRun => tokenRun.DisplayName,
            DocumentNoteReferenceRun note => note.DisplayMarker ?? note.NoteId,
            _ => string.Empty
        };

        var html = WebUtility.HtmlEncode(text);
        foreach (var mark in inline.Marks)
        {
            html = mark.Type switch
            {
                InlineMarkType.Bold => $"<strong>{html}</strong>",
                InlineMarkType.Italic => $"<em>{html}</em>",
                InlineMarkType.Underline => $"<u>{html}</u>",
                InlineMarkType.Link when mark.Link is not null => $"<a href=\"{WebUtility.HtmlEncode(mark.Link.Href)}\">{html}</a>",
                InlineMarkType.CommentAnchor when mark.CommentAnchor is not null => $"<span data-comment-id=\"{WebUtility.HtmlEncode(mark.CommentAnchor.CommentId)}\">{html}</span>",
                _ => html
            };
        }

        return html;
    }

    private static string ToPlainText(string html)
    {
        var withoutTags = Regex.Replace(html, "<.*?>", string.Empty, RegexOptions.CultureInvariant);
        return WebUtility.HtmlDecode(withoutTags);
    }

    private static Guid TryParseGuid(string value)
    {
        return Guid.TryParse(value, out var guid) ? guid : Guid.NewGuid();
    }
}
