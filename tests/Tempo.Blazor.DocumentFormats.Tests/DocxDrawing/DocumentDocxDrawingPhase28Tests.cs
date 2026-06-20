using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentFormats.Docx;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace Tempo.Blazor.DocumentFormats.Tests.DocxDrawing;

public sealed class DocumentDocxDrawingPhase28Tests
{
    private static readonly byte[] PngBytes = Convert.FromBase64String(
        DocumentFormatTestData.TransparentPngDataUrl[(DocumentFormatTestData.TransparentPngDataUrl.IndexOf(',') + 1)..]);

    [Fact]
    public async Task Phase28_ImportAsync_InlineImageBetweenTextKeepsInlineOrder()
    {
        var imported = await ImportParagraphAsync(main => new W.Paragraph(
            new W.Run(new W.Text("Text A ")),
            CreateDrawingRun(main, "Middle image"),
            new W.Run(new W.Text(" Text B"))));

        var inlines = FirstParagraph(imported.Document).Inlines;

        inlines.Should().HaveCount(3);
        inlines[0].Should().BeOfType<TextRun>().Which.Text.Should().Be("Text A ");
        inlines[1].Should().BeOfType<DocumentDrawingRun>().Which.AltText.Should().Be("Middle image");
        inlines[2].Should().BeOfType<TextRun>().Which.Text.Should().Be(" Text B");
    }

    [Fact]
    public async Task Phase28_ImportAsync_TwoInlineImagesInSameSentenceKeepBothPositions()
    {
        var imported = await ImportParagraphAsync(main => new W.Paragraph(
            new W.Run(new W.Text("A ")),
            CreateDrawingRun(main, "First image"),
            new W.Run(new W.Text(" B ")),
            CreateDrawingRun(main, "Second image"),
            new W.Run(new W.Text(" C"))));

        var inlines = FirstParagraph(imported.Document).Inlines;

        inlines.Select(inline => inline.GetType()).Should().Equal(
            typeof(TextRun),
            typeof(DocumentDrawingRun),
            typeof(TextRun),
            typeof(DocumentDrawingRun),
            typeof(TextRun));
        inlines.OfType<DocumentDrawingRun>().Select(drawing => drawing.AltText)
            .Should()
            .Equal("First image", "Second image");
    }

    [Fact]
    public async Task Phase28_ImportAsync_HyperlinkAroundImageBecomesDrawingLinkUrl()
    {
        var imported = await ImportParagraphAsync(main =>
        {
            var hyperlink = main.AddHyperlinkRelationship(new Uri("https://example.test/picture"), true);
            return new W.Paragraph(
                new W.Run(new W.Text("Before ")),
                new W.Hyperlink(CreateDrawingRun(main, "Linked image")) { Id = hyperlink.Id },
                new W.Run(new W.Text(" after")));
        });

        var drawing = FirstParagraph(imported.Document).Inlines.OfType<DocumentDrawingRun>().Single();

        drawing.LinkUrl.Should().Be("https://example.test/picture");
        drawing.Marks.Should().NotContain(mark => mark.Type == InlineMarkType.Link);
    }

    [Fact]
    public async Task Phase28_ImportAsync_CommentRangeAcrossTextAndImageKeepsCommentMarkOnDrawing()
    {
        var imported = await ImportParagraphAsync(main => new W.Paragraph(
            new W.CommentRangeStart { Id = "4" },
            new W.Run(new W.Text("Reviewed ")),
            CreateDrawingRun(main, "Commented image"),
            new W.CommentRangeEnd { Id = "4" },
            new W.Run(new W.CommentReference { Id = "4" })));

        var inlines = FirstParagraph(imported.Document).Inlines;
        var text = inlines.OfType<TextRun>().Single();
        var drawing = inlines.OfType<DocumentDrawingRun>().Single();

        text.Marks.Should().Contain(mark => mark.Type == InlineMarkType.CommentAnchor && mark.CommentAnchor != null && mark.CommentAnchor.CommentId == "4");
        drawing.Marks.Should().Contain(mark => mark.Type == InlineMarkType.CommentAnchor && mark.CommentAnchor != null && mark.CommentAnchor.CommentId == "4");
    }

    [Fact]
    public async Task Phase28_ImportAsync_RevisionRunWithImageKeepsImageInPlaceAndMarksIt()
    {
        var imported = await ImportParagraphAsync(main => new W.Paragraph(
            new W.InsertedRun(
                new W.Run(new W.Text("Inserted ")),
                CreateDrawingRun(main, "Inserted image"),
                new W.Run(new W.Text(" text")))
            {
                Id = "9",
                Author = "Reviewer",
                Date = DateTime.UtcNow
            },
            new W.Run(new W.Text(" tail"))));

        var inlines = FirstParagraph(imported.Document).Inlines;

        inlines.Select(inline => inline.GetType()).Should().Equal(
            typeof(TextRun),
            typeof(DocumentDrawingRun),
            typeof(TextRun),
            typeof(TextRun));
        var drawing = inlines.OfType<DocumentDrawingRun>().Single();
        drawing.Marks.Should().Contain(mark => mark.Type == InlineMarkType.Revision && mark.RevisionId == "docx-rev-9");
    }

