using System.Net;
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

/// <summary>Imports common safe Markdown document structures into a <see cref="DocumentEditorDocument"/>.</summary>
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

        if (string.IsNullOrWhiteSpace(markdown))
        {
            return document;
        }

        var lines = NormalizeNewLines(markdown).Split('\n');
        var order = 0d;
        for (var index = 0; index < lines.Length;)
        {
            var line = lines[index].TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line))
            {
                index++;
                continue;
            }

            if (TryReadTable(lines, index, out var table, out var nextIndex))
            {
                table.Order = order++;
                document.Blocks.Add(table);
                index = nextIndex;
                continue;
            }

            if (TryReadSingleLineBlock(line, order, out var block))
            {
                document.Blocks.Add(block);
                order++;
                index++;
                continue;
            }

            var paragraphLines = new List<string>();
            while (index < lines.Length
                   && !string.IsNullOrWhiteSpace(lines[index])
                   && !TryReadSingleLineBlock(lines[index], order, out _)
                   && !LooksLikeTable(lines, index))
            {
                paragraphLines.Add(lines[index].Trim());
                index++;
            }

            if (paragraphLines.Count > 0)
            {
                document.Blocks.Add(new DocumentBlock
                {
                    Type = DocumentBlockType.Paragraph,
                    Order = order++,
                    Content = new ParagraphBlockContent
                    {
                        Inlines = ParseInlines(string.Join(' ', paragraphLines))
                    }
                });
            }
        }

        return document;
    }

    private static bool TryReadSingleLineBlock(string line, double order, out DocumentBlock block)
    {
        block = new DocumentBlock();
        var trimmed = line.Trim();

        var heading = HeadingRegex().Match(trimmed);
        if (heading.Success)
        {
            block = new DocumentBlock
            {
                Type = DocumentBlockType.Heading,
                Order = order,
                Content = new HeadingBlockContent
                {
                    Level = heading.Groups["level"].Value.Length,
                    Inlines = ParseInlines(heading.Groups["text"].Value.Trim())
                }
            };
            return true;
        }

        var image = ImageOnlyRegex().Match(trimmed);
        if (image.Success)
        {
            var alt = DecodeMarkdownText(image.Groups["alt"].Value);
            block = new DocumentBlock
            {
                Type = DocumentBlockType.Image,
                Order = order,
                Content = new ImageBlockContent
                {
                    Source = DocumentImageSource.Url,
                    Url = DecodeMarkdownText(image.Groups["url"].Value.Trim()),
                    AltText = alt,
                    Caption = alt
                }
            };
            return true;
        }

        if (trimmed is "---" or "***" or "___")
        {
            block = new DocumentBlock
            {
                Type = DocumentBlockType.PageBreak,
                Order = order,
                Content = new PageBreakBlockContent()
            };
            return true;
        }

        var quote = QuoteRegex().Match(trimmed);
        if (quote.Success)
        {
            block = new DocumentBlock
            {
                Type = DocumentBlockType.Quote,
                Order = order,
                Content = new QuoteBlockContent
                {
                    Inlines = ParseInlines(quote.Groups["text"].Value.Trim())
                }
            };
            return true;
        }

        var list = ListRegex().Match(line);
        if (list.Success)
        {
            var marker = list.Groups["marker"].Value;
            var ordered = char.IsDigit(marker[0]);
            var startNumber = ordered && int.TryParse(NumberPrefixRegex().Match(marker).Value, out var parsed)
                ? parsed
                : 1;

            block = new DocumentBlock
            {
                Type = DocumentBlockType.List,
                Order = order,
                Content = new ListBlockContent
                {
                    Ordered = ordered,
                    StartNumber = startNumber,
                    IndentLevel = list.Groups["indent"].Value.Replace("\t", "    ", StringComparison.Ordinal).Length / 2,
                    Inlines = ParseInlines(list.Groups["text"].Value.Trim())
                }
            };
            return true;
        }

        return false;
    }

    private static bool TryReadTable(string[] lines, int index, out DocumentBlock block, out int nextIndex)
    {
        block = new DocumentBlock();
        nextIndex = index;
        if (!LooksLikeTable(lines, index))
        {
            return false;
        }

        var rows = new List<TableRowContent>
        {
            new()
            {
                Cells = SplitTableCells(lines[index])
                    .Select(cell => CreateTableCell(cell, isHeader: true))
                    .ToList()
            }
        };

        nextIndex = index + 2;
        while (nextIndex < lines.Length && IsTableRow(lines[nextIndex]))
        {
            rows.Add(new TableRowContent
            {
                Cells = SplitTableCells(lines[nextIndex])
                    .Select(cell => CreateTableCell(cell, isHeader: false))
                    .ToList()
            });
            nextIndex++;
        }

        block = new DocumentBlock
        {
            Type = DocumentBlockType.Table,
            Content = new TableBlockContent { Rows = rows }
        };
        return true;
    }

    private static bool LooksLikeTable(string[] lines, int index)
        => index + 1 < lines.Length
            && IsTableRow(lines[index])
            && TableSeparatorRegex().IsMatch(lines[index + 1].Trim());

    private static bool IsTableRow(string line)
        => line.Trim().StartsWith('|') && line.Trim().EndsWith('|');

    private static TableCellContent CreateTableCell(string markdown, bool isHeader) => new()
    {
        IsHeader = isHeader,
        Blocks =
        [
            new DocumentBlock
            {
                Type = DocumentBlockType.Paragraph,
                Order = 0,
                Content = new ParagraphBlockContent { Inlines = ParseInlines(markdown.Trim()) }
            }
        ]
    };

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

    private static List<InlineContent> ParseInlines(string markdown)
    {
        var result = new List<InlineContent>();
        var index = 0;
        while (index < markdown.Length)
        {
            var nextImage = markdown.IndexOf("![", index, StringComparison.Ordinal);
            var nextLink = markdown.IndexOf('[', index);
            var nextBold = markdown.IndexOf("**", index, StringComparison.Ordinal);
            var nextBoldUnderscore = markdown.IndexOf("__", index, StringComparison.Ordinal);
            var nextItalic = markdown.IndexOf('*', index);
            var nextItalicUnderscore = markdown.IndexOf('_', index);
            var nextStrike = markdown.IndexOf("~~", index, StringComparison.Ordinal);
            var nextCode = markdown.IndexOf('`', index);
            var next = MinPositive(nextImage, nextLink, nextBold, nextBoldUnderscore, nextItalic, nextItalicUnderscore, nextStrike, nextCode);
            if (next < 0)
            {
                AddText(result, DecodeMarkdownText(markdown[index..]), []);
                break;
            }

            if (next > index)
            {
                AddText(result, DecodeMarkdownText(markdown[index..next]), []);
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
                || TryReadDelimited(markdown, index, "__", InlineMarkType.Bold, out marked, out consumed)
                || TryReadDelimited(markdown, index, "~~", InlineMarkType.Strikethrough, out marked, out consumed)
                || TryReadDelimited(markdown, index, "`", InlineMarkType.FontFamily, out marked, out consumed)
                || TryReadDelimited(markdown, index, "*", InlineMarkType.Italic, out marked, out consumed)
                || TryReadDelimited(markdown, index, "_", InlineMarkType.Italic, out marked, out consumed))
            {
                result.Add(marked);
                index += consumed;
                continue;
            }

            AddText(result, DecodeMarkdownText(markdown[index].ToString()), []);
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

        var alt = DecodeMarkdownText(markdown[(index + 2)..closeAlt]);
        var url = DecodeMarkdownText(markdown[(closeAlt + 2)..closeUrl]);
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

        var href = DecodeMarkdownText(markdown[(closeText + 2)..closeUrl]);
        run = new TextRun
        {
            Text = DecodeMarkdownText(markdown[(index + 1)..closeText]),
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
            Text = DecodeMarkdownText(markdown[(index + delimiter.Length)..close]),
            Marks = [new InlineMark { Type = markType }]
        };
        consumed = close - index + delimiter.Length;
        return true;
    }

    private static void AddText(List<InlineContent> inlines, string text, List<InlineMark> marks)
    {
        if (!string.IsNullOrEmpty(text))
        {
            inlines.Add(new TextRun { Text = text, Marks = marks });
        }
    }

    private static int MinPositive(params int[] values)
        => values.Where(value => value >= 0).DefaultIfEmpty(-1).Min();

    private static string NormalizeNewLines(string value)
        => (value ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    private static string DecodeMarkdownText(string text)
        => WebUtility.HtmlDecode(UnescapeMarkdown(text));

    private static string UnescapeMarkdown(string text)
        => Regex.Replace(text, """\\([\\`*_{}\[\]()#+\-.!|~<>])""", "$1");

    private static bool IsSafeMarkdownUrl(string value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && uri.Scheme is "http" or "https" or "mailto";

    [GeneratedRegex("""^(?<level>#{1,6})\s+(?<text>.+)$""")]
    private static partial Regex HeadingRegex();

    [GeneratedRegex("""^!\[(?<alt>[^\]]*)\]\((?<url>[^)]*)\)$""")]
    private static partial Regex ImageOnlyRegex();

    [GeneratedRegex("""^>\s?(?<text>.+)$""")]
    private static partial Regex QuoteRegex();

    [GeneratedRegex("""^(?<indent>\s*)(?<marker>(?:[-+*])|(?:\d+[.)]))\s+(?<text>.+)$""")]
    private static partial Regex ListRegex();

    [GeneratedRegex("""\d+""")]
    private static partial Regex NumberPrefixRegex();

    [GeneratedRegex("""^\|?\s*:?-{3,}:?\s*(\|\s*:?-{3,}:?\s*)+\|?$""")]
    private static partial Regex TableSeparatorRegex();
}
