using Tempo.Blazor.DocumentEditor.Models;
using Wyg = Tempo.Blazor.Components.DocumentEditor.Wysiwyg.Model;

namespace Tempo.Blazor.Components.DocumentEditor.Wysiwyg.Serialization;

/// <summary>Serializes between the WYSIWYG editing model and the persistence JSON model.</summary>
public class DocumentSerializer
{
    /// <summary>Convert WYSIWYG model to persistence model.</summary>
    public DocumentEditorDocument ToPersistenceModel(Wyg.DocumentModel model)
    {
        var result = new DocumentEditorDocument
        {
            DocumentId = model.Id,
            Metadata = new DocumentEditorMetadata
            {
                Title = model.Metadata.Title,
                Author = model.Metadata.AuthorId is not null || model.Metadata.AuthorName is not null
                    ? new DocumentEditorAuthor
                    {
                        Id = model.Metadata.AuthorId ?? string.Empty,
                        DisplayName = model.Metadata.AuthorName ?? string.Empty
                    }
                    : null,
                CreatedAt = model.Metadata.CreatedAt ?? DateTimeOffset.UtcNow,
                ModifiedAt = model.Metadata.ModifiedAt
            },
            PageSettings = ToPersistencePageSettings(model.PageSettings),
            Assets = [],
            Sections = model.Sections.Select(ToPersistenceSection).ToList(),
            Anchors = [],
            HeadersFooters = model.HeadersFooters.Select(ToPersistenceHeaderFooter).ToList(),
            Notes = model.Notes.Select(ToPersistenceNote).ToList(),
            Comments = model.Comments.Select(ToPersistenceComment).ToList(),
            Revisions = model.Revisions.Select(ToPersistenceRevision).ToList()
        };

        foreach (var block in model.Body)
        {
            result.Blocks.Add(ToPersistenceBlock(block));
        }

        return result;
    }

    /// <summary>Convert persistence model to WYSIWYG model.</summary>
    public Wyg.DocumentModel FromPersistenceModel(DocumentEditorDocument persistence)
    {
        var result = new Wyg.DocumentModel
        {
            Id = persistence.DocumentId,
            Metadata = new Wyg.DocumentMetadata
            {
                Title = persistence.Metadata?.Title ?? string.Empty,
                AuthorId = persistence.Metadata?.Author?.Id,
                AuthorName = persistence.Metadata?.Author?.DisplayName,
                CreatedAt = persistence.Metadata?.CreatedAt,
                ModifiedAt = persistence.Metadata?.ModifiedAt
            },
            PageSettings = FromPersistencePageSettings(persistence.PageSettings)
        };

        foreach (var block in persistence.Blocks)
        {
            result.Body.Add(FromPersistenceBlock(block));
        }

        foreach (var headerFooter in persistence.HeadersFooters)
        {
            result.HeadersFooters.Add(FromPersistenceHeaderFooter(headerFooter));
        }

        foreach (var note in persistence.Notes)
        {
            result.Notes.Add(FromPersistenceNote(note));
        }

        foreach (var comment in persistence.Comments)
        {
            result.Comments.Add(FromPersistenceComment(comment));
        }

        foreach (var revision in persistence.Revisions)
        {
            result.Revisions.Add(FromPersistenceRevision(revision));
        }

        return result;
    }

    /// <summary>Serialize WYSIWYG model to legacy JSON string.</summary>
    public string Serialize(Wyg.DocumentModel document) =>
        System.Text.Json.JsonSerializer.Serialize(ToPersistenceModel(document));

    /// <summary>Deserialize legacy JSON string to WYSIWYG model.</summary>
    public Wyg.DocumentModel Deserialize(string json) =>
        FromPersistenceModel(System.Text.Json.JsonSerializer.Deserialize<DocumentEditorDocument>(json)!);

