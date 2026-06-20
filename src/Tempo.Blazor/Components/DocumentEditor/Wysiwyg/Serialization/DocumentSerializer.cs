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
        DocumentEditorJson.Serialize(ToPersistenceModel(document));

    /// <summary>Deserialize legacy JSON string to WYSIWYG model.</summary>
    public Wyg.DocumentModel Deserialize(string json) =>
        FromPersistenceModel(DocumentEditorJson.Deserialize(json));

    private static DocumentBlock ToPersistenceBlock(Wyg.Block block)
    {
        return block switch
        {
            Wyg.ParagraphBlock p => new DocumentBlock
            {
                Id = p.Id,
                Type = DocumentBlockType.Paragraph,
                ParagraphProperties = ToPersistenceParagraphProperties(p.Properties),
                Content = ToPersistenceParagraphContent(p)
            },
            Wyg.HeadingBlock h => new DocumentBlock
            {
                Id = h.Id,
                Type = DocumentBlockType.Heading,
                ParagraphProperties = ToPersistenceParagraphProperties(h.Properties),
                Content = new HeadingBlockContent
                {
                    Level = h.Level,
                    Inlines = h.Inlines.Select(ToPersistenceInline).ToList()
                }
            },
            Wyg.ListItemBlock l => new DocumentBlock
            {
                Id = l.Id,
                Type = DocumentBlockType.List,
                ParagraphProperties = ToPersistenceParagraphProperties(l.Properties),
                Content = new ListBlockContent
                {
                    Ordered = l.Ordered,
                    IndentLevel = l.IndentLevel,
                    Inlines = l.Inlines.Select(ToPersistenceInline).ToList()
                }
            },
            Wyg.TableBlock t => new DocumentBlock
            {
                Id = t.Id,
                Type = DocumentBlockType.Table,
                ParagraphProperties = ToPersistenceParagraphProperties(t.Properties),
                Content = ToPersistenceTableContent(t)
            },
            Wyg.ImageBlock i => new DocumentBlock
            {
                Id = i.Id,
                Type = DocumentBlockType.Paragraph,
                ParagraphProperties = ToPersistenceParagraphProperties(i.Properties),
                Content = new ParagraphBlockContent
                {
                    Inlines = [ToPersistenceDrawingRun(i)]
                }
            },
            Wyg.PageBreakBlock pageBreak => new DocumentBlock
            {
                Id = pageBreak.Id,
                Type = DocumentBlockType.PageBreak,
                ParagraphProperties = ToPersistenceParagraphProperties(pageBreak.Properties),
                Content = new PageBreakBlockContent()
            },
            Wyg.SectionBreakBlock sectionBreak => new DocumentBlock
            {
                Id = sectionBreak.Id,
                Type = DocumentBlockType.PageBreak,
                ParagraphProperties = ToPersistenceParagraphProperties(sectionBreak.Properties),
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
            Layout = new TableLayoutContent
            {
                Width = ParseNullableDouble(table.TableProperties?.Width),
                Alignment = table.TableProperties?.Alignment ?? TableHorizontalAlignment.Left,
                CellPadding = table.TableProperties?.CellPadding,
                BackgroundColor = table.TableProperties?.BackgroundColor,
                Borders = table.TableProperties?.Borders ?? new TableCellBorders()
            },
            Rows = table.Rows.Select(r => new TableRowContent
            {
                Cells = r.Cells.Select(c => new TableCellContent
                {
                    Id = c.Id,
                    Blocks = c.Blocks.Select(ToPersistenceBlock).ToList(),
                    ColumnSpan = c.ColumnSpan,
                    RowSpan = c.RowSpan,
                    Width = c.Width,
                    BackgroundColor = c.BackgroundColor,
                    Borders = c.Borders,
                    VerticalAlignment = c.VerticalAlignment,
                    Padding = c.Padding
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
                Id = t.Id,
                Text = t.Text,
                Marks = t.Marks.SelectMany(ToPersistenceMarks).ToList()
            },
            Wyg.HardBreak hardBreak => new TextRun { Id = hardBreak.Id, Text = "\n" },
            Wyg.TabInline tab => new TextRun { Id = tab.Id, Text = "\t" },
            Wyg.DrawingInline drawing => ToPersistenceDrawingRun(drawing),
            _ => new TextRun { Text = string.Empty }
        };
    }

    private static IEnumerable<InlineMark> ToPersistenceMarks(Wyg.Mark mark)
    {
        switch (mark)
        {
            case Wyg.BoldMark:
                yield return new InlineMark { Type = InlineMarkType.Bold };
                break;
            case Wyg.ItalicMark:
                yield return new InlineMark { Type = InlineMarkType.Italic };
                break;
            case Wyg.UnderlineMark:
                yield return new InlineMark { Type = InlineMarkType.Underline };
                break;
            case Wyg.StrikethroughMark:
                yield return new InlineMark { Type = InlineMarkType.Strikethrough };
                break;
            case Wyg.SubscriptMark:
                yield return new InlineMark { Type = InlineMarkType.Subscript };
                break;
            case Wyg.SuperscriptMark:
                yield return new InlineMark { Type = InlineMarkType.Superscript };
                break;
            case Wyg.FontMark font:
                if (!string.IsNullOrWhiteSpace(font.Family))
                {
                    yield return new InlineMark { Type = InlineMarkType.FontFamily, Value = font.Family };
                }

                if (!string.IsNullOrWhiteSpace(font.Size))
                {
                    yield return new InlineMark { Type = InlineMarkType.FontSize, Value = font.Size };
                }
                break;
            case Wyg.ColorMark color when !string.IsNullOrWhiteSpace(color.Color):
                yield return new InlineMark { Type = InlineMarkType.TextColor, Value = color.Color };
                break;
            case Wyg.HighlightMark highlight when !string.IsNullOrWhiteSpace(highlight.Color):
                yield return new InlineMark { Type = InlineMarkType.Highlight, Value = highlight.Color };
                break;
            case Wyg.LinkMark link:
                yield return new InlineMark
                {
                    Type = InlineMarkType.Link,
                    Link = new LinkMarkData { Href = link.Href, Title = link.Title }
                };
                break;
        }
    }

    private static Wyg.Block FromPersistenceBlock(DocumentBlock block)
    {
        return block.Type switch
        {
            DocumentBlockType.Paragraph => new Wyg.ParagraphBlock
            {
                Id = block.Id,
                Properties = FromPersistenceParagraphProperties(block.ParagraphProperties),
                Inlines = block.Content is ParagraphBlockContent pc
                    ? pc.Inlines.Select(FromPersistenceInline).ToList()
                    : []
            },
            DocumentBlockType.Heading => new Wyg.HeadingBlock
            {
                Id = block.Id,
                Properties = FromPersistenceParagraphProperties(block.ParagraphProperties),
                Level = block.Content is HeadingBlockContent hc ? hc.Level : 1,
                Inlines = block.Content is HeadingBlockContent hc2
                    ? hc2.Inlines.Select(FromPersistenceInline).ToList()
                    : []
            },
            DocumentBlockType.List => new Wyg.ListItemBlock
            {
                Id = block.Id,
                Properties = FromPersistenceParagraphProperties(block.ParagraphProperties),
                Ordered = block.Content is ListBlockContent lc ? lc.Ordered : false,
                IndentLevel = block.Content is ListBlockContent lc2 ? lc2.IndentLevel : 0,
                Inlines = block.Content is ListBlockContent lc3
                    ? lc3.Inlines.Select(FromPersistenceInline).ToList()
                    : []
            },
            DocumentBlockType.Table => FromPersistenceTableBlock(block),
            DocumentBlockType.Image => FromPersistenceImageBlock(block),
            DocumentBlockType.PageBreak => new Wyg.PageBreakBlock
            {
                Id = block.Id,
                Properties = FromPersistenceParagraphProperties(block.ParagraphProperties)
            },
            _ => new Wyg.ParagraphBlock()
        };
    }

    private static DocumentDrawingRun ToPersistenceDrawingRun(Wyg.ImageBlock image)
    {
        var width = CssLengthToPoints(image.Size.Width);
        var height = CssLengthToPoints(image.Size.Height);

        return new DocumentDrawingRun
        {
            Id = image.Id,
            ObjectId = image.Id,
            Kind = DocumentDrawingKind.Image,
            Source = DocumentImageSource.Url,
            Url = ToPersistentImageUrl(DocumentImageSource.Url, image.Src),
            AltText = image.Alt,
            IsDecorative = image.IsDecorative,
            Size = width is null && height is null
                ? new DocumentImageSize()
                : new DocumentImageSize
                {
                    Width = width,
                    Height = height
                },
            Layout = ToPersistenceImageLayout(image, width, height)
        };
    }

    private static DocumentDrawingRun ToPersistenceDrawingRun(Wyg.DrawingInline drawing)
    {
        return new DocumentDrawingRun
        {
            Id = drawing.Id,
            ObjectId = drawing.ObjectId,
            Kind = drawing.Kind,
            Source = drawing.Source,
            Url = ToPersistentImageUrl(drawing.Source, drawing.Url),
            AssetId = drawing.Source == DocumentImageSource.Asset ? drawing.AssetId : null,
            AltText = drawing.AltText,
            IsDecorative = drawing.IsDecorative,
            Caption = drawing.Caption,
            Size = CloneImageSize(drawing.Size),
            NaturalSize = CloneImageSize(drawing.NaturalSize),
            Layout = CloneLayout(drawing.Layout),
            LinkUrl = drawing.LinkUrl,
            Metadata = drawing.Metadata.ToDictionary(pair => pair.Key, pair => pair.Value)
        };
    }

    private static DocumentObjectLayout ToPersistenceImageLayout(Wyg.ImageBlock image, double? width, double? height)
    {
        var isInline = image.Layout == Wyg.ImageLayout.Inline;
        return new DocumentObjectLayout
        {
            Kind = isInline ? DocumentObjectLayoutKind.Inline : DocumentObjectLayoutKind.Anchored,
            Anchor = new DocumentObjectAnchor
            {
                MoveWithText = true,
                FixedOnPage = false
            },
            Position = new DocumentObjectPosition
            {
                HorizontalRelativeTo = DocumentRelativePosition.Page,
                VerticalRelativeTo = DocumentRelativePosition.Paragraph,
                X = isInline ? 0 : CssLengthToPoints(image.Position?.X) ?? 0,
                Y = isInline ? 0 : CssLengthToPoints(image.Position?.Y) ?? 0
            },
            Wrap = new DocumentObjectWrap
            {
                Mode = isInline ? DocumentWrapMode.Inline : ToPersistenceImageWrapMode(image.WrapMode)
            },
            Transform = new DocumentObjectTransform
            {
                Width = width,
                Height = height,
                LockAspectRatio = true
            }
        };
    }

    private static Wyg.ImageBlock FromPersistenceImageBlock(DocumentBlock block)
    {
        var content = block.Content as ImageBlockContent;
        var layout = content?.Layout ?? DocumentObjectLayout.Inline();
        return new Wyg.ImageBlock
        {
            Id = block.Id,
            Properties = FromPersistenceParagraphProperties(block.ParagraphProperties),
            Src = content?.Url ?? content?.AssetId ?? string.Empty,
            Alt = content?.AltText ?? string.Empty,
            IsDecorative = content?.IsDecorative ?? false,
            Size = new Wyg.ImageSize
            {
                Width = ToCssNumber(layout.Transform.Width ?? content?.Size?.Width),
                Height = ToCssNumber(layout.Transform.Height ?? content?.Size?.Height)
            },
            Layout = layout.IsInline ? Wyg.ImageLayout.Inline : Wyg.ImageLayout.Floating,
            Position = layout.IsInline
                ? null
                : new Wyg.ImagePosition
                {
                    X = ToCssNumber(layout.Position.X) ?? "0",
                    Y = ToCssNumber(layout.Position.Y) ?? "0"
                },
            WrapMode = FromPersistenceImageWrapMode(layout.Wrap.Mode)
        };
    }

    private static DocumentWrapMode ToPersistenceImageWrapMode(Wyg.ImageWrapMode mode)
        => mode switch
        {
            Wyg.ImageWrapMode.Tight => DocumentWrapMode.Tight,
            Wyg.ImageWrapMode.Through => DocumentWrapMode.Through,
            Wyg.ImageWrapMode.TopAndBottom => DocumentWrapMode.TopBottom,
            Wyg.ImageWrapMode.BehindText => DocumentWrapMode.BehindText,
            Wyg.ImageWrapMode.InFrontOfText => DocumentWrapMode.InFrontOfText,
            _ => DocumentWrapMode.Square
        };

    private static Wyg.ImageWrapMode FromPersistenceImageWrapMode(DocumentWrapMode mode)
        => mode switch
        {
            DocumentWrapMode.Tight => Wyg.ImageWrapMode.Tight,
            DocumentWrapMode.Through => Wyg.ImageWrapMode.Through,
            DocumentWrapMode.TopBottom => Wyg.ImageWrapMode.TopAndBottom,
            DocumentWrapMode.BehindText => Wyg.ImageWrapMode.BehindText,
            DocumentWrapMode.InFrontOfText => Wyg.ImageWrapMode.InFrontOfText,
            _ => Wyg.ImageWrapMode.Square
        };

    private static string? ToPersistentImageUrl(DocumentImageSource source, string? url)
        => source == DocumentImageSource.Url && DocumentImagePersistence.IsSafePersistentImageUrl(url)
            ? url
            : null;

    private static DocumentImageSize CloneImageSize(DocumentImageSize? size)
        => size is null
            ? new DocumentImageSize()
            : new DocumentImageSize
            {
                Width = size.Width,
                Height = size.Height,
                LockAspectRatio = size.LockAspectRatio
            };

    private static DocumentObjectLayout CloneLayout(DocumentObjectLayout? layout)
    {
        if (layout is null)
        {
            return DocumentObjectLayout.Inline();
        }

        return new DocumentObjectLayout
        {
            Kind = layout.Kind,
            Anchor = new DocumentObjectAnchor
            {
                BlockId = layout.Anchor.BlockId,
                InlineIndex = layout.Anchor.InlineIndex,
                Offset = layout.Anchor.Offset,
                Region = layout.Anchor.Region,
                TableId = layout.Anchor.TableId,
                CellId = layout.Anchor.CellId,
                HeaderFooterId = layout.Anchor.HeaderFooterId,
                MoveWithText = layout.Anchor.MoveWithText,
                FixedOnPage = layout.Anchor.FixedOnPage,
                LockAnchor = layout.Anchor.LockAnchor
            },
            Position = new DocumentObjectPosition
            {
                HorizontalRelativeTo = layout.Position.HorizontalRelativeTo,
                VerticalRelativeTo = layout.Position.VerticalRelativeTo,
                X = layout.Position.X,
                Y = layout.Position.Y,
                HorizontalAlignment = layout.Position.HorizontalAlignment,
                VerticalAlignment = layout.Position.VerticalAlignment
            },
            Wrap = new DocumentObjectWrap
            {
                Mode = layout.Wrap.Mode,
                DistanceLeft = layout.Wrap.DistanceLeft,
                DistanceRight = layout.Wrap.DistanceRight,
                DistanceTop = layout.Wrap.DistanceTop,
                DistanceBottom = layout.Wrap.DistanceBottom,
                WrapContourPoints = layout.Wrap.WrapContourPoints
                    .Select(point => new DocumentObjectWrapPoint { X = point.X, Y = point.Y })
                    .ToList()
            },
            Transform = new DocumentObjectTransform
            {
                Width = layout.Transform.Width,
                Height = layout.Transform.Height,
                NaturalWidth = layout.Transform.NaturalWidth,
                NaturalHeight = layout.Transform.NaturalHeight,
                LockAspectRatio = layout.Transform.LockAspectRatio,
                Rotation = layout.Transform.Rotation,
                Crop = new DocumentObjectCrop
                {
                    Left = layout.Transform.Crop.Left,
                    Top = layout.Transform.Crop.Top,
                    Right = layout.Transform.Crop.Right,
                    Bottom = layout.Transform.Crop.Bottom
                }
            },
            Stacking = new DocumentObjectStacking
            {
                ZIndex = layout.Stacking.ZIndex,
                AllowOverlap = layout.Stacking.AllowOverlap
            }
        };
    }

    private static Wyg.TableBlock FromPersistenceTableBlock(DocumentBlock block)
    {
        var content = block.Content as TableBlockContent;
        var table = new Wyg.TableBlock
        {
            Id = block.Id,
            Properties = FromPersistenceParagraphProperties(block.ParagraphProperties),
            TableProperties = content?.Layout is { } layout
                ? new Wyg.TableProperties
                {
                    Width = layout.Width?.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    Alignment = layout.Alignment,
                    CellPadding = layout.CellPadding,
                    BackgroundColor = layout.BackgroundColor,
                    Borders = layout.Borders
                }
                : null
        };
        if (content?.Rows is null) return table;

        foreach (var row in content.Rows)
        {
            var tableRow = new Wyg.TableRow();
            foreach (var cell in row.Cells)
            {
                var tableCell = new Wyg.TableCell
                {
                    Id = cell.Id,
                    ColumnSpan = cell.ColumnSpan,
                    RowSpan = cell.RowSpan,
                    Width = cell.Width,
                    BackgroundColor = cell.BackgroundColor,
                    Borders = cell.Borders,
                    VerticalAlignment = cell.VerticalAlignment,
                    Padding = cell.Padding
                };
                foreach (var cellBlock in cell.Blocks)
                {
                    tableCell.Blocks.Add(FromPersistenceBlock(cellBlock));
                }
                tableRow.Cells.Add(tableCell);
            }
            table.Rows.Add(tableRow);
        }
        return table;
    }

    private static double? ParseNullableDouble(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static string? ToCssNumber(double? value)
        => value?.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static Wyg.Inline FromPersistenceInline(InlineContent inline)
    {
        if (inline is DocumentDrawingRun drawing)
        {
            return new Wyg.DrawingInline
            {
                Id = drawing.Id ?? Guid.NewGuid().ToString("N"),
                ObjectId = drawing.ObjectId,
                Kind = drawing.Kind,
                Source = drawing.Source,
                Url = drawing.Url,
                AssetId = drawing.AssetId,
                AltText = drawing.AltText,
                IsDecorative = drawing.IsDecorative,
                Caption = drawing.Caption,
                Size = CloneImageSize(drawing.Size),
                NaturalSize = CloneImageSize(drawing.NaturalSize),
                Layout = CloneLayout(drawing.Layout),
                LinkUrl = drawing.LinkUrl,
                Metadata = drawing.Metadata.ToDictionary(pair => pair.Key, pair => pair.Value)
            };
        }

        if (inline is not TextRun textRun)
            return new Wyg.TextRun { Text = string.Empty };

        var result = new Wyg.TextRun { Id = textRun.Id ?? Guid.NewGuid().ToString("N"), Text = textRun.Text ?? string.Empty };
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
                InlineMarkType.FontFamily => new Wyg.FontMark { Family = mark.Value ?? string.Empty, Size = string.Empty },
                InlineMarkType.FontSize => new Wyg.FontMark { Family = string.Empty, Size = mark.Value ?? string.Empty },
                InlineMarkType.TextColor => new Wyg.ColorMark { Color = mark.Value ?? string.Empty },
                InlineMarkType.Highlight => new Wyg.HighlightMark { Color = mark.Value ?? string.Empty },
                InlineMarkType.Link => new Wyg.LinkMark { Href = mark.Link?.Href ?? string.Empty, Title = mark.Link?.Title },
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

    private static DocumentParagraphProperties ToPersistenceParagraphProperties(Wyg.ParagraphProperties? properties)
    {
        if (properties is null)
        {
            return new DocumentParagraphProperties();
        }

        return new DocumentParagraphProperties
        {
            Alignment = properties.Alignment switch
            {
                Wyg.TextAlignment.Center => DocumentTextAlignment.Center,
                Wyg.TextAlignment.Right => DocumentTextAlignment.Right,
                Wyg.TextAlignment.Justify => DocumentTextAlignment.Justify,
                _ => DocumentTextAlignment.Left
            },
            LineSpacing = properties.LineSpacing,
            SpacingBefore = CssLengthToPointsOrZero(properties.SpaceBefore),
            SpacingAfter = CssLengthToPointsOrZero(properties.SpaceAfter),
            LeftIndent = CssLengthToPointsOrZero(properties.LeftIndent),
            RightIndent = CssLengthToPointsOrZero(properties.RightIndent),
            FirstLineIndent = CssLengthToPointsOrZero(properties.FirstLineIndent)
        };
    }

    private static Wyg.ParagraphProperties FromPersistenceParagraphProperties(DocumentParagraphProperties? properties)
    {
        if (properties is null)
        {
            return new Wyg.ParagraphProperties();
        }

        return new Wyg.ParagraphProperties
        {
            Alignment = properties.Alignment switch
            {
                DocumentTextAlignment.Center => Wyg.TextAlignment.Center,
                DocumentTextAlignment.Right => Wyg.TextAlignment.Right,
                DocumentTextAlignment.Justify => Wyg.TextAlignment.Justify,
                _ => Wyg.TextAlignment.Left
            },
            LineSpacing = properties.LineSpacing,
            SpaceBefore = ToCssPointLength(properties.SpacingBefore),
            SpaceAfter = ToCssPointLength(properties.SpacingAfter),
            LeftIndent = ToCssPointLength(properties.LeftIndent),
            RightIndent = ToCssPointLength(properties.RightIndent),
            FirstLineIndent = ToCssPointLength(properties.FirstLineIndent)
        };
    }

    private static double CssLengthToPointsOrZero(string? css)
        => string.IsNullOrWhiteSpace(css) ? 0 : CssLengthToPoints(css) ?? 0;

    private static string? ToCssPointLength(double value)
        => Math.Abs(value) < 0.0001
            ? null
            : $"{value.ToString(System.Globalization.CultureInfo.InvariantCulture)}pt";

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
            Id = headerFooter.Id,
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
            Id = note.Id,
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
                Id = e.Id,
                Author = new DocumentEditorAuthor { Id = e.AuthorId ?? string.Empty, DisplayName = e.AuthorName ?? string.Empty },
                Text = e.Text,
                CreatedAt = e.CreatedAt
            }).ToList(),
            Status = comment.IsResolved ? DocumentCommentStatus.Resolved : DocumentCommentStatus.Open
        };
    }

    private static Wyg.DocumentComment FromPersistenceComment(DocumentComment comment)
    {
        var result = new Wyg.DocumentComment
        {
            Id = comment.Id,
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
                Id = entry.Id,
                AuthorId = entry.Author?.Id,
                AuthorName = entry.Author?.DisplayName,
                Text = entry.Text,
                CreatedAt = entry.CreatedAt
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
            Action = (DocumentRevisionAction)revision.Action,
            Range = ToPersistenceRevisionRange(revision.Range),
            PayloadJson = revision.PayloadJson,
            GroupId = revision.GroupId
        };
    }

    private static Wyg.DocumentRevision FromPersistenceRevision(DocumentRevision revision)
    {
        return new Wyg.DocumentRevision
        {
            Id = revision.Id,
            Type = (Wyg.DocumentRevisionType)revision.Type,
            AuthorId = revision.Author?.Id,
            AuthorName = revision.Author?.DisplayName,
            CreatedAt = revision.CreatedAt,
            Action = (Wyg.DocumentRevisionAction)revision.Action,
            Range = FromPersistenceRevisionRange(revision.Range),
            PayloadJson = revision.PayloadJson,
            GroupId = revision.GroupId
        };
    }

    private static DocumentRevisionRange ToPersistenceRevisionRange(Wyg.DocumentRevisionRange range)
        => new()
        {
            BlockId = range.BlockId,
            SourceBlockId = range.SourceBlockId,
            StartInlineIndex = range.StartInlineIndex,
            StartOffset = range.StartOffset,
            EndInlineIndex = range.EndInlineIndex,
            EndOffset = range.EndOffset
        };

    private static Wyg.DocumentRevisionRange FromPersistenceRevisionRange(DocumentRevisionRange? range)
        => new()
        {
            BlockId = range?.BlockId,
            SourceBlockId = range?.SourceBlockId,
            StartInlineIndex = range?.StartInlineIndex,
            StartOffset = range?.StartOffset,
            EndInlineIndex = range?.EndInlineIndex,
            EndOffset = range?.EndOffset
        };
}
