using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentFormats.Docx;

namespace Tempo.Blazor.DocumentFormats.Tests.DocxDrawing;

public sealed class DocumentDocxDrawingPhase35Tests
{
    [Fact]
    public async Task Phase35_ExportAsync_HeaderDrawingRunWritesDrawingElement()
    {
        var exported = await new DocumentDocxExporter().ExportAsync(CreateScopedDrawingDocument());

        using var package = DocxDrawingTestPackage.Open(exported.Content);
        var headerXml = package.ReadXml(package.HeaderPartPaths.Single());

        headerXml.Descendants(DocxDrawingTestPackage.W + "drawing").Should().ContainSingle();
        package.AssertHasInlinePicture(headerXml, "Header scoped image");
    }

    [Fact]
    public async Task Phase35_ImportAsync_HeaderDrawingRunStaysInHeaderWithAnchorScope()
    {
        var imported = await new DocumentDocxImporter().ImportAsync(new MemoryStream(DocxDrawingFixtureBuilder.CreateHeaderFooterAndTableCell()));
        var header = imported.Document.HeadersFooters.Single(item => item.Type == DocumentHeaderFooterType.Header);
        var drawing = header.Blocks.SelectMany(GetInlines).OfType<DocumentDrawingRun>().Single();

        drawing.AltText.Should().Be("Header picture");
        drawing.Layout.Anchor.Region.Should().Be(DocumentRenditionAnchorScope.Header);
        drawing.Layout.Anchor.HeaderFooterId.Should().Be(header.Id);
    }

    [Fact]
    public async Task Phase35_ExportAsync_FooterDrawingRunWritesDrawingElement()
    {
        var exported = await new DocumentDocxExporter().ExportAsync(CreateScopedDrawingDocument());

        using var package = DocxDrawingTestPackage.Open(exported.Content);
        var footerXml = package.ReadXml(package.FooterPartPaths.Single());

        footerXml.Descendants(DocxDrawingTestPackage.W + "drawing").Should().ContainSingle();
        package.AssertHasInlinePicture(footerXml, "Footer scoped image");
    }

    [Fact]
    public async Task Phase35_ImportAsync_FooterDrawingRunStaysInFooterWithAnchorScope()
    {
        var imported = await new DocumentDocxImporter().ImportAsync(new MemoryStream(DocxDrawingFixtureBuilder.CreateHeaderFooterAndTableCell()));
        var footer = imported.Document.HeadersFooters.Single(item => item.Type == DocumentHeaderFooterType.Footer);
        var drawing = footer.Blocks.SelectMany(GetInlines).OfType<DocumentDrawingRun>().Single();

        drawing.AltText.Should().Be("Footer picture");
        drawing.Layout.Anchor.Region.Should().Be(DocumentRenditionAnchorScope.Footer);
        drawing.Layout.Anchor.HeaderFooterId.Should().Be(footer.Id);
    }

    [Fact]
    public async Task Phase35_ExportAsync_TableCellDrawingRunStaysInCellWithAnchorIds()
    {
        var document = CreateScopedDrawingDocument();
        var exported = await new DocumentDocxExporter().ExportAsync(document);

        using var package = DocxDrawingTestPackage.Open(exported.Content);
        var tableDrawing = package.AssertTableCellDrawing();
        var inline = tableDrawing.Descendants(DocxDrawingTestPackage.Wp + "inline").Single();

        ((string?)inline.Attribute(DocxDrawingTestPackage.Tm + "anchor-region")).Should().Be(DocumentRenditionAnchorScope.TableCell.ToString());
        ((string?)inline.Attribute(DocxDrawingTestPackage.Tm + "table-id")).Should().Be("phase35-table");
        ((string?)inline.Attribute(DocxDrawingTestPackage.Tm + "cell-id")).Should().Be("phase35-cell");
    }

    [Fact]
    public async Task Phase35_ImportAsync_TableCellDrawingRunStaysInCellWithAnchorIds()
    {
        var imported = await new DocumentDocxImporter().ImportAsync(new MemoryStream(DocxDrawingFixtureBuilder.CreateHeaderFooterAndTableCell()));
        var tableBlock = imported.Document.Blocks.Single(block => block.Content is TableBlockContent);
        var table = (TableBlockContent)tableBlock.Content;
        var cell = table.Rows.Single().Cells.Single();
        var drawing = cell.Blocks.SelectMany(GetInlines).OfType<DocumentDrawingRun>().Single();

        drawing.AltText.Should().Be("Table cell picture");
        drawing.Layout.Anchor.Region.Should().Be(DocumentRenditionAnchorScope.TableCell);
        drawing.Layout.Anchor.TableId.Should().Be(tableBlock.Id);
        drawing.Layout.Anchor.CellId.Should().Be(cell.Id);
    }

    [Fact]
    public async Task Phase35_ExportAsync_FootnoteDrawingRunStaysInFootnote()
    {
        var exported = await new DocumentDocxExporter().ExportAsync(CreateScopedDrawingDocument());

        using var package = DocxDrawingTestPackage.Open(exported.Content);
        var footnotesXml = package.ReadXml("word/footnotes.xml");
        var inline = package.AssertHasInlinePicture(footnotesXml, "Footnote scoped image");

        footnotesXml.Descendants(DocxDrawingTestPackage.W + "footnote")
            .SelectMany(footnote => footnote.Descendants(DocxDrawingTestPackage.W + "drawing"))
            .Should()
            .ContainSingle();
        ((string?)inline.Attribute(DocxDrawingTestPackage.Tm + "anchor-region")).Should().Be(DocumentRenditionAnchorScope.Footnote.ToString());
    }

