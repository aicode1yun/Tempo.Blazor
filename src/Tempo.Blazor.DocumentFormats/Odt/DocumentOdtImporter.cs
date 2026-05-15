using System.IO.Compression;
using System.Globalization;
using System.Xml.Linq;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentFormats.Internal;

namespace Tempo.Blazor.DocumentFormats.Odt;

/// <summary>Imports ODT packages into the document editor JSON model.</summary>
public sealed class DocumentOdtImporter : IDocumentFormatImporter
{
    /// <inheritdoc />
    public async Task<DocumentFormatImportResult> ImportAsync(Stream stream, DocumentFormatImportOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new DocumentFormatImportOptions();
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);
        memory.Position = 0;
        using var archive = new ZipArchive(memory, ZipArchiveMode.Read, leaveOpen: false);
        var reader = new OdtPackageReader(archive, options);
        return await reader.ReadAsync(cancellationToken);
    }
}

/// <summary>Reads OpenDocument package XML into the editor model.</summary>
public sealed class OdtPackageReader
{
    private static readonly XNamespace Office = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";
    private static readonly XNamespace Text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
    private static readonly XNamespace Table = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";
    private static readonly XNamespace Draw = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0";
    private static readonly XNamespace Svg = "urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0";
    private static readonly XNamespace Tm = "urn:tempo-blazor:document-editor:1.0";
    private static readonly XNamespace XLink = "http://www.w3.org/1999/xlink";

    private readonly ZipArchive _archive;
    private readonly DocumentFormatImportOptions _options;
    private readonly List<DocumentFormatCompatibilityWarning> _warnings = [];
    private readonly List<DocumentFormatPreservedPart> _preservedParts = [];
    private int _order;

    /// <summary>Creates an ODT package reader.</summary>
    public OdtPackageReader(ZipArchive archive, DocumentFormatImportOptions options)
    {
        _archive = archive;
        _options = options;
    }

    /// <summary>Reads the package.</summary>
    public async Task<DocumentFormatImportResult> ReadAsync(CancellationToken cancellationToken = default)
    {
        var doc = DocumentEditorDocument.Empty(_options.DocumentId);
        doc.Metadata.Title = string.IsNullOrWhiteSpace(_options.FileName)
            ? "Imported ODT"
            : Path.GetFileNameWithoutExtension(_options.FileName);

        var content = await ReadXmlAsync("content.xml", cancellationToken);
        if (content is null)
        {
            _warnings.Add(new DocumentFormatCompatibilityWarning
            {
                Code = "odt.missing-content",
                Message = "ODT package does not contain content.xml.",
                Severity = DocumentFormatCompatibilitySeverity.Dropped,
                SourcePath = "content.xml"
            });
            return Result(doc);
        }

        var textRoot = content.Descendants(Office + "text").FirstOrDefault();
        if (textRoot is not null)
        {
            foreach (var element in textRoot.Elements())
            {
                cancellationToken.ThrowIfCancellationRequested();
                await ReadBodyElementAsync(doc, element, cancellationToken);
            }
        }

        PreserveUnsupportedEntries();
        if (doc.Blocks.Count == 0)
        {
            doc.Blocks.Add(DocumentModelText.Paragraph(string.Empty));
        }

        var firstHeading = doc.Blocks.Select(block => block.Content).OfType<HeadingBlockContent>().FirstOrDefault();
        if (firstHeading is not null)
        {
            var title = DocumentModelText.GetInlineText(firstHeading.Inlines);
            if (!string.IsNullOrWhiteSpace(title))
            {
                doc.Metadata.Title = title;
            }
        }

        return Result(doc);
    }

    private DocumentFormatImportResult Result(DocumentEditorDocument doc)
    {
        return new DocumentFormatImportResult
        {
            Document = doc,
            Format = DocumentFormatKind.Odt,
            Warnings = _warnings,
            PreservedParts = _preservedParts
        };
    }

