using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentFormats.Docx;
using Tempo.Blazor.DocumentFormats.Tests;

namespace Tempo.Blazor.DocumentFormats.Tests.DocxDrawing;

public sealed class DocumentDocxDrawingPhase39Tests
{
    [Fact]
    public async Task Phase39_Export_WritesStableTempoIdsForImportedDrawingE2EAnchors()
    {
        var exported = await new DocumentDocxExporter().ExportAsync(CreatePhase39Document());

        using var package = DocxDrawingTestPackage.Open(exported.Content);
        package.DocumentXml.Descendants(DocxDrawingTestPackage.W + "p")
            .Should()
            .Contain(element => (string?)element.Attribute(DocxDrawingTestPackage.Tm + "block-id") == "phase39-body");
        package.DocumentXml.Descendants(DocxDrawingTestPackage.W + "tbl")
            .Should()
            .Contain(element => (string?)element.Attribute(DocxDrawingTestPackage.Tm + "block-id") == "phase39-table");
        package.DocumentXml.Descendants(DocxDrawingTestPackage.W + "tc")
            .Should()
            .Contain(element => (string?)element.Attribute(DocxDrawingTestPackage.Tm + "cell-id") == "phase39-cell");

        var inline = package.AssertHasInlinePicture(altText: "Phase39 inline image");
        ((string?)inline.Attribute(DocxDrawingTestPackage.Tm + "object-id")).Should().Be("phase39-inline-object");
        ((string?)inline.Attribute(DocxDrawingTestPackage.Tm + "run-id")).Should().Be("phase39-inline-run");

        var anchor = package.AssertHasAnchorPicture(altText: "Phase39 square image");
        ((string?)anchor.Attribute(DocxDrawingTestPackage.Tm + "object-id")).Should().Be("phase39-square-object");
        ((string?)anchor.Attribute(DocxDrawingTestPackage.Tm + "anchor-block-id")).Should().Be("phase39-body");

        var headerXml = package.ReadXml(package.HeaderPartPaths.Single());
        ((string?)headerXml.Root!.Attribute(DocxDrawingTestPackage.Tm + "header-footer-id")).Should().Be("phase39-header");
        var headerInline = package.AssertHasInlinePicture(headerXml, "Phase39 header image");
        ((string?)headerInline.Attribute(DocxDrawingTestPackage.Tm + "object-id")).Should().Be("phase39-header-object");
    }

    [Fact]
    public async Task Phase39_RoundTrip_PreservesStableImportedDrawingIdsAndAnchors()
    {
        var exported = await new DocumentDocxExporter().ExportAsync(CreatePhase39Document());

        var imported = await new DocumentDocxImporter().ImportAsync(new MemoryStream(exported.Content));

        imported.Document.Blocks.Should().Contain(block => block.Id == "phase39-body");
        imported.Document.HeadersFooters.Should().Contain(header => header.Id == "phase39-header");
        var table = imported.Document.Blocks.Single(block => block.Id == "phase39-table").Content.Should().BeOfType<TableBlockContent>().Subject;
        table.Rows.Single().Cells.Single().Id.Should().Be("phase39-cell");
        table.Rows.Single().Cells.Single().Blocks.Single().Id.Should().Be("phase39-cell-block");

        var drawings = EnumerateDrawings(imported.Document).ToDictionary(drawing => drawing.AltText!, StringComparer.Ordinal);
        drawings["Phase39 inline image"].ObjectId.Should().Be("phase39-inline-object");
        drawings["Phase39 inline image"].Id.Should().Be("phase39-inline-run");
        drawings["Phase39 inline image"].Layout.Anchor.BlockId.Should().Be("phase39-body");

        drawings["Phase39 square image"].ObjectId.Should().Be("phase39-square-object");
        drawings["Phase39 square image"].Layout.Wrap.Mode.Should().Be(DocumentWrapMode.Square);
        drawings["Phase39 square image"].Layout.Anchor.BlockId.Should().Be("phase39-body");

        drawings["Phase39 header image"].ObjectId.Should().Be("phase39-header-object");
        drawings["Phase39 header image"].Layout.Anchor.Region.Should().Be(DocumentRenditionAnchorScope.Header);
        drawings["Phase39 header image"].Layout.Anchor.HeaderFooterId.Should().Be("phase39-header");

        drawings["Phase39 table image"].ObjectId.Should().Be("phase39-table-object");
        drawings["Phase39 table image"].Layout.Anchor.Region.Should().Be(DocumentRenditionAnchorScope.TableCell);
        drawings["Phase39 table image"].Layout.Anchor.TableId.Should().Be("phase39-table");
        drawings["Phase39 table image"].Layout.Anchor.CellId.Should().Be("phase39-cell");
    }