    private static DocumentBlock ToPersistenceBlock(Wyg.Block block)
    {
        return block switch
        {
            Wyg.ParagraphBlock p => new DocumentBlock
            {
                Type = DocumentBlockType.Paragraph,
                Content = ToPersistenceParagraphContent(p)
            },
            Wyg.HeadingBlock h => new DocumentBlock
            {
                Type = DocumentBlockType.Heading,
                Content = new HeadingBlockContent
                {
                    Level = h.Level,
                    Inlines = h.Inlines.Select(ToPersistenceInline).ToList()
                }
            },
            Wyg.ListItemBlock l => new DocumentBlock
            {
                Type = DocumentBlockType.List,
                Content = new ListBlockContent
                {
                    Ordered = l.Ordered,
                    IndentLevel = l.IndentLevel,
                    Inlines = l.Inlines.Select(ToPersistenceInline).ToList()
                }
            },
            Wyg.TableBlock t => new DocumentBlock
            {
                Type = DocumentBlockType.Table,
                Content = ToPersistenceTableContent(t)
            },
            Wyg.ImageBlock i => new DocumentBlock
            {
                Type = DocumentBlockType.Image,
                Content = new ImageBlockContent
                {
                    Source = DocumentImageSource.Url,
                    Url = i.Src,
                    AltText = i.Alt,
                    Size = i.Size.Width is null && i.Size.Height is null
                        ? new DocumentImageSize()
                        : new DocumentImageSize
                        {
                            Width = CssLengthToPoints(i.Size.Width),
                            Height = CssLengthToPoints(i.Size.Height)
                        }
                }
            },
            Wyg.PageBreakBlock => new DocumentBlock
            {
                Type = DocumentBlockType.PageBreak,
                Content = new PageBreakBlockContent()
            },
            Wyg.SectionBreakBlock => new DocumentBlock
            {
                Type = DocumentBlockType.PageBreak,
                Content = new PageBreakBlockContent()
            },
            _ => new DocumentBlock
            {
                Type = DocumentBlockType.Paragraph,
                Content = new ParagraphBlockContent()
            }
        };
    }

    private static ParagraphBlockContent ToPersistenceParagraphContent(Wyg.ParagraphBlock block)
    {
        return new ParagraphBlockContent
        {
            Inlines = block.Inlines.Select(ToPersistenceInline).ToList()
        };
    }

    private static TableBlockContent ToPersistenceTableContent(Wyg.TableBlock table)
    {
        return new TableBlockContent
        {
            Rows = table.Rows.Select(r => new TableRowContent
            {
                Cells = r.Cells.Select(c => new TableCellContent
                {
                    Blocks = c.Blocks.Select(ToPersistenceBlock).ToList(),
                    ColumnSpan = c.ColumnSpan,
                    RowSpan = c.RowSpan
                }).ToList()
            }).ToList()
        };
    }

    private static InlineContent ToPersistenceInline(Wyg.Inline inline)
    {
        return inline switch
        {
            Wyg.TextRun t => new TextRun
            {
                Text = t.Text,
                Marks = t.Marks.Select(ToPersistenceMark).ToList()
            },
            Wyg.HardBreak => new TextRun { Text = "\n" },
            Wyg.TabInline => new TextRun { Text = "\t" },
            _ => new TextRun { Text = string.Empty }
        };
    }

    private static InlineMark ToPersistenceMark(Wyg.Mark mark)
    {
        return mark switch
        {
            Wyg.BoldMark => new InlineMark { Type = InlineMarkType.Bold },
            Wyg.ItalicMark => new InlineMark { Type = InlineMarkType.Italic },
            Wyg.UnderlineMark => new InlineMark { Type = InlineMarkType.Underline },
            Wyg.StrikethroughMark => new InlineMark { Type = InlineMarkType.Strikethrough },
            Wyg.SubscriptMark => new InlineMark { Type = InlineMarkType.Subscript },
            Wyg.SuperscriptMark => new InlineMark { Type = InlineMarkType.Superscript },
            Wyg.LinkMark l => new InlineMark
            {
                Type = InlineMarkType.Link,
                Link = new LinkMarkData { Href = l.Href }
            },
            _ => new InlineMark { Type = InlineMarkType.Bold }
        };
    }