    private async Task ReadBodyElementAsync(DocumentEditorDocument doc, XElement element, CancellationToken cancellationToken)
    {
        if (element.Name == Text + "p")
        {
            if (string.Equals((string?)element.Attribute(Text + "style-name"), "page-break", StringComparison.OrdinalIgnoreCase))
            {
                doc.Blocks.Add(new DocumentBlock
                {
                    Type = DocumentBlockType.PageBreak,
                    Order = _order++,
                    Content = new PageBreakBlockContent()
                });
                return;
            }

            var hasFrames = element.Descendants(Draw + "frame").Any();
            foreach (var frame in element.Descendants(Draw + "frame"))
            {
                var image = await ReadImageAsync(frame, cancellationToken);
                if (image is not null)
                {
                    doc.Blocks.Add(new DocumentBlock
                    {
                        Type = DocumentBlockType.Image,
                        Order = _order++,
                        Content = image
                    });
                }
            }

            var inlines = await ReadInlinesAsync(element.Nodes(), cancellationToken);
            if (inlines.Count > 0 || !hasFrames)
            {
                doc.Blocks.Add(new DocumentBlock
                {
                    Type = DocumentBlockType.Paragraph,
                    Order = _order++,
                    Content = new ParagraphBlockContent { Inlines = inlines }
                });
            }
        }
        else if (element.Name == Text + "h")
        {
            var level = (int?)element.Attribute(Text + "outline-level") ?? 1;
            doc.Blocks.Add(new DocumentBlock
            {
                Type = DocumentBlockType.Heading,
                Order = _order++,
                Content = new HeadingBlockContent { Level = Math.Clamp(level, 1, 6), Inlines = await ReadInlinesAsync(element.Nodes(), cancellationToken) }
            });
        }
        else if (element.Name == Text + "list")
        {
            foreach (var item in element.Elements(Text + "list-item"))
            {
                var paragraph = item.Elements(Text + "p").FirstOrDefault();
                doc.Blocks.Add(new DocumentBlock
                {
                    Type = DocumentBlockType.List,
                    Order = _order++,
                    Content = new ListBlockContent
                    {
                        Ordered = ((string?)element.Attribute(Text + "style-name"))?.Contains("number", StringComparison.OrdinalIgnoreCase) == true,
                        Inlines = paragraph is null ? [] : await ReadInlinesAsync(paragraph.Nodes(), cancellationToken)
                    }
                });
            }
        }
        else if (element.Name == Table + "table")
        {
            doc.Blocks.Add(await ReadTableAsync(element, cancellationToken));
        }
        else if (element.Name == Text + "section")
        {
            var section = new DocumentSection
            {
                Order = doc.Sections.Count,
                Title = (string?)element.Attribute(Text + "name")
            };
            doc.Sections.Add(section);
            foreach (var child in element.Elements())
            {
                await ReadBodyElementAsync(doc, child, cancellationToken);
            }
        }
        else if (element.Name == Tm + "header-footer")
        {
            doc.HeadersFooters.Add(await ReadHeaderFooterAsync(element, cancellationToken));
        }
        else if (element.Name == Tm + "comment")
        {
            doc.Comments.Add(ReadComment(element));
        }
    }