    private static DocumentEditorDocument CreatePhase39Document()
    {
        var document = DocumentEditorDocument.Empty("phase39-docx-drawing-e2e");
        document.Metadata.Title = "Phase39 DOCX drawing E2E";
        document.Blocks =
        [
            new DocumentBlock
            {
                Id = "phase39-body",
                Order = 0,
                Content = new ParagraphBlockContent
                {
                    Inlines =
                    [
                        new TextRun { Text = "Before " },
                        CreateDrawing("phase39-inline-object", "phase39-inline-run", "Phase39 inline image", CreateInlineLayout("phase39-body")),
                        new TextRun { Text = " after " },
                        CreateDrawing("phase39-square-object", "phase39-square-run", "Phase39 square image", CreateSquareLayout("phase39-body"))
                    ]
                }
            },
            new DocumentBlock
            {
                Id = "phase39-table",
                Order = 1,
                Type = DocumentBlockType.Table,
                Content = new TableBlockContent
                {
                    Rows =
                    [
                        new TableRowContent
                        {
                            Cells =
                            [
                                new TableCellContent
                                {
                                    Id = "phase39-cell",
                                    Blocks =
                                    [
                                        new DocumentBlock
                                        {
                                            Id = "phase39-cell-block",
                                            Content = new ParagraphBlockContent
                                            {
                                                Inlines =
                                                [
                                                    new TextRun { Text = "Cell " },
                                                    CreateDrawing(
                                                        "phase39-table-object",
                                                        "phase39-table-run",
                                                        "Phase39 table image",
                                                        CreateInlineLayout("phase39-cell-block"))
                                                ]
                                            }
                                        }
                                    ]
                                }
                            ]
                        }
                    ]
                }
            }
        ];

        document.HeadersFooters.Add(new DocumentHeaderFooter
        {
            Id = "phase39-header",
            Type = DocumentHeaderFooterType.Header,
            Scope = DocumentHeaderFooterScope.Primary,
            Blocks =
            [
                new DocumentBlock
                {
                    Id = "phase39-header-block",
                    Content = new ParagraphBlockContent
                    {
                        Inlines =
                        [
                            new TextRun { Text = "Header " },
                            CreateDrawing(
                                "phase39-header-object",
                                "phase39-header-run",
                                "Phase39 header image",
                                CreateInlineLayout("phase39-header-block"))
                        ]
                    }
                }
            ]
        });

        return document;
    }

    private static DocumentDrawingRun CreateDrawing(string objectId, string runId, string altText, DocumentObjectLayout layout)
        => new()
        {
            ObjectId = objectId,
            Id = runId,
            Source = DocumentImageSource.Url,
            Url = DocumentFormatTestData.TransparentPngDataUrl,
            AltText = altText,
            Size = new DocumentImageSize { Width = 96, Height = 54 },
            NaturalSize = new DocumentImageSize { Width = 96, Height = 54 },
            Layout = layout
        };

    private static DocumentObjectLayout CreateInlineLayout(string anchorBlockId)
    {
        var layout = DocumentObjectLayout.Inline();
        layout.Anchor.BlockId = anchorBlockId;
        layout.Anchor.MoveWithText = true;
        layout.Transform.Width = 96;
        layout.Transform.Height = 54;
        layout.Transform.NaturalWidth = 96;
        layout.Transform.NaturalHeight = 54;
        return layout;
    }

    private static DocumentObjectLayout CreateSquareLayout(string anchorBlockId)
        => new()
        {
            Kind = DocumentObjectLayoutKind.Anchored,
            Anchor = new DocumentObjectAnchor
            {
                BlockId = anchorBlockId,
                InlineIndex = 3,
                Offset = 12,
                MoveWithText = true
            },
            Position = new DocumentObjectPosition
            {
                HorizontalRelativeTo = DocumentRelativePosition.Margin,
                VerticalRelativeTo = DocumentRelativePosition.Paragraph,
                HorizontalAlignment = DocumentImageHorizontalPosition.Left,
                X = 18,
                Y = 12
            },
            Wrap = new DocumentObjectWrap
            {
                Mode = DocumentWrapMode.Square,
                DistanceLeft = 6,
                DistanceRight = 6
            },
            Transform = new DocumentObjectTransform
            {
                Width = 96,
                Height = 54,
                NaturalWidth = 96,
                NaturalHeight = 54,
                LockAspectRatio = true
            }
        };

    private static IEnumerable<DocumentDrawingRun> EnumerateDrawings(DocumentEditorDocument document)
    {
        foreach (var drawing in EnumerateBlocks(document.Blocks).SelectMany(GetBlockDrawings))
        {
            yield return drawing;
        }

        foreach (var drawing in document.HeadersFooters.SelectMany(header => EnumerateBlocks(header.Blocks)).SelectMany(GetBlockDrawings))
        {
            yield return drawing;
        }
    }

    private static IEnumerable<DocumentBlock> EnumerateBlocks(IEnumerable<DocumentBlock> blocks)
    {
        foreach (var block in blocks)
        {
            yield return block;
            if (block.Content is not TableBlockContent table)
            {
                continue;
            }

            foreach (var child in table.Rows.SelectMany(row => row.Cells).SelectMany(cell => cell.Blocks))
            {
                yield return child;
            }
        }
    }

    private static IEnumerable<DocumentDrawingRun> GetBlockDrawings(DocumentBlock block)
        => block.Content switch
        {
            ParagraphBlockContent paragraph => paragraph.Inlines.OfType<DocumentDrawingRun>(),
            HeadingBlockContent heading => heading.Inlines.OfType<DocumentDrawingRun>(),
            ListBlockContent list => list.Inlines.OfType<DocumentDrawingRun>(),
            QuoteBlockContent quote => quote.Inlines.OfType<DocumentDrawingRun>(),
            _ => []
        };
}