    private static Wyg.Block FromPersistenceBlock(DocumentBlock block)
    {
        return block.Type switch
        {
            DocumentBlockType.Paragraph => new Wyg.ParagraphBlock
            {
                Inlines = block.Content is ParagraphBlockContent pc
                    ? pc.Inlines.Select(FromPersistenceInline).ToList()
                    : []
            },
            DocumentBlockType.Heading => new Wyg.HeadingBlock
            {
                Level = block.Content is HeadingBlockContent hc ? hc.Level : 1,
                Inlines = block.Content is HeadingBlockContent hc2
                    ? hc2.Inlines.Select(FromPersistenceInline).ToList()
                    : []
            },
            DocumentBlockType.List => new Wyg.ListItemBlock
            {
                Ordered = block.Content is ListBlockContent lc ? lc.Ordered : false,
                IndentLevel = block.Content is ListBlockContent lc2 ? lc2.IndentLevel : 0,
                Inlines = block.Content is ListBlockContent lc3
                    ? lc3.Inlines.Select(FromPersistenceInline).ToList()
                    : []
            },
            DocumentBlockType.Table => FromPersistenceTableBlock(block.Content as TableBlockContent),
            DocumentBlockType.Image => new Wyg.ImageBlock
            {
                Src = block.Content is ImageBlockContent ic ? ic.Url ?? string.Empty : string.Empty,
                Alt = block.Content is ImageBlockContent ic2 ? ic2.AltText ?? string.Empty : string.Empty,
                Size = block.Content is ImageBlockContent ic3
                    ? new Wyg.ImageSize
                    {
                        Width = ic3.Size?.Width?.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        Height = ic3.Size?.Height?.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    }
                    : new Wyg.ImageSize()
            },
            DocumentBlockType.PageBreak => new Wyg.PageBreakBlock(),
            _ => new Wyg.ParagraphBlock()
        };
    }

    private static Wyg.TableBlock FromPersistenceTableBlock(TableBlockContent? content)
    {
        var table = new Wyg.TableBlock();
        if (content?.Rows is null) return table;

        foreach (var row in content.Rows)
        {
            var tableRow = new Wyg.TableRow();
            foreach (var cell in row.Cells)
            {
                var tableCell = new Wyg.TableCell
                {
                    ColumnSpan = cell.ColumnSpan,
                    RowSpan = cell.RowSpan
                };
                foreach (var block in cell.Blocks)
                {
                    tableCell.Blocks.Add(FromPersistenceBlock(block));
                }
                tableRow.Cells.Add(tableCell);
            }
            table.Rows.Add(tableRow);
        }
        return table;
    }

    private static Wyg.Inline FromPersistenceInline(InlineContent inline)
    {
        if (inline is not TextRun textRun)
            return new Wyg.TextRun { Text = string.Empty };

        var result = new Wyg.TextRun { Text = textRun.Text ?? string.Empty };
        foreach (var mark in textRun.Marks)
        {
            Wyg.Mark? wysiwygMark = mark.Type switch
            {
                InlineMarkType.Bold => new Wyg.BoldMark(),
                InlineMarkType.Italic => new Wyg.ItalicMark(),
                InlineMarkType.Underline => new Wyg.UnderlineMark(),
                InlineMarkType.Strikethrough => new Wyg.StrikethroughMark(),
                InlineMarkType.Subscript => new Wyg.SubscriptMark(),
                InlineMarkType.Superscript => new Wyg.SuperscriptMark(),
                InlineMarkType.Link => new Wyg.LinkMark { Href = mark.Link?.Href ?? string.Empty },
                _ => null
            };
            if (wysiwygMark is not null)
                result.Marks.Add(wysiwygMark);
        }
        return result;
    }

    private static DocumentPageSettings? ToPersistencePageSettings(Wyg.PageSettings? settings)
    {
        if (settings is null) return null;

        double widthPts = 595.276;
        double heightPts = 841.89;
        double marginTop = 72;
        double marginBottom = 72;
        double marginLeft = 72;
        double marginRight = 72;

        // Naive CSS-to-points conversion (1pt ≈ 1.333px, 1mm ≈ 2.835pt, 1in = 72pt)
        if (!string.IsNullOrWhiteSpace(settings.Width)) widthPts = CssLengthToPoints(settings.Width) ?? widthPts;
        if (!string.IsNullOrWhiteSpace(settings.Height)) heightPts = CssLengthToPoints(settings.Height) ?? heightPts;
        if (!string.IsNullOrWhiteSpace(settings.MarginTop)) marginTop = CssLengthToPoints(settings.MarginTop) ?? marginTop;
        if (!string.IsNullOrWhiteSpace(settings.MarginBottom)) marginBottom = CssLengthToPoints(settings.MarginBottom) ?? marginBottom;
        if (!string.IsNullOrWhiteSpace(settings.MarginLeft)) marginLeft = CssLengthToPoints(settings.MarginLeft) ?? marginLeft;
        if (!string.IsNullOrWhiteSpace(settings.MarginRight)) marginRight = CssLengthToPoints(settings.MarginRight) ?? marginRight;

        return new DocumentPageSettings
        {
            Size = new DocumentPageSize { Width = widthPts, Height = heightPts },
            Margins = new DocumentPageMargins
            {
                Top = marginTop,
                Bottom = marginBottom,
                Left = marginLeft,
                Right = marginRight
            },
            Landscape = settings.Orientation == Wyg.PageOrientation.Landscape
        };
    }