    private async Task<DocumentBlock> ReadTableAsync(XElement table, CancellationToken cancellationToken)
    {
        var rows = new List<TableRowContent>();
        foreach (var row in table.Elements(Table + "table-row"))
        {
            var cells = new List<TableCellContent>();
            foreach (var cell in row.Elements().Where(e => e.Name == Table + "table-cell" || e.Name == Table + "covered-table-cell"))
            {
                var columnSpan = (int?)cell.Attribute(Table + "number-columns-spanned") ?? 1;
                var rowSpan = (int?)cell.Attribute(Table + "number-rows-spanned") ?? 1;
                var blocks = new List<DocumentBlock>();
                foreach (var paragraph in cell.Elements(Text + "p"))
                {
                    blocks.Add(new DocumentBlock
                    {
                        Type = DocumentBlockType.Paragraph,
                        Content = new ParagraphBlockContent { Inlines = await ReadInlinesAsync(paragraph.Nodes(), cancellationToken) }
                    });
                }

                cells.Add(new TableCellContent
                {
                    ColumnSpan = Math.Max(1, columnSpan),
                    RowSpan = Math.Max(1, rowSpan),
                    Merge = new TableCellMerge
                    {
                        IsOrigin = cell.Name != Table + "covered-table-cell",
                        OriginCellId = (string?)cell.Attribute(Tm + "origin-cell-id")
                    },
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

    private async Task<List<InlineContent>> ReadInlinesAsync(IEnumerable<XNode> nodes, CancellationToken cancellationToken, List<InlineMark>? inheritedMarks = null)
    {
        var result = new List<InlineContent>();
        var marks = inheritedMarks ?? [];

        foreach (var node in nodes)
        {
            if (node is XText text)
            {
                result.Add(new TextRun { Text = text.Value, Marks = marks.Select(CloneMark).ToList() });
            }
            else if (node is XElement element)
            {
                if (element.Name == Text + "span")
                {
                    var spanMarks = MarksFromStyle((string?)element.Attribute(Text + "style-name"));
                    var commentId = (string?)element.Attribute(Tm + "comment-id");
                    if (!string.IsNullOrWhiteSpace(commentId))
                    {
                        spanMarks.Add(new InlineMark
                        {
                            Type = InlineMarkType.CommentAnchor,
                            CommentAnchor = new CommentAnchorMarkData { CommentId = commentId, AnchorId = commentId }
                        });
                    }

                    result.AddRange(await ReadInlinesAsync(element.Nodes(), cancellationToken, MergeMarks(marks, spanMarks)));
                }
                else if (element.Name == Text + "a")
                {
                    var href = (string?)element.Attribute(XLink + "href") ?? string.Empty;
                    result.AddRange(await ReadInlinesAsync(element.Nodes(), cancellationToken, MergeMarks(marks, [new InlineMark { Type = InlineMarkType.Link, Link = new LinkMarkData { Href = href } }])));
                }
                else if (element.Name == Text + "line-break")
                {
                    result.Add(new TextRun { Text = "\n", Marks = marks.Select(CloneMark).ToList() });
                }
                else if (element.Name == Draw + "frame")
                {
                    continue;
                }
                else if (element.Name == Office + "annotation")
                {
                    var commentText = string.Join("\n", element.Descendants(Text + "p").Select(p => p.Value));
                    result.Add(new TextRun
                    {
                        Text = string.Empty,
                        Marks = [new InlineMark { Type = InlineMarkType.CommentAnchor, CommentAnchor = new CommentAnchorMarkData { CommentId = $"odt-{Guid.NewGuid():N}" } }]
                    });
                    _warnings.Add(new DocumentFormatCompatibilityWarning
                    {
                        Code = "odt.annotation",
                        Message = $"Imported ODT annotation: {commentText}",
                        Severity = DocumentFormatCompatibilitySeverity.Info,
                        SourcePath = "content.xml"
                    });
                }
            }
        }

        return result;
    }

    private async Task<ImageBlockContent?> ReadImageAsync(XElement frame, CancellationToken cancellationToken)
    {
        var image = frame.Element(Draw + "image");
        var href = (string?)image?.Attribute(XLink + "href");
        if (string.IsNullOrWhiteSpace(href))
        {
            return null;
        }

        var entry = _archive.GetEntry(href);
        string? url = null;
        string? assetId = href;
        if (entry is not null)
        {
            await using var stream = entry.Open();
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory, cancellationToken);
            var contentType = href.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || href.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
                ? "image/jpeg"
                : "image/png";
            if (_options.ImageImporter is not null)
            {
                var imported = await _options.ImageImporter(new DocumentFormatImageImportRequest
                {
                    SourcePath = href,
                    ContentType = contentType,
                    Content = memory.ToArray(),
                    FileName = Path.GetFileName(href)
                }, cancellationToken);
                url = imported.Url;
                assetId = imported.AssetId ?? assetId;
            }
            else
            {
                url = $"data:{contentType};base64,{Convert.ToBase64String(memory.ToArray())}";
            }
        }

        var floatingLayout = ReadFloatingLayout(frame);

        return new ImageBlockContent
        {
            Source = url is null ? DocumentImageSource.Asset : DocumentImageSource.Url,
            Url = url,
            AssetId = url is null ? assetId : null,
            AltText = (string?)frame.Attribute(Draw + "name"),
            Size = new DocumentImageSize
            {
                Width = ParseLength((string?)frame.Attribute(Svg + "width")),
                Height = ParseLength((string?)frame.Attribute(Svg + "height"))
            },
            FloatingLayout = floatingLayout
        };
    }

    private async Task<DocumentHeaderFooter> ReadHeaderFooterAsync(XElement element, CancellationToken cancellationToken)
    {
        var blocks = new List<DocumentBlock>();
        var order = 0;
        foreach (var paragraph in element.Elements(Text + "p"))
        {
            blocks.Add(new DocumentBlock
            {
                Type = DocumentBlockType.Paragraph,
                Order = order++,
                Content = new ParagraphBlockContent { Inlines = await ReadInlinesAsync(paragraph.Nodes(), cancellationToken) }
            });
        }

        return new DocumentHeaderFooter
        {
            Id = (string?)element.Attribute(Tm + "id") ?? Guid.NewGuid().ToString("N"),
            Type = ParseEnum((string?)element.Attribute(Tm + "type"), DocumentHeaderFooterType.Header),
            Scope = ParseEnum((string?)element.Attribute(Tm + "scope"), DocumentHeaderFooterScope.Primary),
            Blocks = blocks
        };
    }

    private static DocumentComment ReadComment(XElement element)
    {
        return new DocumentComment
        {
            Id = (string?)element.Attribute(Tm + "id") ?? Guid.NewGuid().ToString("N"),
            SourceFormat = (string?)element.Attribute(Tm + "source-format") ?? "odt",
            ExternalId = (string?)element.Attribute(Tm + "external-id"),
            Anchor = new DocumentCommentAnchor
            {
                Type = DocumentCommentAnchorType.ImportedOdt,
                ExternalAnchorId = (string?)element.Attribute(Tm + "id")
            },
            Entries = element.Elements(Tm + "entry").Select(entry => new DocumentCommentEntry
            {
                Author = new DocumentEditorAuthor { DisplayName = (string?)entry.Attribute(Tm + "author") ?? string.Empty },
                CreatedAt = DateTimeOffset.TryParse((string?)entry.Attribute(Tm + "created-at"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var createdAt)
                    ? createdAt
                    : DateTimeOffset.UtcNow,
                Text = entry.Value
            }).ToList()
        };
    }

    private static DocumentFloatingLayout ReadFloatingLayout(XElement frame)
    {
        var floating = ((string?)frame.Attribute(Text + "anchor-type")) == "page";
        if (!floating)
        {
            return new DocumentFloatingLayout { Inline = true, WrapMode = DocumentWrapMode.Inline };
        }

        return new DocumentFloatingLayout
        {
            Inline = false,
            HorizontalRelativeTo = ParseEnum((string?)frame.Attribute(Tm + "horizontal-relative-to"), DocumentRelativePosition.Page),
            VerticalRelativeTo = ParseEnum((string?)frame.Attribute(Tm + "vertical-relative-to"), DocumentRelativePosition.Paragraph),
            X = ParseLength((string?)frame.Attribute(Svg + "x")) ?? 0,
            Y = ParseLength((string?)frame.Attribute(Svg + "y")) ?? 0,
            WrapMode = ParseEnum((string?)frame.Attribute(Tm + "wrap-mode"), DocumentWrapMode.Square),
            ZIndex = int.TryParse((string?)frame.Attribute(Draw + "z-index"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var zIndex) ? zIndex : 0,
            LockAnchor = string.Equals((string?)frame.Attribute(Tm + "lock-anchor"), "true", StringComparison.OrdinalIgnoreCase)
        };
    }

    private static double? ParseLength(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var numeric = value.Trim();
        if (numeric.EndsWith("pt", StringComparison.OrdinalIgnoreCase)
            || numeric.EndsWith("px", StringComparison.OrdinalIgnoreCase)
            || numeric.EndsWith("cm", StringComparison.OrdinalIgnoreCase))
        {
            numeric = numeric[..^2];
        }

        return double.TryParse(numeric, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback)
        where TEnum : struct
    {
        return Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) ? parsed : fallback;
    }

    private static List<InlineMark> MarksFromStyle(string? styleName)
    {
        var marks = new List<InlineMark>();
        if (string.IsNullOrWhiteSpace(styleName))
        {
            return marks;
        }

        if (styleName.Contains("bold", StringComparison.OrdinalIgnoreCase)) marks.Add(new InlineMark { Type = InlineMarkType.Bold });
        if (styleName.Contains("italic", StringComparison.OrdinalIgnoreCase)) marks.Add(new InlineMark { Type = InlineMarkType.Italic });
        if (styleName.Contains("underline", StringComparison.OrdinalIgnoreCase)) marks.Add(new InlineMark { Type = InlineMarkType.Underline });
        if (styleName.Contains("strike", StringComparison.OrdinalIgnoreCase)) marks.Add(new InlineMark { Type = InlineMarkType.Strikethrough });
        return marks;
    }

    private static List<InlineMark> MergeMarks(IEnumerable<InlineMark> left, IEnumerable<InlineMark> right)
    {
        return left.Concat(right).Select(CloneMark).ToList();
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

    private async Task<XDocument?> ReadXmlAsync(string path, CancellationToken cancellationToken)
    {
        var entry = _archive.GetEntry(path);
        if (entry is null)
        {
            return null;
        }

        await using var stream = entry.Open();
        return await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken);
    }

    private void PreserveUnsupportedEntries()
    {
        foreach (var entry in _archive.Entries.Where(entry => entry.FullName != "content.xml" && entry.FullName != "mimetype" && !entry.FullName.StartsWith("Pictures/", StringComparison.OrdinalIgnoreCase)))
        {
            using var stream = entry.Open();
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            _preservedParts.Add(new DocumentFormatPreservedPart
            {
                Path = entry.FullName,
                Content = memory.ToArray()
            });
        }
    }
}
