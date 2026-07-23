using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Dm = Tempo.Blazor.DocumentEditor.Models;
using Nm = Tempo.Blazor.NotionEditor.Models;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;

namespace Tempo.Blazor.DocumentFormats.Notion;

/// <summary>Converts Notion editor page blocks into the document editor model consumed by Tempo.Blazor.DocumentFormats exporters.</summary>
public static partial class NotionToDocumentModelConverter
{
    private const string ApproximateWarningCode = "notion.block.approximate";

    /// <summary>Converts a Notion page and its blocks into a document model.</summary>
    public static NotionToDocumentModelConversionResult ConvertPage(INotionPage page, IReadOnlyList<IPageBlock> blocks)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(blocks);

        var warnings = new List<DocumentFormatCompatibilityWarning>();
        var document = Dm.DocumentEditorDocument.Empty(page.Id.ToString("N"));
        document.Metadata.Title = string.IsNullOrWhiteSpace(page.Title) ? page.Id.ToString("D") : page.Title;
        document.Metadata.Description = page.Description;
        document.Metadata.CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(page.CreatedAt, DateTimeKind.Utc));
        document.Metadata.ModifiedAt = new DateTimeOffset(DateTime.SpecifyKind(page.LastEditedAt, DateTimeKind.Utc));
        document.Metadata.Author = new Dm.DocumentEditorAuthor
        {
            Id = page.CreatedByUserId ?? string.Empty,
            DisplayName = page.CreatedByUserId ?? string.Empty
        };
        document.Metadata.Tags = page.Labels.ToList();

        document.Blocks = ConvertBlocks(blocks, warnings);
        if (document.Blocks.Count == 0)
        {
            document.Blocks.Add(CreateParagraph(Guid.NewGuid().ToString("N"), 0, string.Empty));
        }

        return new NotionToDocumentModelConversionResult(document, warnings);
    }

    /// <summary>Converts ordered Notion blocks into ordered document blocks.</summary>
    public static List<Dm.DocumentBlock> ConvertBlocks(
        IReadOnlyList<IPageBlock> blocks,
        IList<DocumentFormatCompatibilityWarning>? warnings = null)
    {
        ArgumentNullException.ThrowIfNull(blocks);

        var result = new List<Dm.DocumentBlock>();
        var ordered = blocks.OrderBy(block => block.Order).ThenBy(block => block.Id).ToList();
        var childRowsByTableId = ordered
            .Where(block => block.ParentBlockId.HasValue && block.Type == BlockType.TableRow)
            .GroupBy(block => block.ParentBlockId!.Value)
            .ToDictionary(group => group.Key, group => group.OrderBy(block => block.Order).ToList());

        for (var index = 0; index < ordered.Count; index++)
        {
            var block = ordered[index];
            if (block.ParentBlockId.HasValue)
            {
                continue;
            }

            if (block.Type == BlockType.Table)
            {
                var rows = childRowsByTableId.TryGetValue(block.Id, out var childRows)
                    ? childRows
                    : [];
                result.Add(CreateTable(block, rows, result.Count, warnings));
                continue;
            }

            if (block.Type == BlockType.TableRow)
            {
                result.Add(ConvertBlock(block, result.Count, warnings));
                continue;
            }

            result.Add(ConvertBlock(block, result.Count, warnings));
        }

        return result;
    }

    private static Dm.DocumentBlock ConvertBlock(
        IPageBlock block,
        int order,
        IList<DocumentFormatCompatibilityWarning>? warnings)
    {
        return block.Type switch
        {
            BlockType.Paragraph => CreateParagraph(block.Id.ToString("N"), order, TextInlines(block.Content as Nm.ITextBlockContent)),
            BlockType.Heading1 => CreateHeading(block.Id.ToString("N"), order, 1, TextInlines(block.Content as Nm.ITextBlockContent)),
            BlockType.Heading2 => CreateHeading(block.Id.ToString("N"), order, 2, TextInlines(block.Content as Nm.ITextBlockContent)),
            BlockType.Heading3 => CreateHeading(block.Id.ToString("N"), order, 3, TextInlines(block.Content as Nm.ITextBlockContent)),
            BlockType.BulletList => CreateList(block.Id.ToString("N"), order, false, block.Content as Nm.IListBlockContent),
            BlockType.NumberedList => CreateList(block.Id.ToString("N"), order, true, block.Content as Nm.IListBlockContent),
            BlockType.TodoItem => CreateTodo(block.Id.ToString("N"), order, block.Content as Nm.ITodoBlockContent),
            BlockType.Quote => CreateQuote(block.Id.ToString("N"), order, TextInlines(block.Content as Nm.ITextBlockContent)),
            BlockType.Callout => CreateCallout(block, order),
            BlockType.Image => CreateImage(block, order),
            BlockType.Divider => CreatePageBreak(block.Id.ToString("N"), order),
            BlockType.Code => CreateCode(block, order),
            _ => CreateApproximateParagraph(block, order, warnings)
        };
    }

    private static Dm.DocumentBlock CreateParagraph(string id, int order, IEnumerable<Dm.InlineContent> inlines)
        => new()
        {
            Id = id,
            Type = Dm.DocumentBlockType.Paragraph,
            Order = order,
            Content = new Dm.ParagraphBlockContent { Inlines = inlines.ToList() }
        };

    private static Dm.DocumentBlock CreateParagraph(string id, int order, string text)
        => CreateParagraph(id, order, [new Dm.TextRun { Text = text }]);

    private static Dm.DocumentBlock CreateHeading(string id, int order, int level, IEnumerable<Dm.InlineContent> inlines)
        => new()
        {
            Id = id,
            Type = Dm.DocumentBlockType.Heading,
            Order = order,
            Content = new Dm.HeadingBlockContent
            {
                Level = Math.Clamp(level, 1, 6),
                Inlines = inlines.ToList()
            }
        };

    private static Dm.DocumentBlock CreatePageBreak(string id, int order)
        => new()
        {
            Id = id,
            Type = Dm.DocumentBlockType.PageBreak,
            Order = order,
            Content = new Dm.PageBreakBlockContent()
        };

    private static Dm.DocumentBlock CreateCode(IPageBlock block, int order)
        => new()
        {
            Id = block.Id.ToString("N"),
            Type = Dm.DocumentBlockType.Code,
            Order = order,
            Content = new Dm.CodeBlockContent
            {
                Language = (block.Content as Nm.ICodeBlockContent)?.Language,
                Code = (block.Content as Nm.ICodeBlockContent)?.Code ?? string.Empty
            }
        };

    private static Dm.DocumentBlock CreateList(string id, int order, bool ordered, Nm.IListBlockContent? content)
        => new()
        {
            Id = id,
            Type = Dm.DocumentBlockType.List,
            Order = order,
            Content = new Dm.ListBlockContent
            {
                Ordered = ordered,
                IndentLevel = Math.Max(0, content?.IndentLevel ?? 0),
                Inlines = TextInlines(content).ToList()
            }
        };

    /// <summary>
    /// The checkbox is model state on <see cref="Dm.ListBlockContent.IsChecked"/>. It used to be
    /// glued in front of the text as a literal "[x] ", which the Markdown exporter then escaped
    /// into "\[x\]" — the task never survived a round trip.
    /// </summary>
    private static Dm.DocumentBlock CreateTodo(string id, int order, Nm.ITodoBlockContent? content)
    {
        var inlines = TextInlines(content).ToList();
        if (inlines.Count == 0)
        {
            inlines.Add(new Dm.TextRun());
        }

        return new Dm.DocumentBlock
        {
            Id = id,
            Type = Dm.DocumentBlockType.List,
            Order = order,
            Content = new Dm.ListBlockContent
            {
                Ordered = false,
                IsChecked = content?.IsChecked ?? false,
                Inlines = inlines
            }
        };
    }

    private static Dm.DocumentBlock CreateQuote(string id, int order, IEnumerable<Dm.InlineContent> inlines)
        => new()
        {
            Id = id,
            Type = Dm.DocumentBlockType.Quote,
            Order = order,
            Content = new Dm.QuoteBlockContent { Inlines = inlines.ToList() }
        };

    private static Dm.DocumentBlock CreateCallout(IPageBlock block, int order)
    {
        var content = block.Content as Nm.ICalloutBlockContent;
        var inlines = TextInlines(content).ToList();
        if (!string.IsNullOrWhiteSpace(content?.IconEmoji))
        {
            inlines.Insert(0, new Dm.TextRun { Text = content.IconEmoji + " " });
        }

        return CreateQuote(block.Id.ToString("N"), order, inlines);
    }

    private static Dm.DocumentBlock CreateImage(IPageBlock block, int order)
    {
        var content = block.Content as Nm.IImageBlockContent;
        return new Dm.DocumentBlock
        {
            Id = block.Id.ToString("N"),
            Type = Dm.DocumentBlockType.Image,
            Order = order,
            Content = new Dm.ImageBlockContent
            {
                Source = string.IsNullOrWhiteSpace(content?.FileId) ? Dm.DocumentImageSource.Url : Dm.DocumentImageSource.Asset,
                Url = content?.Url,
                AssetId = content?.FileId,
                AltText = content?.AltText,
                Caption = content?.Caption,
                Size = new Dm.DocumentImageSize
                {
                    Width = content?.Width,
                    LockAspectRatio = true
                },
                Alignment = content?.Alignment switch
                {
                    MediaAlignment.Left => Dm.DocumentImageAlignment.Start,
                    MediaAlignment.FullWidth => Dm.DocumentImageAlignment.Center,
                    _ => Dm.DocumentImageAlignment.Center
                }
            }
        };
    }

    private static Dm.DocumentBlock CreateTable(
        IPageBlock tableBlock,
        IReadOnlyList<IPageBlock> rowBlocks,
        int order,
        IList<DocumentFormatCompatibilityWarning>? warnings)
    {
        var tableContent = tableBlock.Content as Nm.ITableBlockContent;
        if (rowBlocks.Count == 0)
        {
            AddApproximateWarning(warnings, tableBlock, "Table has no rows and was exported as text.");
            return CreateParagraph(tableBlock.Id.ToString("N"), order, "Empty table");
        }

        var logicalRows = rowBlocks
            .Select(rowBlock => new Nm.NotionAuthoringTableRow
            {
                Cells = ReadRowCells(rowBlock.Content)
                    .Select(ToCanonicalCell)
                    .ToList()
            })
            .ToList();
        var columnCount = tableContent?.ColumnCount
            ?? logicalRows.Max(row => row.Cells.Sum(cell => cell.ColumnSpan));
        if (!Nm.NotionTableGridProjector.TryProject(
                logicalRows,
                columnCount,
                $"notion:block:{tableBlock.Id:D}.rows",
                out var projection,
                out var projectionIssues))
        {
            foreach (var issue in projectionIssues)
            {
                warnings?.Add(new DocumentFormatCompatibilityWarning
                {
                    Code = issue.Code,
                    Message = issue.Message,
                    Severity = DocumentFormatCompatibilitySeverity.Warning,
                    SourcePath = issue.Path,
                    ObjectId = tableBlock.Id.ToString("N")
                });
            }

            return CreateParagraph(
                tableBlock.Id.ToString("N"),
                order,
                "Invalid table");
        }

        var rows = new List<Dm.TableRowContent>(logicalRows.Count);
        for (var rowIndex = 0; rowIndex < logicalRows.Count; rowIndex++)
        {
            var cells = new List<Dm.TableCellContent>();
            var columnIndex = 0;
            while (columnIndex < columnCount)
            {
                var slot = projection!.GetSlot(rowIndex, columnIndex);
                if (slot.IsOrigin)
                {
                    var originId = CellId(tableBlock.Id, slot.OriginRow, slot.OriginColumn);
                    cells.Add(ToDocumentCell(
                        slot.Cell,
                        originId,
                        tableContent,
                        slot.OriginColumn,
                        tableContent?.HasHeaderRow == true && rowIndex == 0));
                    columnIndex += Math.Max(1, slot.Cell.ColumnSpan);
                    continue;
                }

                if (slot.OriginRow < rowIndex && slot.OriginColumn == columnIndex)
                {
                    cells.Add(new Dm.TableCellContent
                    {
                        Id = $"{CellId(tableBlock.Id, rowIndex, columnIndex)}-continuation",
                        ColumnSpan = Math.Max(1, slot.Cell.ColumnSpan),
                        RowSpan = 1,
                        Merge = new Dm.TableCellMerge
                        {
                            IsOrigin = false,
                            OriginCellId = CellId(
                                tableBlock.Id,
                                slot.OriginRow,
                                slot.OriginColumn)
                        },
                        Blocks =
                        [
                            CreateParagraph(
                                $"{CellId(tableBlock.Id, rowIndex, columnIndex)}-text",
                                0,
                                string.Empty)
                        ]
                    });
                    columnIndex += Math.Max(1, slot.Cell.ColumnSpan);
                    continue;
                }

                columnIndex++;
            }

            rows.Add(new Dm.TableRowContent { Cells = cells });
        }

        return new Dm.DocumentBlock
        {
            Id = tableBlock.Id.ToString("N"),
            Type = Dm.DocumentBlockType.Table,
            Order = order,
            Content = new Dm.TableBlockContent
            {
                Rows = rows,
                ColumnAlignments = tableContent?.ColumnAlignments
                    .Select(ToDocumentAlignment)
                    .ToList() ?? []
            }
        };
    }

    private static IReadOnlyList<Nm.NotionTableCell> ReadRowCells(
        Nm.IBlockContent content)
    {
        return content is Nm.ITableRowBlockContent row
            ? row.RichCells.Where(cell => !cell.IsMergeHidden).ToList()
            : [];
    }

    private static Nm.NotionAuthoringTableCell ToCanonicalCell(
        Nm.NotionTableCell cell)
        => new()
        {
            Html = Nm.NotionHtmlSanitizer.SanitizeBlockContent(cell.Html),
            Inlines = cell.Inlines,
            BackgroundColor = cell.BackgroundColor,
            TextColor = cell.TextColor,
            HorizontalAlignment = cell.HorizontalAlignment,
            VerticalAlignment = cell.VerticalAlignment,
            RowSpan = Math.Max(1, cell.RowSpan),
            ColumnSpan = Math.Max(1, cell.ColSpan),
            Width = cell.Width,
            Borders = cell.Borders
        };

    private static Dm.TableCellContent ToDocumentCell(
        Nm.NotionAuthoringTableCell cell,
        string cellId,
        Nm.ITableBlockContent? table,
        int columnIndex,
        bool isHeader)
    {
        var width = cell.Width;
        if (width is null && table?.ColumnWidths.Count > columnIndex)
        {
            width = table.ColumnWidths
                .Skip(columnIndex)
                .Take(Math.Max(1, cell.ColumnSpan))
                .Where(value => value is > 0)
                .Sum();
            if (width == 0)
            {
                width = null;
            }
        }

        return new Dm.TableCellContent
        {
            Id = cellId,
            ColumnSpan = Math.Max(1, cell.ColumnSpan),
            RowSpan = Math.Max(1, cell.RowSpan),
            IsHeader = isHeader,
            Merge = new Dm.TableCellMerge { IsOrigin = true },
            Width = width,
            BackgroundColor = cell.BackgroundColor,
            Borders = new Dm.TableCellBorders
            {
                Top = ToBorderCss(cell.Borders.Top),
                Right = ToBorderCss(cell.Borders.Right),
                Bottom = ToBorderCss(cell.Borders.Bottom),
                Left = ToBorderCss(cell.Borders.Left)
            },
            VerticalAlignment = cell.VerticalAlignment switch
            {
                Nm.NotionTableVerticalAlignment.Middle =>
                    Dm.TableCellVerticalAlignment.Middle,
                Nm.NotionTableVerticalAlignment.Bottom =>
                    Dm.TableCellVerticalAlignment.Bottom,
                _ => Dm.TableCellVerticalAlignment.Top
            },
            Blocks =
            [
                new Dm.DocumentBlock
                {
                    Id = $"{cellId}-text",
                    Type = Dm.DocumentBlockType.Paragraph,
                    Order = 0,
                    ParagraphProperties = new Dm.DocumentParagraphProperties
                    {
                        Alignment = cell.HorizontalAlignment switch
                        {
                            Nm.NotionTableHorizontalAlignment.Center =>
                                Dm.DocumentTextAlignment.Center,
                            Nm.NotionTableHorizontalAlignment.Right =>
                                Dm.DocumentTextAlignment.Right,
                            _ => Dm.DocumentTextAlignment.Left
                        }
                    },
                    Content = new Dm.ParagraphBlockContent
                    {
                        Inlines = ToDocumentInlines(cell)
                    }
                }
            ]
        };
    }

    private static List<Dm.InlineContent> ToDocumentInlines(
        Nm.NotionAuthoringTableCell cell)
    {
        if (cell.Inlines.Count == 0)
        {
            return TextInlines(
                    Nm.NotionHtmlSanitizer.SanitizeBlockContent(cell.Html))
                .ToList();
        }

        return cell.Inlines.Select(inline =>
        {
            var marks = new List<Dm.InlineMark>();
            if (inline.Bold)
                marks.Add(new Dm.InlineMark { Type = Dm.InlineMarkType.Bold });
            if (inline.Italic)
                marks.Add(new Dm.InlineMark { Type = Dm.InlineMarkType.Italic });
            if (inline.Underline)
                marks.Add(new Dm.InlineMark { Type = Dm.InlineMarkType.Underline });
            if (inline.Strikethrough)
                marks.Add(new Dm.InlineMark { Type = Dm.InlineMarkType.Strikethrough });
            if (inline.Code)
            {
                marks.Add(new Dm.InlineMark
                {
                    Type = Dm.InlineMarkType.FontFamily,
                    Value = "monospace"
                });
            }
            if (!string.IsNullOrWhiteSpace(inline.Href))
            {
                marks.Add(new Dm.InlineMark
                {
                    Type = Dm.InlineMarkType.Link,
                    Link = new Dm.LinkMarkData { Href = inline.Href }
                });
            }

            var textColor = inline.TextColor ?? cell.TextColor;
            if (!string.IsNullOrWhiteSpace(textColor))
            {
                marks.Add(new Dm.InlineMark
                {
                    Type = Dm.InlineMarkType.TextColor,
                    Value = textColor
                });
            }
            if (!string.IsNullOrWhiteSpace(inline.BackgroundColor))
            {
                marks.Add(new Dm.InlineMark
                {
                    Type = Dm.InlineMarkType.Highlight,
                    Value = inline.BackgroundColor
                });
            }

            return (Dm.InlineContent)new Dm.TextRun
            {
                Text = inline.Text,
                Marks = marks
            };
        }).ToList();
    }

    private static string CellId(Guid tableId, int rowIndex, int columnIndex)
        => $"{tableId:N}-r{rowIndex}-c{columnIndex}";

    private static Dm.TableColumnAlignment ToDocumentAlignment(
        Dm.TableColumnAlignment alignment)
        => alignment;

    private static string? ToBorderCss(Nm.NotionTableBorder? border)
    {
        if (border is null)
        {
            return null;
        }
        if (border.Style == Nm.NotionTableBorderStyle.None)
        {
            return "none";
        }

        var color = string.IsNullOrWhiteSpace(border.Color)
            ? "#000000"
            : border.Color;
        return FormattableString.Invariant(
            $"{border.Width:0.##}px {border.Style.ToString().ToLowerInvariant()} {color}");
    }

    private static Dm.DocumentBlock CreateApproximateParagraph(
        IPageBlock block,
        int order,
        IList<DocumentFormatCompatibilityWarning>? warnings)
    {
        AddApproximateWarning(warnings, block, $"{block.Type} was exported as a text fallback.");
        return CreateParagraph(block.Id.ToString("N"), order, FallbackText(block));
    }

    private static void AddApproximateWarning(
        IList<DocumentFormatCompatibilityWarning>? warnings,
        IPageBlock block,
        string message)
    {
        warnings?.Add(new DocumentFormatCompatibilityWarning
        {
            Code = ApproximateWarningCode,
            Message = message,
            Severity = DocumentFormatCompatibilitySeverity.Warning,
            SourcePath = $"notion:block:{block.Type}:{block.Id:D}",
            ObjectId = block.Id.ToString("N")
        });
    }

    private static string FallbackText(IPageBlock block)
    {
        return block.Content switch
        {
            Nm.ITextBlockContent text => HtmlToText(text.Html),
            Nm.ICodeBlockContent code => FormatLabel("Code", string.IsNullOrWhiteSpace(code.Language) ? code.Code : $"{code.Language}: {code.Code}", code.Caption),
            Nm.IEquationBlockContent equation => FormatLabel("Equation", equation.Expression),
            Nm.IVideoBlockContent video => FormatLabel("Video", video.Url, video.Caption),
            Nm.IAudioBlockContent audio => FormatLabel("Audio", audio.Url, audio.Caption),
            Nm.IFileBlockContent file => FormatLabel("File", string.IsNullOrWhiteSpace(file.Url) ? file.FileName : $"{file.FileName} {file.Url}", file.Caption),
            Nm.IPdfBlockContent pdf => FormatLabel("PDF", pdf.Url, pdf.Caption),
            Nm.IBookmarkBlockContent bookmark => FormatLabel("Bookmark", FirstNonEmpty(bookmark.Title, bookmark.Url), bookmark.Description, bookmark.Caption),
            Nm.IEmbedBlockContent embed => FormatLabel("Embed", embed.Url, embed.Caption),
            Nm.IChildPageBlockContent childPage => FormatLabel("Child page", FirstNonEmpty(childPage.Title, childPage.ChildPageId.ToString("D"))),
            Nm.ILinkedPageBlockContent linkedPage => FormatLabel("Linked page", FirstNonEmpty(linkedPage.Title, linkedPage.LinkedPageId.ToString("D"))),
            Nm.ISyncedBlockOriginContent origin => FormatLabel("Synced block", origin.SyncId.ToString("D")),
            Nm.ISyncedBlockRefContent reference => FormatLabel("Synced block reference", reference.SyncId.ToString("D")),
            Nm.IInlineDatabaseBlockContent database => FormatLabel("Database", FirstNonEmpty(database.Title, database.DatabaseId.ToString("D"))),
            Nm.ILinkedDatabaseBlockContent linkedDatabase => FormatLabel("Linked database", linkedDatabase.SourceDatabaseId.ToString("D")),
            Nm.IColumnListBlockContent columns => FormatLabel("Columns", columns.ColumnCount.ToString()),
            Nm.IColumnBlockContent column => FormatLabel("Column", $"{column.ColumnIndex + 1} ({column.WidthPercent:0.##}%)"),
            Nm.ITemplateButtonBlockContent template => FormatLabel("Template button", template.Label),
            Nm.ITableOfContentsBlockContent toc => FormatLabel("Table of contents", $"max level {toc.MaxLevel}"),
            Nm.IDiagramBlockContent diagram => FormatLabel("Diagram", FirstNonEmpty(diagram.Caption, diagram.DiagramDocumentId.ToString("D"))),
            Nm.IWireframeBlockContent wireframe => FormatLabel("Wireframe", FirstNonEmpty(wireframe.Caption, wireframe.WireframeDocumentId.ToString("D"))),
            Nm.ISpreadsheetBlockContent spreadsheet => FormatLabel("Spreadsheet", FirstNonEmpty(spreadsheet.Caption, spreadsheet.SpreadsheetDocumentId.ToString("D"))),
            Nm.IWorkItemBlockContent workItem => FormatLabel("Work item", FirstNonEmpty(workItem.CachedSnapshot?.Title, workItem.ExternalId), workItem.CachedSnapshot?.StatusLabel),
            Nm.IContentByLabelBlockContent labels => FormatLabel("Content by label", string.Join(", ", labels.Labels)),
            Nm.IIncludePageBlockContent includePage => FormatLabel("Included page", includePage.SourcePageId?.ToString("D") ?? string.Empty),
            Nm.IChildrenDisplayBlockContent children => FormatLabel("Children", children.RootPageId?.ToString("D") ?? "current page"),
            Nm.IExcerptBlockContent excerpt => FormatLabel("Excerpt", HtmlToText(excerpt.Html)),
            Nm.IExcerptIncludeBlockContent excerptInclude => FormatLabel("Excerpt include", excerptInclude.SourcePageId?.ToString("D") ?? string.Empty),
            Nm.IPagePropertiesBlockContent properties => FormatLabel("Page properties", string.Join("; ", properties.Rows.Select(row => $"{row.Key}: {HtmlToText(row.ValueHtml)}"))),
            Nm.IPagePropertiesReportBlockContent report => FormatLabel("Page properties report", string.Join(", ", report.Columns.Concat(report.Labels))),
            Nm.IBreadcrumbBlockContent => "Breadcrumb",
            Nm.IDividerBlockContent => "────",
            _ => block.Type.ToString()
        };
    }

    private static string FormatLabel(string label, params string?[] parts)
    {
        var value = string.Join(" · ", parts.Where(part => !string.IsNullOrWhiteSpace(part)).Select(part => part!.Trim()));
        return string.IsNullOrWhiteSpace(value) ? label : $"{label}: {value}";
    }

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static IEnumerable<Dm.InlineContent> TextInlines(Nm.ITextBlockContent? content)
        => TextInlines(content?.Html);

    private static IEnumerable<Dm.InlineContent> TextInlines(string? html)
    {
        var runs = ParseInlineHtml(html).ToList();
        return runs.Count == 0 ? [new Dm.TextRun { Text = string.Empty }] : runs;
    }

    private static IEnumerable<Dm.InlineContent> ParseInlineHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return [];
        }

        var parsed = new List<Dm.InlineContent>();
        var marks = new Stack<Dm.InlineMark>();
        var position = 0;
        foreach (Match match in HtmlTagRegex().Matches(html))
        {
            AppendText(html[position..match.Index]);
            var tag = match.Groups["tag"].Value;
            var closing = match.Groups["closing"].Success;
            var attributes = match.Groups["attrs"].Value;

            if (closing)
            {
                PopMarkForTag(tag);
            }
            else if (string.Equals(tag, "br", StringComparison.OrdinalIgnoreCase))
            {
                AppendText("\n");
            }
            else
            {
                var mark = MarkForTag(tag, attributes);
                if (mark is not null)
                {
                    marks.Push(mark);
                }
            }

            position = match.Index + match.Length;
        }

        AppendText(html[position..]);

        void AppendText(string raw)
        {
            var text = WebUtility.HtmlDecode(StripResidualTags(raw));
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            parsed.Add(new Dm.TextRun { Text = text, Marks = marks.Reverse().Select(CloneMark).ToList() });
        }

        void PopMarkForTag(string tag)
        {
            var type = MarkTypeForTag(tag);
            if (type is null)
            {
                return;
            }

            var retained = new Stack<Dm.InlineMark>();
            while (marks.Count > 0)
            {
                var mark = marks.Pop();
                if (mark.Type == type)
                {
                    break;
                }

                retained.Push(mark);
            }

            while (retained.Count > 0)
            {
                marks.Push(retained.Pop());
            }
        }

        return parsed;
    }

    private static Dm.InlineMark? MarkForTag(string tag, string attributes)
    {
        var type = MarkTypeForTag(tag);
        if (type is null)
        {
            return null;
        }

        var mark = new Dm.InlineMark { Type = type.Value };
        if (type == Dm.InlineMarkType.Link)
        {
            var href = AttributeValue(attributes, "href");
            if (!string.IsNullOrWhiteSpace(href))
            {
                mark.Link = new Dm.LinkMarkData { Href = WebUtility.HtmlDecode(href) };
            }
        }

        return mark;
    }

    private static Dm.InlineMarkType? MarkTypeForTag(string tag)
    {
        return tag.ToLowerInvariant() switch
        {
            "strong" or "b" => Dm.InlineMarkType.Bold,
            "em" or "i" => Dm.InlineMarkType.Italic,
            "u" => Dm.InlineMarkType.Underline,
            "s" or "strike" or "del" => Dm.InlineMarkType.Strikethrough,
            "code" => Dm.InlineMarkType.FontFamily,
            "a" => Dm.InlineMarkType.Link,
            _ => null
        };
    }

    private static Dm.InlineMark CloneMark(Dm.InlineMark mark)
        => new()
        {
            Type = mark.Type,
            Value = mark.Value,
            Link = mark.Link is null ? null : new Dm.LinkMarkData { Href = mark.Link.Href, Title = mark.Link.Title },
            CommentAnchor = mark.CommentAnchor,
            RevisionId = mark.RevisionId
        };

    private static string HtmlToText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var withBreaks = BlockBreakRegex().Replace(
            Nm.NotionHtmlSanitizer.SanitizeBlockContent(html),
            "\n");
        return WebUtility.HtmlDecode(StripResidualTags(withBreaks)).Trim();
    }

    private static string StripResidualTags(string value)
        => ResidualTagRegex().Replace(value, string.Empty);

    private static string? AttributeValue(string attributes, string attributeName)
    {
        var match = Regex.Match(attributes, $@"\b{Regex.Escape(attributeName)}\s*=\s*(?:""(?<v>[^""]*)""|'(?<v>[^']*)'|(?<v>[^\s>]+))", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));
        return match.Success ? match.Groups["v"].Value : null;
    }

    [GeneratedRegex("<(?<closing>/)?(?<tag>[a-zA-Z][a-zA-Z0-9:-]*)(?<attrs>[^>]*)>", RegexOptions.Compiled)]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex("</?(p|div|li|br|h[1-6]|tr|table|blockquote)[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex BlockBreakRegex();

    [GeneratedRegex("<[^>]+>", RegexOptions.Compiled)]
    private static partial Regex ResidualTagRegex();
}

/// <summary>Result of converting Notion blocks into a document model.</summary>
public sealed record NotionToDocumentModelConversionResult(
    Dm.DocumentEditorDocument Document,
    IReadOnlyList<DocumentFormatCompatibilityWarning> Warnings);