    [Fact]
    public async Task Phase35_ImportAsync_FootnoteDrawingRunStaysInFootnoteWithAnchorScope()
    {
        var exported = await new DocumentDocxExporter().ExportAsync(CreateScopedDrawingDocument());

        var imported = await new DocumentDocxImporter().ImportAsync(new MemoryStream(exported.Content));
        var drawing = imported.Document.Notes
            .Single(note => note.Type == DocumentNoteType.Footnote)
            .Blocks
            .SelectMany(GetInlines)
            .OfType<DocumentDrawingRun>()
            .Single();

        drawing.AltText.Should().Be("Footnote scoped image");
        drawing.Layout.Anchor.Region.Should().Be(DocumentRenditionAnchorScope.Footnote);
    }

    [Fact]
    public async Task Phase35_ExportAsync_CommentDrawingRunStaysInCommentsPart()
    {
        var exported = await new DocumentDocxExporter().ExportAsync(CreateScopedDrawingDocument());

        using var package = DocxDrawingTestPackage.Open(exported.Content);
        var commentsXml = package.ReadXml("word/comments.xml");
        var inline = package.AssertHasInlinePicture(commentsXml, "Comment scoped image");

        commentsXml.Descendants(DocxDrawingTestPackage.W + "comment")
            .SelectMany(comment => comment.Descendants(DocxDrawingTestPackage.W + "drawing"))
            .Should()
            .ContainSingle();
        ((string?)inline.Attribute(DocxDrawingTestPackage.Tm + "anchor-region")).Should().Be(DocumentRenditionAnchorScope.Comment.ToString());
        exported.Warnings.Should().NotContain(warning => warning.Code.Contains("comment", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Phase35_ImportAsync_CommentDrawingRunStaysInCommentEntryWithAnchorScope()
    {
        var exported = await new DocumentDocxExporter().ExportAsync(CreateScopedDrawingDocument());

        var imported = await new DocumentDocxImporter().ImportAsync(new MemoryStream(exported.Content));
        var drawing = imported.Document.Comments
            .Single()
            .Entries
            .SelectMany(entry => entry.Inlines)
            .OfType<DocumentDrawingRun>()
            .Single();

        drawing.AltText.Should().Be("Comment scoped image");
        drawing.Layout.Anchor.Region.Should().Be(DocumentRenditionAnchorScope.Comment);
    }

    private static DocumentEditorDocument CreateScopedDrawingDocument()
    {
        var document = DocumentEditorDocument.Empty("phase35-scoped-drawings");
        document.Blocks.Add(Paragraph(
            new TextRun { Text = "Body " },
            new DocumentNoteReferenceRun { NoteId = "1", NoteType = DocumentNoteType.Footnote },
            new TextRun
            {
                Text = " comment anchor",
                Marks = [new InlineMark { Type = InlineMarkType.CommentAnchor, CommentAnchor = new CommentAnchorMarkData { CommentId = "phase35-comment" } }]
            }));
        document.Blocks.Add(new DocumentBlock
        {
            Id = "phase35-table",
            Type = DocumentBlockType.Table,
            Order = 1,
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
                                Id = "phase35-cell",
                                Blocks = [Paragraph(new TextRun { Text = "Cell " }, Drawing("Table cell scoped image"))]
                            }
                        ]
                    }
                ]
            }
        });
        document.HeadersFooters.Add(new DocumentHeaderFooter
        {
            Id = "phase35-header",
            Type = DocumentHeaderFooterType.Header,
            Blocks = [Paragraph(new TextRun { Text = "Header " }, Drawing("Header scoped image"))]
        });
        document.HeadersFooters.Add(new DocumentHeaderFooter
        {
            Id = "phase35-footer",
            Type = DocumentHeaderFooterType.Footer,
            Blocks = [Paragraph(new TextRun { Text = "Footer " }, Drawing("Footer scoped image"))]
        });
        document.Notes.Add(new DocumentNote
        {
            Id = "1",
            Type = DocumentNoteType.Footnote,
            Blocks = [Paragraph(new TextRun { Text = "Footnote " }, Drawing("Footnote scoped image"))]
        });
        document.Comments.Add(new DocumentComment
        {
            Id = "phase35-comment",
            Entries =
            [
                new DocumentCommentEntry
                {
                    Author = new DocumentEditorAuthor { DisplayName = "Reviewer" },
                    Text = "Comment scoped image",
                    Inlines = [new TextRun { Text = "Comment " }, Drawing("Comment scoped image")]
                }
            ]
        });

        return document;
    }

    private static DocumentBlock Paragraph(params InlineContent[] inlines)
        => new()
        {
            Type = DocumentBlockType.Paragraph,
            Content = new ParagraphBlockContent { Inlines = inlines.ToList() }
        };

    private static DocumentDrawingRun Drawing(string altText)
        => new()
        {
            Source = DocumentImageSource.Url,
            Url = DocumentFormatTestData.TransparentPngDataUrl,
            AltText = altText,
            Size = new DocumentImageSize { Width = 24, Height = 24 },
            Layout = new DocumentObjectLayout
            {
                Kind = DocumentObjectLayoutKind.Inline,
                Wrap = new DocumentObjectWrap { Mode = DocumentWrapMode.Inline },
                Transform = new DocumentObjectTransform { Width = 24, Height = 24 }
            }
        };

    private static IEnumerable<InlineContent> GetInlines(DocumentBlock block)
        => block.Content switch
        {
            ParagraphBlockContent paragraph => paragraph.Inlines,
            HeadingBlockContent heading => heading.Inlines,
            ListBlockContent list => list.Inlines,
            QuoteBlockContent quote => quote.Inlines,
            _ => []
        };
}