    private static Wyg.PageSettings FromPersistencePageSettings(DocumentPageSettings? settings)
    {
        if (settings is null) return Wyg.PageSettings.DefaultA4();

        string ToCssLength(double pts) => $"{pts}pt";

        return new Wyg.PageSettings
        {
            Width = ToCssLength(settings.Size?.Width ?? 595.276),
            Height = ToCssLength(settings.Size?.Height ?? 841.89),
            MarginTop = ToCssLength(settings.Margins?.Top ?? 72),
            MarginBottom = ToCssLength(settings.Margins?.Bottom ?? 72),
            MarginLeft = ToCssLength(settings.Margins?.Left ?? 72),
            MarginRight = ToCssLength(settings.Margins?.Right ?? 72),
            Orientation = settings.Landscape ? Wyg.PageOrientation.Landscape : Wyg.PageOrientation.Portrait
        };
    }

    private static double? CssLengthToPoints(string css)
    {
        var span = css.AsSpan().Trim();
        if (span.Length == 0) return null;

        // Find where digits end
        int i = 0;
        if (span[0] == '-' || span[0] == '+') i++;
        while (i < span.Length && (char.IsDigit(span[i]) || span[i] == '.')) i++;
        if (!double.TryParse(span[..i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value))
            return null;

        var unit = span[i..].Trim().ToString().ToLowerInvariant();
        return unit switch
        {
            "px" => value * 0.75,
            "mm" => value * 2.83464567,
            "cm" => value * 28.3464567,
            "in" => value * 72,
            "pt" => value,
            "pc" => value * 12,
            _ => value
        };
    }

    private static DocumentSection ToPersistenceSection(Wyg.Section section)
    {
        return new DocumentSection
        {
            Id = section.Id,
            Order = 0,
            Properties = new DocumentSectionProperties
            {
                DifferentFirstPage = section.Properties.DifferentFirstPage,
                DifferentOddAndEvenPages = section.Properties.DifferentOddEvenPages
            }
        };
    }

    private static DocumentHeaderFooter ToPersistenceHeaderFooter(Wyg.HeaderFooter headerFooter)
    {
        return new DocumentHeaderFooter
        {
            Id = headerFooter.Id,
            Type = headerFooter.Type == Wyg.HeaderFooterType.Header
                ? DocumentHeaderFooterType.Header
                : DocumentHeaderFooterType.Footer,
            Scope = headerFooter.Scope switch
            {
                Wyg.HeaderFooterScope.FirstPage => DocumentHeaderFooterScope.FirstPage,
                Wyg.HeaderFooterScope.EvenPage => DocumentHeaderFooterScope.EvenPages,
                _ => DocumentHeaderFooterScope.Primary
            },
            SectionId = null,
            Blocks = headerFooter.Blocks.Select(ToPersistenceBlock).ToList()
        };
    }

    private static Wyg.HeaderFooter FromPersistenceHeaderFooter(DocumentHeaderFooter headerFooter)
    {
        var result = new Wyg.HeaderFooter
        {
            Type = headerFooter.Type == DocumentHeaderFooterType.Header
                ? Wyg.HeaderFooterType.Header
                : Wyg.HeaderFooterType.Footer,
            Scope = headerFooter.Scope switch
            {
                DocumentHeaderFooterScope.FirstPage => Wyg.HeaderFooterScope.FirstPage,
                DocumentHeaderFooterScope.EvenPages => Wyg.HeaderFooterScope.EvenPage,
                DocumentHeaderFooterScope.OddPages => Wyg.HeaderFooterScope.Primary,
                _ => Wyg.HeaderFooterScope.Primary
            }
        };
        foreach (var block in headerFooter.Blocks)
        {
            result.Blocks.Add(FromPersistenceBlock(block));
        }
        return result;
    }

    private static DocumentNote ToPersistenceNote(Wyg.DocumentNote note)
    {
        return new DocumentNote
        {
            Id = note.Id,
            Type = note.NoteType == Wyg.DocumentNoteType.Endnote ? DocumentNoteType.Endnote : DocumentNoteType.Footnote,
            Marker = note.Marker,
            Blocks = note.Blocks.Select(ToPersistenceBlock).ToList()
        };
    }

    private static Wyg.DocumentNote FromPersistenceNote(DocumentNote note)
    {
        var result = new Wyg.DocumentNote
        {
            NoteType = note.Type == DocumentNoteType.Endnote ? Wyg.DocumentNoteType.Endnote : Wyg.DocumentNoteType.Footnote,
            Marker = note.Marker ?? string.Empty
        };
        foreach (var block in note.Blocks)
        {
            result.Blocks.Add(FromPersistenceBlock(block));
        }
        return result;
    }

    private static DocumentComment ToPersistenceComment(Wyg.DocumentComment comment)
    {
        return new DocumentComment
        {
            Id = comment.Id,
            Anchor = new DocumentCommentAnchor
            {
                Type = DocumentCommentAnchorType.TextRange,
                BlockId = comment.Anchor.StartBlockId,
                StartInlineIndex = comment.Anchor.StartInlineIndex,
                StartOffset = comment.Anchor.StartTextOffset,
                EndInlineIndex = comment.Anchor.EndInlineIndex,
                EndOffset = comment.Anchor.EndTextOffset
            },
            Visibility = DocumentCommentVisibility.Internal,
            Entries = comment.Entries.Select(e => new DocumentCommentEntry
            {
                Author = new DocumentEditorAuthor { Id = e.AuthorId ?? string.Empty, DisplayName = e.AuthorName ?? string.Empty },
                Text = e.Text
            }).ToList(),
            Status = comment.IsResolved ? DocumentCommentStatus.Resolved : DocumentCommentStatus.Open
        };
    }

    private static Wyg.DocumentComment FromPersistenceComment(DocumentComment comment)
    {
        var result = new Wyg.DocumentComment
        {
            IsResolved = comment.Status == DocumentCommentStatus.Resolved
        };
        if (comment.Anchor is not null)
        {
            result.Anchor = new Wyg.DocumentCommentAnchor
            {
                StartBlockId = comment.Anchor.BlockId ?? string.Empty,
                StartInlineIndex = comment.Anchor.StartInlineIndex ?? 0,
                StartTextOffset = comment.Anchor.StartOffset ?? 0,
                EndBlockId = comment.Anchor.BlockId ?? string.Empty,
                EndInlineIndex = comment.Anchor.EndInlineIndex ?? 0,
                EndTextOffset = comment.Anchor.EndOffset ?? 0
            };
        }
        foreach (var entry in comment.Entries)
        {
            result.Entries.Add(new Wyg.DocumentCommentEntry
            {
                AuthorId = entry.Author?.Id,
                AuthorName = entry.Author?.DisplayName,
                Text = entry.Text
            });
        }
        return result;
    }

    private static DocumentRevision ToPersistenceRevision(Wyg.DocumentRevision revision)
    {
        return new DocumentRevision
        {
            Id = revision.Id,
            Type = (DocumentRevisionType)revision.Type,
            Author = new DocumentRevisionAuthor
            {
                Id = revision.AuthorId ?? string.Empty,
                DisplayName = revision.AuthorName ?? string.Empty
            },
            CreatedAt = revision.CreatedAt,
            Action = (DocumentRevisionAction)revision.Action
        };
    }

    private static Wyg.DocumentRevision FromPersistenceRevision(DocumentRevision revision)
    {
        return new Wyg.DocumentRevision
        {
            Type = (Wyg.DocumentRevisionType)revision.Type,
            AuthorId = revision.Author?.Id,
            AuthorName = revision.Author?.DisplayName,
            CreatedAt = revision.CreatedAt,
            Action = (Wyg.DocumentRevisionAction)revision.Action
        };
    }
}