    [Fact]
    public async Task Phase28_ImportAsync_TextBeforeDrawingInSameRunIsPreserved()
    {
        var imported = await ImportParagraphAsync(main => new W.Paragraph(
            new W.Run(
                new W.Text("Before "),
                CreateDrawing(main, "Same run image"))));

        var inlines = FirstParagraph(imported.Document).Inlines;

        inlines.Should().HaveCount(2);
        inlines[0].Should().BeOfType<TextRun>().Which.Text.Should().Be("Before ");
        inlines[1].Should().BeOfType<DocumentDrawingRun>().Which.AltText.Should().Be("Same run image");
    }

    [Fact]
    public async Task Phase28_ImportAsync_TextAfterDrawingInSameRunIsPreserved()
    {
        var imported = await ImportParagraphAsync(main => new W.Paragraph(
            new W.Run(
                CreateDrawing(main, "Same run image"),
                new W.Text(" after"))));

        var inlines = FirstParagraph(imported.Document).Inlines;

        inlines.Should().HaveCount(2);
        inlines[0].Should().BeOfType<DocumentDrawingRun>().Which.AltText.Should().Be("Same run image");
        inlines[1].Should().BeOfType<TextRun>().Which.Text.Should().Be(" after");
    }

    [Fact]
    public async Task Phase28_ImportAsync_ImageAtStartWithoutBreakDoesNotTreatTrailingTextAsCaption()
    {
        var imported = await ImportParagraphAsync(main => new W.Paragraph(
            CreateDrawingRun(main, "Leading image"),
            new W.Run(new W.Text("ordinary trailing text"))));

        var inlines = FirstParagraph(imported.Document).Inlines;
        var drawing = inlines.OfType<DocumentDrawingRun>().Single();

        inlines.Should().HaveCount(2);
        inlines[0].Should().BeOfType<DocumentDrawingRun>();
        inlines[1].Should().BeOfType<TextRun>().Which.Text.Should().Be("ordinary trailing text");
        drawing.Caption.Should().BeNull();
    }

    [Fact]
    public async Task Phase28_ImportAsync_DrawingAnchorReceivesBlockInlineIndexAndOffset()
    {
        var imported = await ImportParagraphAsync(main => new W.Paragraph(
            new W.Run(new W.Text("Alpha ")),
            CreateDrawingRun(main, "Anchored image"),
            new W.Run(new W.Text(" omega"))));

        var block = imported.Document.Blocks.Single();
        var drawing = FirstParagraph(imported.Document).Inlines.OfType<DocumentDrawingRun>().Single();

        drawing.Layout.Anchor.BlockId.Should().Be(block.Id);
        drawing.Layout.Anchor.InlineIndex.Should().Be(1);
        drawing.Layout.Anchor.Offset.Should().Be("Alpha ".Length);
    }

    private static async Task<DocumentFormatImportResult> ImportParagraphAsync(Func<MainDocumentPart, W.Paragraph> paragraphFactory)
    {
        await using var package = CreateDocx(paragraphFactory);
        return await new DocumentDocxImporter().ImportAsync(package);
    }

    private static ParagraphBlockContent FirstParagraph(DocumentEditorDocument document)
        => document.Blocks.Select(block => block.Content).OfType<ParagraphBlockContent>().First();

    private static MemoryStream CreateDocx(Func<MainDocumentPart, W.Paragraph> paragraphFactory)
    {
        var memory = new MemoryStream();
        using (var word = WordprocessingDocument.Create(memory, WordprocessingDocumentType.Document, true))
        {
            var main = word.AddMainDocumentPart();
            main.Document = new W.Document(new W.Body(
                paragraphFactory(main),
                new W.SectionProperties()));
            main.Document.Save();
        }

        memory.Position = 0;
        return memory;
    }

    private static W.Run CreateDrawingRun(MainDocumentPart owner, string altText)
        => new(CreateDrawing(owner, altText));

    private static W.Drawing CreateDrawing(MainDocumentPart owner, string altText)
    {
        var imagePart = owner.AddImagePart(ImagePartType.Png);
        using (var stream = new MemoryStream(PngBytes))
        {
            imagePart.FeedData(stream);
        }

        var relationshipId = owner.GetIdOfPart(imagePart);
        var cx = DocxUnitConverter.PointToEmu(64);
        var cy = DocxUnitConverter.PointToEmu(32);
        return new W.Drawing(new DW.Inline(
            new DW.Extent { Cx = cx, Cy = cy },
            new DW.DocProperties { Id = 1U, Name = altText, Description = altText },
            new A.Graphic(new A.GraphicData(
                new PIC.Picture(
                    new PIC.NonVisualPictureProperties(
                        new PIC.NonVisualDrawingProperties { Id = 1U, Name = altText, Description = altText },
                        new PIC.NonVisualPictureDrawingProperties()),
                    new PIC.BlipFill(new A.Blip { Embed = relationshipId }, new A.Stretch(new A.FillRectangle())),
                    new PIC.ShapeProperties(
                        new A.Transform2D(
                            new A.Offset { X = 0L, Y = 0L },
                            new A.Extents { Cx = cx, Cy = cy }),
                        new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })))
            { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" })));
    }
}
