using System.Text;
using System.Text.RegularExpressions;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.DocumentFormats.Markdown;

/// <summary>Options used when importing Markdown into the editor document model.</summary>
public sealed class DocumentMarkdownImportOptions
{
    /// <summary>Optional document id assigned to the imported model.</summary>
    public string? DocumentId { get; set; }

    /// <summary>Optional metadata title for the imported document.</summary>
    public string? Title { get; set; }
}

/// <summary>Imports a safe semantic Markdown subset into a <see cref="DocumentEditorDocument"/>.</summary>
public sealed partial class DocumentMarkdownImporter : IDocumentFormatImporter
{
    /// <inheritdoc />
    public async Task<DocumentFormatImportResult> ImportAsync(
        Stream stream,
        DocumentFormatImportOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new DocumentFormatImportOptions();
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var markdown = await reader.ReadToEndAsync(cancellationToken);
        return new DocumentFormatImportResult
        {
            Document = Import(markdown, new DocumentMarkdownImportOptions
            {
                DocumentId = options.DocumentId,
                Title = string.IsNullOrWhiteSpace(options.FileName)
                    ? null
                    : Path.GetFileNameWithoutExtension(options.FileName)
            }),
            Format = DocumentFormatKind.Markdown
        };
    }

    /// <summary>Imports Markdown text into a document model.</summary>
    public DocumentEditorDocument Import(string markdown, DocumentMarkdownImportOptions? options = null)
    {
        options ??= new DocumentMarkdownImportOptions();
        var document = DocumentEditorDocument.Empty(options.DocumentId);
        document.Metadata.Title = options.Title ?? string.Empty;

        var lines = NormalizeNewLines(markdown).Split('\n');
        var order = 0d;
        var paragraph = new List<string>();

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index].TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line))
            {
                FlushParagraph(document, paragraph, ref order);
                continue;
            }

            if (TryReadTable(lines, ref index, document, ref order))
            {
                FlushParagraph(document, paragraph, ref order);
                continue;
            }

            var headingMatch = HeadingRegex().Match(line);
            if (headingMatch.Success)
            {
                FlushParagraph(document, paragraph, ref order);
                document.Blocks.Add(new DocumentBlock
                {
                    Type = DocumentBlockType.Heading,
                    Order = order++,
                    Content = new HeadingBlockContent
                    {
                        Level = headingMatch.Groups["level"].Value.Length,
                        Inlines = ParseInlines(headingMatch.Groups["text"].Value)
                    }
                });
                continue;
            }

            var imageMatch = ImageOnlyRegex().Match(line.Trim());
            if (imageMatch.Success)
            {
                FlushParagraph(document, paragraph, ref order);
                document.Blocks.Add(new DocumentBlock
                {
                    Type = DocumentBlockType.Image,
                    Order = order++,
                    Content = new ImageBlockContent
                    {
                        Source = DocumentImageSource.Url,
                        Url = UnescapeMarkdown(imageMatch.Groups["url"].Value.Trim()),
                        AltText = UnescapeMarkdown(imageMatch.Groups["alt"].Value),
                        Caption = UnescapeMarkdown(imageMatch.Groups["alt"].Value)
                    }
                });
                continue;
            }

            var listMatch = ListRegex().Match(line);
            if (listMatch.Success)
            {
                FlushParagraph(document, paragraph, ref order);
                var ordered = !string.IsNullOrWhiteSpace(listMatch.Groups["number"].Value);
                var startNumber = ordered && int.TryParse(listMatch.Groups["number"].Value, out var parsed)
                    ? parsed
                    : 1;
                document.Blocks.Add(new DocumentBlock
                {
                    Type = DocumentBlockType.List,
                    Order = order++,
                    Content = new ListBlockContent
                    {
                        Ordered = ordered,
                        StartNumber = startNumber,
                        Inlines = ParseInlines(listMatch.Groups["text"].Value)
                    }
                });
                continue;
            }

            if (line.StartsWith("> ", StringComparison.Ordinal))
            {
                FlushParagraph(document, paragraph, ref order);
                document.Blocks.Add(new DocumentBlock
                {
                    Type = DocumentBlockType.Quote,
                    Order = order++,
                    Content = new QuoteBlockContent { Inlines = ParseInlines(line[2..]) }
                });
                continue;
            }

            if (line.Trim() is "---" or "***" or "___")
            {
                FlushParagraph(document, paragraph, ref order);
                document.Blocks.Add(new DocumentBlock
                {
                    Type = DocumentBlockType.PageBreak,
                    Order = order++,
                    Content = new PageBreakBlockContent()
                });
                continue;
            }

            paragraph.Add(line.Trim());
        }

        FlushParagraph(document, paragraph, ref order);
        return document;
    }

    private static bool TryReadTable(string[] lines, ref int index, DocumentEditorDocument document, ref double order)
    {
        if (index + 1 >= lines.Length || !IsTableRow(lines[index]) || !TableSeparatorRegex().IsMatch(lines[index + 1].Trim()))
        {
            return false;
        }

        var rows = new List<TableRowContent>();
        rows.Add(ReadTableRow(lines[index]));
        index += 2;
        while (index < lines.Length && IsTableRow(lines[index]))
        {
            rows.Add(ReadTableRow(lines[index]));
            index++;
        }

        index--;
        document.Blocks.Add(new DocumentBlock
        {
            Type = DocumentBlockType.Table,
            Order = order++,
            Content = new TableBlockContent { Rows = rows }
        });
        return true;
    }

    private static bool IsTableRow(string line)
        => line.Trim().StartsWith('|') && line.Trim().EndsWith('|');

    private static TableRowContent ReadTableRow(string line)
    {
        var cells = SplitTableCells(line)
            .Select(cell => new TableCellContent
            {
                Blocks =
                [
                    new DocumentBlock
                    {
                        Type = DocumentBlockType.Paragraph,
                        Content = new ParagraphBlockContent { Inlines = ParseInlines(cell.Trim()) }
                    }
                ]
            })
            .ToList();

        return new TableRowContent { Cells = cells };
    }

    private static IReadOnlyList<string> SplitTableCells(string line)
    {
        var trimmed = line.Trim().Trim('|');
        var cells = new List<string>();
        var current = new StringBuilder();
        var escaped = false;
        foreach (var ch in trimmed)
        {
            if (escaped)
            {
                current.Append(ch);
                escaped = false;
                continue;
            }

            if (ch == '\\')
            {
                escaped = true;
                continue;
            }

            if (ch == '|')
            {
                cells.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        cells.Add(current.ToString());
        return cells;
    }

    private static void FlushParagraph(DocumentEditorDocument document, List<string> lines, ref double order)
    {
        if (lines.Count == 0)
        {
            return;
        }

        document.Blocks.Add(new DocumentBlock
        {
            Type = DocumentBlockType.Paragraph,
            Order = order++,
            Content = new ParagraphBlockContent { Inlines = ParseInlines(string.Join(' ', lines)) }
        });
        lines.Clear();
    }

    private static List<InlineContent> ParseInlines(string markdown)
    {
        var result = new List<InlineContent>();
        var index = 0;
        while (index < markdown.Length)
        {
            var nextImage = markdown.IndexOf("![", index, StringComparison.Ordinal);
            var nextLink = markdown.IndexOf('[', index);
            var nextBold = markdown.IndexOf("**", index, StringComparison.Ordinal);
            var nextItalic = markdown.IndexOf('*', index);
            var nextStrike = markdown.IndexOf("~~", index, StringComparison.Ordinal);
            var next = MinPositive(nextImage, nextLink, nextBold, nextItalic, nextStrike);
            if (next < 0)
            {
                AddText(result, UnescapeMarkdown(markdown[index..]), []);
                break;
            }

            if (next > index)
            {
                AddText(result, UnescapeMarkdown(markdown[index..next]), []);
                index = next;
                continue;
            }

            if (TryReadImageRun(markdown, index, out var drawing, out var consumed))
            {
                result.Add(drawing);
                index += consumed;
                continue;
            }

            if (TryReadLink(markdown, index, out var linkRun, out consumed))
            {
                result.Add(linkRun);
                index += consumed;
                continue;
            }

            if (TryReadDelimited(markdown, index, "**", InlineMarkType.Bold, out var marked, out consumed)
                || TryReadDelimited(markdown, index, "~~", InlineMarkType.Strikethrough, out marked, out consumed)
                || TryReadDelimited(markdown, index, "*", InlineMarkType.Italic, out marked, out consumed))
            {
                result.Add(marked);
                index += consumed;
                continue;
            }

            AddText(result, UnescapeMarkdown(markdown[index].ToString()), []);
            index++;
        }

        return result.Count == 0 ? [new TextRun()] : result;
    }

    private static bool TryReadImageRun(string markdown, int index, out DocumentDrawingRun drawing, out int consumed)
    {
        drawing = new DocumentDrawingRun();
        consumed = 0;
        if (!markdown[index..].StartsWith("![", StringComparison.Ordinal))
        {
            return false;
        }

        var closeAlt = markdown.IndexOf(']', index + 2);
        if (closeAlt < 0 || closeAlt + 1 >= markdown.Length || markdown[closeAlt + 1] != '(')
        {
            return false;
        }

        var closeUrl = markdown.IndexOf(')', closeAlt + 2);
        if (closeUrl < 0)
        {
            return false;
        }

        var alt = UnescapeMarkdown(markdown[(index + 2)..closeAlt]);
        var url = UnescapeMarkdown(markdown[(closeAlt + 2)..closeUrl]);
        drawing = new DocumentDrawingRun
        {
            Kind = DocumentDrawingKind.Image,
            Source = DocumentImageSource.Url,
            Url = url,
            AltText = alt,
            Caption = alt
        };
        consumed = closeUrl - index + 1;
        return true;
    }

    private static bool TryReadLink(string markdown, int index, out TextRun run, out int consumed)
    {
        run = new TextRun();
        consumed = 0;
        if (markdown[index] != '[' || (index > 0 && markdown[index - 1] == '!'))
        {
            return false;
        }

        var closeText = markdown.IndexOf(']', index + 1);
        if (closeText < 0 || closeText + 1 >= markdown.Length || markdown[closeText + 1] != '(')
        {
            return false;
        }

        var closeUrl = markdown.IndexOf(')', closeText + 2);
        if (closeUrl < 0)
        {
            return false;
        }

        var href = UnescapeMarkdown(markdown[(closeText + 2)..closeUrl]);
        run = new TextRun
        {
            Text = UnescapeMarkdown(markdown[(index + 1)..closeText]),
            Marks = IsSafeMarkdownUrl(href)
                ? [new InlineMark { Type = InlineMarkType.Link, Link = new LinkMarkData { Href = href } }]
                : []
        };
        consumed = closeUrl - index + 1;
        return true;
    }

    private static bool TryReadDelimited(
        string markdown,
        int index,
        string delimiter,
        InlineMarkType markType,
        out TextRun run,
        out int consumed)
    {
        run = new TextRun();
        consumed = 0;
        if (!markdown[index..].StartsWith(delimiter, StringComparison.Ordinal))
        {
            return false;
        }

        var close = markdown.IndexOf(delimiter, index + delimiter.Length, StringComparison.Ordinal);
        if (close < 0)
        {
            return false;
        }

        run = new TextRun
        {
            Text = UnescapeMarkdown(markdown[(index + delimiter.Length)..close]),
            Marks = [new InlineMark { Type = markType }]
        };
        consumed = close - index + delimiter.Length;
        return true;
    }

    private static void AddText(List<InlineContent> inlines, string text, List<InlineMark> marks)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        inlines.Add(new TextRun { Text = text, Marks = marks });
    }

    private static int MinPositive(params int[] values)
        => values.Where(value => value >= 0).DefaultIfEmpty(-1).Min();

    private static string NormalizeNewLines(string value)
        => (value ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    private static string UnescapeMarkdown(string text)
        => Regex.Replace(text, """\\([\\`*_{}\[\]()#+\-.!|~<>])""", "$1");

    private static bool IsSafeMarkdownUrl(string value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && uri.Scheme is "http" or "https" or "mailto";

    [GeneratedRegex("""^(?<level>#{1,6})\s+(?<text>.+)$""")]
    private static partial Regex HeadingRegex();

    [GeneratedRegex("""^(?:(?<number>\d+)\.\s+|[-*+]\s+)(?<text>.+)$""")]
    private static partial Regex ListRegex();

    [GeneratedRegex("""^!\[(?<alt>[^\]]*)\]\((?<url>[^)]*)\)$""")]
    private static partial Regex ImageOnlyRegex();

    [GeneratedRegex("""^\|?\s*:?-{3,}:?\s*(\|\s*:?-{3,}:?\s*)+\|?$""")]
    private static partial Regex TableSeparatorRegex();
}
