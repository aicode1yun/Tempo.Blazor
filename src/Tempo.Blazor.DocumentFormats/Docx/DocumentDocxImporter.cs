using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentFormats.Internal;
using W = DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;

namespace Tempo.Blazor.DocumentFormats.Docx;

/// <summary>Imports DOCX packages into the document editor JSON model.</summary>
public sealed class DocumentDocxImporter : IDocumentFormatImporter
{
    /// <inheritdoc />
    public async Task<DocumentFormatImportResult> ImportAsync(Stream stream, DocumentFormatImportOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new DocumentFormatImportOptions();

        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);
        memory.Position = 0;

        using var word = WordprocessingDocument.Open(memory, false);
        var reader = new DocxPackageReader(word, options);
        return await reader.ReadAsync(cancellationToken);
    }
}

/// <summary>Reads WordprocessingML package parts into the editor model.</summary>
public sealed class DocxPackageReader
{
    private readonly WordprocessingDocument _document;
    private readonly DocumentFormatImportOptions _options;
    private readonly List<DocumentFormatCompatibilityWarning> _warnings = [];
    private readonly List<DocumentFormatPreservedPart> _preservedParts = [];
    private readonly Dictionary<string, string> _hyperlinks = new(StringComparer.Ordinal);
    private int _order;

    /// <summary>Creates a DOCX package reader.</summary>
    public DocxPackageReader(WordprocessingDocument document, DocumentFormatImportOptions options)
    {
        _document = document;
        _options = options;
    }

    /// <summary>Reads the package.</summary>
    public async Task<DocumentFormatImportResult> ReadAsync(CancellationToken cancellationToken = default)
    {
        var doc = DocumentEditorDocument.Empty(_options.DocumentId);
        doc.Metadata.Title = GetTitleFallback();
        doc.Metadata.ModifiedAt = DateTimeOffset.UtcNow;

        var main = _document.MainDocumentPart;
        if (main?.Document.Body is null)
        {
            _warnings.Add(Warning("docx.empty", "DOCX package does not contain a main document body.", DocumentFormatCompatibilitySeverity.Dropped, "word/document.xml"));
            return Result(doc);
        }

        foreach (var relationship in main.HyperlinkRelationships)
        {
            _hyperlinks[relationship.Id] = relationship.Uri.ToString();
        }

        foreach (var element in main.Document.Body.Elements())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (element is W.Paragraph paragraph)
            {
                var blocks = await ReadParagraphAsync(paragraph, main, cancellationToken);
                doc.Blocks.AddRange(blocks);
            }
            else if (element is W.Table table)
            {
                doc.Blocks.Add(ReadTable(table));
            }
        }

        doc.HeadersFooters.AddRange(ReadHeadersFooters(main));
        doc.Notes.AddRange(ReadNotes(main));
        doc.Comments.AddRange(ReadComments(main));
        doc.Revisions.AddRange(ReadRevisions(main.Document.Body));
        ReadSectionProperties(doc, main.Document.Body);
        PreserveUnsupportedParts();

        if (doc.Blocks.Count == 0)
        {
            doc.Blocks.Add(DocumentModelText.Paragraph(string.Empty, _order++));
        }

