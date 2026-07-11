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

            if (TryReadFencedCode(lines, index, out var code, out var afterFence))
            {
                code.Order = order++;
                document.Blocks.Add(code);
                index = afterFence;
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

    /// <summary>
    /// Reads a fenced code block. The body is taken verbatim — no inline markup, no escaping —
    /// and an unterminated fence runs to the end of the document rather than losing its lines.
    /// </summary>
    private static bool TryReadFencedCode(string[] lines, int index, out DocumentBlock block, out int nextIndex)
    {
        block = new DocumentBlock();
        nextIndex = index;

        var fence = FenceRegex().Match(lines[index].TrimEnd('\r'));
        if (!fence.Success)
        {
            return false;
        }

        var marker = fence.Groups["fence"].Value;
        var language = fence.Groups["language"].Value.Trim();

        var body = new List<string>();
        var cursor = index + 1;
        while (cursor < lines.Length)
        {
            var line = lines[cursor].TrimEnd('\r');
            var closing = FenceRegex().Match(line);
            if (closing.Success
                && closing.Groups["fence"].Value.Length >= marker.Length
                && closing.Groups["language"].Value.Trim().Length == 0)
            {
                cursor++;
                break;
            }

            body.Add(line);
            cursor++;
        }

        block = new DocumentBlock
        {
            Type = DocumentBlockType.Code,
            Content = new CodeBlockContent
            {
                Language = language.Length == 0 ? null : language,
                Code = string.Join('\n', body)
            }
        };
        nextIndex = cursor;
        return true;
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

            // A GFM task list item carries its checkbox as state, not as the literal text "[x] ".
            var text = list.Groups["text"].Value.Trim();
            bool? isChecked = null;
            var task = TaskPrefixRegex().Match(text);
            if (!ordered && task.Success)
            {
                isChecked = task.Groups["state"].Value is "x" or "X";
                text = task.Groups["text"].Value.Trim();
            }

            block = new DocumentBlock
            {
                Type = DocumentBlockType.List,
                Order = order,
                Content = new ListBlockContent
                {
                    Ordered = ordered,
                    StartNumber = startNumber,
                    IsChecked = isChecked,
                    IndentLevel = list.Groups["indent"].Value.Replace("\t", "    ", StringComparison.Ordinal).Length / 2,
                    Inlines = ParseInlines(text)
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

        var headerCells = SplitTableCells(lines[index]);
        var columnCount = headerCells.Count;
        var alignments = ParseColumnAlignments(lines[index + 1], columnCount);

        var rows = new List<TableRowContent>
        {
            new()
            {
                Cells = headerCells
                    .Select(cell => CreateTableCell(cell, isHeader: true))
                    .ToList()
            }
        };

        nextIndex = index + 2;
        while (nextIndex < lines.Length && IsTableRow(lines[nextIndex]))
        {
            rows.Add(new TableRowContent
            {
                Cells = NormalizeCellCount(SplitTableCells(lines[nextIndex]), columnCount, string.Empty)
                    .Select(cell => CreateTableCell(cell, isHeader: false))
                    .ToList()
            });
            nextIndex++;
        }

        block = new DocumentBlock
        {
            Type = DocumentBlockType.Table,
            Content = new TableBlockContent { Rows = rows, ColumnAlignments = alignments }
        };
        return true;
    }

    /// <summary>A table starts wherever a cell-bearing line is followed by a GFM delimiter row.</summary>
    private static bool LooksLikeTable(string[] lines, int index)
        => index + 1 < lines.Length
            && IsTableRow(lines[index])
            && IsTableSeparator(lines[index + 1]);

    /// <summary>
    /// A delimiter row is a pipe-delimited line whose every cell is a run of dashes with optional
    /// colon anchors. Requiring a pipe is what separates a single-column delimiter (<c>| --- |</c>)
    /// from a thematic break (<c>---</c>).
    /// </summary>
    private static bool IsTableSeparator(string line)
    {
        var trimmed = line.Trim();
        if (!ContainsUnescapedPipe(trimmed))
        {
            return false;
        }

        var cells = SplitTableCells(trimmed);
        return cells.Count > 0 && cells.All(cell => SeparatorCellRegex().IsMatch(cell.Trim()));
    }

    /// <summary>Outer pipes are optional in GFM; a row only needs one unescaped cell delimiter.</summary>
    private static bool IsTableRow(string line) => ContainsUnescapedPipe(line);

    private static bool ContainsUnescapedPipe(string line)
    {
        var escaped = false;
        foreach (var ch in line)
        {
            if (escaped)
            {
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
                return true;
            }
        }

        return false;
    }

    private static List<TableColumnAlignment> ParseColumnAlignments(string separatorLine, int columnCount)
    {
        var alignments = SplitTableCells(separatorLine)
            .Select(ParseColumnAlignment)
            .ToList();

        return NormalizeCellCount(alignments, columnCount, TableColumnAlignment.None);
    }

    private static TableColumnAlignment ParseColumnAlignment(string separatorCell)
    {
        var cell = separatorCell.Trim();
        var left = cell.StartsWith(':');
        var right = cell.EndsWith(':') && cell.Length > 1;

        return (left, right) switch
        {
            (true, true) => TableColumnAlignment.Center,
            (true, false) => TableColumnAlignment.Left,
            (false, true) => TableColumnAlignment.Right,
            _ => TableColumnAlignment.None
        };
    }

    /// <summary>Pads short rows and truncates overlong ones so every row matches the header width.</summary>
    private static List<T> NormalizeCellCount<T>(IReadOnlyList<T> cells, int columnCount, T padding)
    {
        if (cells.Count == columnCount)
        {
            return [.. cells];
        }

        if (cells.Count > columnCount)
        {
            return [.. cells.Take(columnCount)];
        }

        var normalized = new List<T>(cells);
        normalized.AddRange(Enumerable.Repeat(padding, columnCount - cells.Count));
        return normalized;
    }

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
        var trimmed = line.Trim();
        var cells = new List<string>();
        var current = new StringBuilder();
        var escaped = false;
        var endedOnDelimiter = false;
        foreach (var ch in trimmed)
        {
            if (escaped)
            {
                current.Append(ch);
                escaped = false;
                endedOnDelimiter = false;
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
                endedOnDelimiter = true;
                continue;
            }

            current.Append(ch);
            endedOnDelimiter = false;
        }

        cells.Add(current.ToString());

        // Leading and trailing pipes are optional delimiters in GFM, not empty cells.
        if (endedOnDelimiter && cells.Count > 1)
        {
            cells.RemoveAt(cells.Count - 1);
        }

        if (cells.Count > 1 && trimmed.StartsWith('|'))
        {
            cells.RemoveAt(0);
        }

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
                || TryReadUnderscoreDelimited(markdown, index, "__", InlineMarkType.Bold, out marked, out consumed)
                || TryReadDelimited(markdown, index, "~~", InlineMarkType.Strikethrough, out marked, out consumed)
                || TryReadDelimited(markdown, index, "`", InlineMarkType.FontFamily, out marked, out consumed)
                || TryReadDelimited(markdown, index, "*", InlineMarkType.Italic, out marked, out consumed)
                || TryReadUnderscoreDelimited(markdown, index, "_", InlineMarkType.Italic, out marked, out consumed))
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

    /// <summary>
    /// Underscore emphasis only fires at word boundaries. Without this, <c>snake_case_name</c>
    /// and Python's <c>__init__</c> would come out italicised, which is why GFM restricts
    /// <c>_</c> — but not <c>*</c> — to run starts and ends.
    /// </summary>
    private static bool TryReadUnderscoreDelimited(
        string markdown,
        int index,
        string delimiter,
        InlineMarkType markType,
        out TextRun run,
        out int consumed)
    {
        run = new TextRun();
        consumed = 0;

        if (index > 0 && IsWordCharacter(markdown[index - 1]))
        {
            return false;
        }

        if (!TryReadDelimited(markdown, index, delimiter, markType, out run, out consumed))
        {
            return false;
        }

        var after = index + consumed;
        if (after < markdown.Length && IsWordCharacter(markdown[after]))
        {
            run = new TextRun();
            consumed = 0;
            return false;
        }

        return true;
    }

    private static bool IsWordCharacter(char value) => char.IsLetterOrDigit(value);

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

    [GeneratedRegex("""^(?<fence>`{3,}|~{3,})(?<language>[^`]*)$""")]
    private static partial Regex FenceRegex();

    [GeneratedRegex("""^\[(?<state>[ xX]?)\]\s+(?<text>.*)$""")]
    private static partial Regex TaskPrefixRegex();

    [GeneratedRegex("""\d+""")]
    private static partial Regex NumberPrefixRegex();

    [GeneratedRegex("""^:?-+:?$""")]
    private static partial Regex SeparatorCellRegex();
}