        return Result(doc);
    }

    private DocumentFormatImportResult Result(DocumentEditorDocument doc)
    {
        return new DocumentFormatImportResult
        {
            Document = doc,
            Format = DocumentFormatKind.Docx,
            Warnings = _warnings,
            PreservedParts = _preservedParts
        };
    }

    private string GetTitleFallback()
    {
        var title = _document.PackageProperties.Title;
        if (!string.IsNullOrWhiteSpace(title))
        {
            return title;
        }

        if (!string.IsNullOrWhiteSpace(_options.FileName))
        {
            return Path.GetFileNameWithoutExtension(_options.FileName);
        }

        return "Imported DOCX";
    }

    private async Task<List<DocumentBlock>> ReadParagraphAsync(W.Paragraph paragraph, MainDocumentPart mainPart, CancellationToken cancellationToken)
    {
        var blocks = new List<DocumentBlock>();
        var pageBreakSeen = false;
        if (paragraph.Descendants<W.Break>().Any(b => b.Type?.Value == W.BreakValues.Page))
        {
            pageBreakSeen = true;
        }

        foreach (var drawing in paragraph.Descendants<W.Drawing>())
        {
            var image = await ReadImageAsync(drawing, mainPart, cancellationToken);
            if (image is not null)
            {
                blocks.Add(new DocumentBlock
                {
                    Type = DocumentBlockType.Image,
                    Order = _order++,
                    Content = image
                });
            }
        }

        var inlines = ReadInlines(paragraph.ChildElements);
        if (inlines.Count > 0 || blocks.Count == 0)
        {
            blocks.Insert(0, new DocumentBlock
            {
                Type = GetParagraphType(paragraph, out var headingLevel, out var ordered, out var indent),
                Order = _order++,
                Content = CreateTextContent(paragraph, inlines, headingLevel, ordered, indent)
            });
        }

        if (pageBreakSeen)
        {
            blocks.Add(new DocumentBlock
            {
                Type = DocumentBlockType.PageBreak,
                Order = _order++,
                Content = new PageBreakBlockContent()
            });
        }

        return blocks;
    }

    private static DocumentBlockType GetParagraphType(W.Paragraph paragraph, out int headingLevel, out bool ordered, out int indent)
    {
        headingLevel = 0;
        ordered = false;
        indent = 0;

        var style = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
        if (!string.IsNullOrWhiteSpace(style) && style.StartsWith("Heading", StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(new string(style.Where(char.IsDigit).ToArray()), out var parsed))
            {
                headingLevel = Math.Clamp(parsed, 1, 6);
            }
            else
            {
                headingLevel = 1;
            }

            return DocumentBlockType.Heading;
        }

        var numbering = paragraph.ParagraphProperties?.NumberingProperties;
        if (numbering is not null)
        {
            ordered = numbering.NumberingId?.Val?.Value != 1;
            indent = numbering.NumberingLevelReference?.Val?.Value ?? 0;
            return DocumentBlockType.List;
        }

        return DocumentBlockType.Paragraph;
    }

    private static DocumentBlockContent CreateTextContent(W.Paragraph paragraph, List<InlineContent> inlines, int headingLevel, bool ordered, int indent)
    {
        return GetParagraphType(paragraph, out _, out _, out _) switch
        {
            DocumentBlockType.Heading => new HeadingBlockContent { Level = headingLevel <= 0 ? 1 : headingLevel, Inlines = inlines },
            DocumentBlockType.List => new ListBlockContent { Ordered = ordered, IndentLevel = indent, Inlines = inlines },
            _ => new ParagraphBlockContent { Inlines = inlines }
        };
    }

    private List<InlineContent> ReadInlines(IEnumerable<OpenXmlElement> elements, List<InlineMark>? inheritedMarks = null)
    {
        var result = new List<InlineContent>();
        var inherited = inheritedMarks ?? [];

        foreach (var element in elements)
        {
            if (element is W.Run run)
            {
                var marks = MergeMarks(inherited, ReadRunMarks(run.RunProperties));
                if (run.Descendants<W.Drawing>().Any())
                {
                    continue;
                }

                var text = string.Concat(run.Elements<W.Text>().Select(t => t.Text));
                if (!string.IsNullOrEmpty(text))
                {
                    result.Add(new TextRun { Text = text, Marks = marks });
                }

                foreach (var noteRef in run.Elements<W.FootnoteReference>())
                {
                    result.Add(new DocumentNoteReferenceRun { NoteId = noteRef.Id?.ToString() ?? string.Empty, NoteType = DocumentNoteType.Footnote });
                }

                foreach (var noteRef in run.Elements<W.EndnoteReference>())
                {
                    result.Add(new DocumentNoteReferenceRun { NoteId = noteRef.Id?.ToString() ?? string.Empty, NoteType = DocumentNoteType.Endnote });
                }
            }
            else if (element is W.Hyperlink hyperlink)
            {
                var href = hyperlink.Id is not null && _hyperlinks.TryGetValue(hyperlink.Id!, out var link)
                    ? link
                    : hyperlink.Anchor?.Value ?? string.Empty;
                var linkMarks = MergeMarks(inherited, [new InlineMark { Type = InlineMarkType.Link, Link = new LinkMarkData { Href = href } }]);
                result.AddRange(ReadInlines(hyperlink.ChildElements, linkMarks));
            }
            else if (element is W.InsertedRun inserted)
            {
                var revisionId = $"docx-ins-{inserted.Id?.Value ?? Guid.NewGuid().ToString("N")}";
                var marks = MergeMarks(inherited, [new InlineMark { Type = InlineMarkType.Revision, RevisionId = revisionId }]);
                result.AddRange(ReadInlines(inserted.ChildElements, marks));
            }
            else if (element is W.DeletedRun deleted)
            {
                var revisionId = $"docx-del-{deleted.Id?.Value ?? Guid.NewGuid().ToString("N")}";
                var marks = MergeMarks(inherited, [new InlineMark { Type = InlineMarkType.Revision, RevisionId = revisionId }]);
                result.AddRange(ReadInlines(deleted.ChildElements, marks));
            }
        }

        return result;
    }

    private static List<InlineMark> ReadRunMarks(W.RunProperties? properties)
    {
        var marks = new List<InlineMark>();
        if (properties is null)
        {
            return marks;
        }

        if (properties.Bold is not null) marks.Add(new InlineMark { Type = InlineMarkType.Bold });
        if (properties.Italic is not null) marks.Add(new InlineMark { Type = InlineMarkType.Italic });
        if (properties.Underline is not null) marks.Add(new InlineMark { Type = InlineMarkType.Underline });
        if (properties.Strike is not null) marks.Add(new InlineMark { Type = InlineMarkType.Strikethrough });
        if (properties.VerticalTextAlignment?.Val?.Value == W.VerticalPositionValues.Superscript) marks.Add(new InlineMark { Type = InlineMarkType.Superscript });
        if (properties.VerticalTextAlignment?.Val?.Value == W.VerticalPositionValues.Subscript) marks.Add(new InlineMark { Type = InlineMarkType.Subscript });
        if (!string.IsNullOrWhiteSpace(properties.Color?.Val?.Value)) marks.Add(new InlineMark { Type = InlineMarkType.TextColor, Value = $"#{properties.Color.Val.Value}" });
        if (properties.Highlight?.Val is not null) marks.Add(new InlineMark { Type = InlineMarkType.Highlight, Value = properties.Highlight.Val.Value.ToString() });
        return marks;
    }

    private static List<InlineMark> MergeMarks(IEnumerable<InlineMark> left, IEnumerable<InlineMark> right)
    {
        return left.Concat(right.Select(CloneMark)).Select(CloneMark).ToList();
    }

    private static InlineMark CloneMark(InlineMark mark)
    {
        return new InlineMark
        {
            Type = mark.Type,
            Value = mark.Value,
            RevisionId = mark.RevisionId,
            Link = mark.Link is null ? null : new LinkMarkData { Href = mark.Link.Href, Title = mark.Link.Title },
            CommentAnchor = mark.CommentAnchor is null ? null : new CommentAnchorMarkData { CommentId = mark.CommentAnchor.CommentId, AnchorId = mark.CommentAnchor.AnchorId }
        };
    }

    private DocumentBlock ReadTable(W.Table table)
    {
        var rows = new List<TableRowContent>();
        foreach (var row in table.Elements<W.TableRow>())
        {
            var cells = new List<TableCellContent>();
            foreach (var cell in row.Elements<W.TableCell>())
            {
                var properties = cell.TableCellProperties;
                var columnSpan = Math.Max(1, properties?.GridSpan?.Val?.Value ?? 1);
                var verticalMerge = properties?.VerticalMerge;
                var rowSpan = verticalMerge?.Val?.Value == W.MergedCellValues.Restart ? 2 : 1;
                var blocks = cell.Elements<W.Paragraph>()
                    .Select(p => new DocumentBlock
                    {
                        Type = DocumentBlockType.Paragraph,
                        Order = 0,
                        Content = new ParagraphBlockContent { Inlines = ReadInlines(p.ChildElements) }
                    })
                    .ToList();

                cells.Add(new TableCellContent
                {
                    ColumnSpan = columnSpan,
                    RowSpan = rowSpan,
                    Merge = new TableCellMerge { IsOrigin = verticalMerge?.Val?.Value != W.MergedCellValues.Continue },
                    Blocks = blocks.Count == 0 ? [DocumentModelText.Paragraph(string.Empty)] : blocks
                });
            }

            rows.Add(new TableRowContent { Cells = cells });
        }

        return new DocumentBlock
        {
            Type = DocumentBlockType.Table,
            Order = _order++,
            Content = new TableBlockContent { Rows = rows }
        };
    }

    private async Task<ImageBlockContent?> ReadImageAsync(W.Drawing drawing, MainDocumentPart mainPart, CancellationToken cancellationToken)
    {
        var blip = drawing.Descendants<A.Blip>().FirstOrDefault();
        var relationshipId = blip?.Embed?.Value;
        if (string.IsNullOrWhiteSpace(relationshipId))
        {
            return null;
        }

        if (mainPart.GetPartById(relationshipId) is not ImagePart imagePart)
        {
            return null;
        }

        await using var stream = imagePart.GetStream(FileMode.Open, FileAccess.Read);
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);
        var bytes = memory.ToArray();
        var fileName = Path.GetFileName(imagePart.Uri.ToString());
        var assetId = relationshipId;
        string? url = null;

        if (_options.ImageImporter is not null)
        {
            var imported = await _options.ImageImporter(new DocumentFormatImageImportRequest
            {
                SourcePath = imagePart.Uri.ToString(),
                ContentType = imagePart.ContentType,
                Content = bytes,
                FileName = fileName
            }, cancellationToken);
            assetId = imported.AssetId ?? assetId;
            url = imported.Url;
        }
        else
        {
            url = $"data:{imagePart.ContentType};base64,{Convert.ToBase64String(bytes)}";
        }

        return new ImageBlockContent
        {
            Source = url is not null ? DocumentImageSource.Url : DocumentImageSource.Asset,
            Url = url,
            AssetId = url is null ? assetId : null,
            AltText = drawing.Descendants<DocumentFormat.OpenXml.Drawing.Pictures.NonVisualDrawingProperties>().FirstOrDefault()?.Description?.Value,
            Size = new DocumentImageSize { Width = 120, Height = 90 },
            FloatingLayout = drawing.Descendants<DocumentFormat.OpenXml.Drawing.Wordprocessing.Anchor>().Any()
                ? new DocumentFloatingLayout { Inline = false, WrapMode = DocumentWrapMode.Square }
                : new DocumentFloatingLayout { Inline = true, WrapMode = DocumentWrapMode.Inline }
        };
    }

    private List<DocumentHeaderFooter> ReadHeadersFooters(MainDocumentPart mainPart)
    {
        var result = new List<DocumentHeaderFooter>();
        foreach (var part in mainPart.HeaderParts)
        {
            result.Add(new DocumentHeaderFooter
            {
                Type = DocumentHeaderFooterType.Header,
                Scope = DocumentHeaderFooterScope.Primary,
                Blocks = part.Header?.Elements<W.Paragraph>().Select((p, i) => new DocumentBlock
                {
                    Type = DocumentBlockType.Paragraph,
                    Order = i,
                    Content = new ParagraphBlockContent { Inlines = ReadInlines(p.ChildElements) }
                }).ToList() ?? []
            });
        }

        foreach (var part in mainPart.FooterParts)
        {
            result.Add(new DocumentHeaderFooter
            {
                Type = DocumentHeaderFooterType.Footer,
                Scope = DocumentHeaderFooterScope.Primary,
                Blocks = part.Footer?.Elements<W.Paragraph>().Select((p, i) => new DocumentBlock
                {
                    Type = DocumentBlockType.Paragraph,
                    Order = i,
                    Content = new ParagraphBlockContent { Inlines = ReadInlines(p.ChildElements) }
                }).ToList() ?? []
            });
        }

        return result;
    }

    private List<DocumentNote> ReadNotes(MainDocumentPart mainPart)
    {
        var result = new List<DocumentNote>();
        if (mainPart.FootnotesPart?.Footnotes is not null)
        {
            foreach (var footnote in mainPart.FootnotesPart.Footnotes.Elements<W.Footnote>().Where(f => f.Id?.Value > 0))
            {
                result.Add(new DocumentNote
                {
                    Id = footnote.Id?.Value.ToString() ?? Guid.NewGuid().ToString("N"),
                    Type = DocumentNoteType.Footnote,
                    Blocks = footnote.Elements<W.Paragraph>().Select((p, i) => new DocumentBlock
                    {
                        Type = DocumentBlockType.Paragraph,
                        Order = i,
                        Content = new ParagraphBlockContent { Inlines = ReadInlines(p.ChildElements) }
                    }).ToList()
                });
            }
        }

        if (mainPart.EndnotesPart?.Endnotes is not null)
        {
            foreach (var endnote in mainPart.EndnotesPart.Endnotes.Elements<W.Endnote>().Where(f => f.Id?.Value > 0))
            {
                result.Add(new DocumentNote
                {
                    Id = endnote.Id?.Value.ToString() ?? Guid.NewGuid().ToString("N"),
                    Type = DocumentNoteType.Endnote,
                    Blocks = endnote.Elements<W.Paragraph>().Select((p, i) => new DocumentBlock
                    {
                        Type = DocumentBlockType.Paragraph,
                        Order = i,
                        Content = new ParagraphBlockContent { Inlines = ReadInlines(p.ChildElements) }
                    }).ToList()
                });
            }
        }

        return result;
    }

    private List<DocumentComment> ReadComments(MainDocumentPart mainPart)
    {
        var comments = mainPart.WordprocessingCommentsPart?.Comments;
        if (comments is null)
        {
            return [];
        }

        return comments.Elements<W.Comment>().Select(comment => new DocumentComment
        {
            SourceFormat = "docx",
            ExternalId = comment.Id?.Value,
            Anchor = new DocumentCommentAnchor
            {
                Type = DocumentCommentAnchorType.ImportedDocx,
                ExternalAnchorId = comment.Id?.Value
            },
            Entries =
            [
                new DocumentCommentEntry
                {
                    Author = new DocumentEditorAuthor
                    {
                        DisplayName = comment.Author?.Value ?? string.Empty
                    },
                    Text = string.Join("\n", comment.Descendants<W.Text>().Select(t => t.Text)),
                    CreatedAt = comment.Date?.Value ?? DateTimeOffset.UtcNow
                }
            ]
        }).ToList();
    }

    private static List<DocumentRevision> ReadRevisions(W.Body body)
    {
        var revisions = new List<DocumentRevision>();
        revisions.AddRange(body.Descendants<W.InsertedRun>().Select(run => CreateRevision(DocumentRevisionType.Insertion, run.Id?.Value, run.Author?.Value, run.Date?.Value)));
        revisions.AddRange(body.Descendants<W.DeletedRun>().Select(run => CreateRevision(DocumentRevisionType.Deletion, run.Id?.Value, run.Author?.Value, run.Date?.Value)));
        revisions.AddRange(body.Descendants<W.RunPropertiesChange>().Select(change => CreateRevision(DocumentRevisionType.Formatting, change.Id?.Value, change.Author?.Value, change.Date?.Value)));
        return revisions;
    }

    private static DocumentRevision CreateRevision(DocumentRevisionType type, string? id, string? author, DateTime? date)
    {
        return new DocumentRevision
        {
            Id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : $"docx-rev-{id}",
            Type = type,
            Author = new DocumentRevisionAuthor { DisplayName = author ?? string.Empty },
            CreatedAt = date is null ? DateTimeOffset.UtcNow : new DateTimeOffset(DateTime.SpecifyKind(date.Value, DateTimeKind.Utc))
        };
    }

    private void ReadSectionProperties(DocumentEditorDocument doc, W.Body body)
    {
        var section = doc.Sections.FirstOrDefault() ?? new DocumentSection { Order = 0 };
        var sectionProperties = body.Elements<W.SectionProperties>().LastOrDefault();
        if (sectionProperties is null)
        {
            return;
        }

        var size = sectionProperties.GetFirstChild<W.PageSize>();
        if (size?.Width is not null && size.Height is not null)
        {
            section.Properties.PageSettings.Size = new DocumentPageSize
            {
                Name = "DOCX",
                Width = TwipsToPoints(size.Width),
                Height = TwipsToPoints(size.Height)
            };
            section.Properties.PageSettings.Landscape = size.Orient?.Value == W.PageOrientationValues.Landscape;
        }

        var margin = sectionProperties.GetFirstChild<W.PageMargin>();
        if (margin is not null)
        {
            section.Properties.PageSettings.Margins = new DocumentPageMargins
            {
                Top = TwipsToPoints(margin.Top?.Value ?? 1440),
                Right = TwipsToPoints(margin.Right?.Value ?? 1440),
                Bottom = TwipsToPoints(margin.Bottom?.Value ?? 1440),
                Left = TwipsToPoints(margin.Left?.Value ?? 1440)
            };
        }
    }

    private static double TwipsToPoints(double twips) => twips / 20d;

    private void PreserveUnsupportedParts()
    {
        var parts = _document.Parts.Select(part => part.OpenXmlPart).ToList();
        for (var i = 0; i < parts.Count; i++)
        {
            parts.AddRange(parts[i].Parts.Select(part => part.OpenXmlPart));
        }

        _preservedParts.AddRange(parts
            .Where(part => !part.Uri.ToString().EndsWith("/document.xml", StringComparison.OrdinalIgnoreCase))
            .Where(part => !part.Uri.ToString().Contains("/media/", StringComparison.OrdinalIgnoreCase))
            .Select(part =>
            {
                using var stream = part.GetStream(FileMode.Open, FileAccess.Read);
                using var memory = new MemoryStream();
                stream.CopyTo(memory);
                return new DocumentFormatPreservedPart
                {
                    Path = part.Uri.ToString(),
                    ContentType = part.ContentType,
                    Content = memory.ToArray()
                };
            }));
    }

    private static DocumentFormatCompatibilityWarning Warning(string code, string message, DocumentFormatCompatibilitySeverity severity, string? path = null)
    {
        return new DocumentFormatCompatibilityWarning
        {
            Code = code,
            Message = message,
            Severity = severity,
            SourcePath = path
        };
    }
}
