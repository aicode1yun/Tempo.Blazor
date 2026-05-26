using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using System.Text;
using System.Xml.Linq;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentFormats.Docx;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace Tempo.Blazor.DocumentFormats.Tests.DocxDrawing;

public sealed class DocumentDocxDrawingPhase36Tests
{
    private const long Cx = 120 * 12700L;
    private const long Cy = 80 * 12700L;

    private static readonly byte[] PngBytes = Convert.FromBase64String(
        DocumentFormatTestData.TransparentPngDataUrl[(DocumentFormatTestData.TransparentPngDataUrl.IndexOf(',') + 1)..]);

    [Fact]
    public async Task Phase36_ImportAsync_ChartGraphicDataWarnsButDoesNotCrash()
    {
        await using var package = CreateUnsupportedGraphicDataDocx("http://schemas.openxmlformats.org/drawingml/2006/chart", "Chart drawing");

        var imported = await new DocumentDocxImporter().ImportAsync(package);

        imported.Warnings.Should().Contain(warning =>
            warning.Code == "docx.drawingChartUnsupported"
            && warning.SourcePath == "word/document.xml");
        imported.Document.Blocks.SelectMany(GetInlines).OfType<TextRun>().Select(run => run.Text)
            .Should()
            .Contain(["Before ", " after"]);
    }

    [Fact]
    public async Task Phase36_ImportAsync_SmartArtAndCanvasGroupWarnAndPreserveRawDrawingXml()
    {
        await using var package = CreateSmartArtAndCanvasDocx();

        var imported = await new DocumentDocxImporter().ImportAsync(package);

        imported.Warnings.Should().Contain(warning => warning.Code == "docx.drawingSmartArtUnsupported");
        imported.Warnings.Should().Contain(warning => warning.Code == "docx.drawingCanvasGroupUnsupported");
        imported.PreservedParts.Should().Contain(part =>
            part.Path.Contains("#drawing/", StringComparison.Ordinal)
            && Encoding.UTF8.GetString(part.Content).Contains("diagram", StringComparison.OrdinalIgnoreCase));
        imported.PreservedParts.Should().Contain(part =>
            part.Path.Contains("#drawing/", StringComparison.Ordinal)
            && Encoding.UTF8.GetString(part.Content).Contains("wordprocessingCanvas", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Phase36_ImportAsync_UnsupportedPictureEffectPreservesRawDrawingAndExportsFallback()
    {
        await using var package = CreateUnsupportedEffectPictureDocx();

        var imported = await new DocumentDocxImporter().ImportAsync(package);
        var drawing = DocumentImagePersistence.EnumerateDrawingRuns(imported.Document).Single();

        imported.Warnings.Should().Contain(warning =>
            warning.Code == "docx.drawingUnsupportedEffectPreserved"
            && warning.SourcePath == "word/document.xml");
        drawing.Docx!.RawDrawingXml.Should().Contain("outerShdw");

        var exported = await new DocumentDocxExporter().ExportAsync(imported.Document);

        exported.Warnings.Should().Contain(warning => warning.Code == "docx.drawingUnsupportedEffectExportFallback");
        using var exportedPackage = DocxDrawingTestPackage.Open(exported.Content);
        exportedPackage.DocumentXml.ToString(SaveOptions.DisableFormatting).Should().Contain("outerShdw");
    }

    [Fact]
    public async Task Phase36_ImportAsync_BrokenRelationshipWarnsWithOwningPartPath()
    {
        await using var package = CreateHeaderBrokenRelationshipDocx();

        var imported = await new DocumentDocxImporter().ImportAsync(package);

        imported.Warnings.Should().Contain(warning =>
            warning.Code == "docx.imageMissingPart"
            && warning.SourcePath != null
            && warning.SourcePath.StartsWith("word/header", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Phase36_ImportAsync_MissingBlipWarnsWithPartPath()
    {
        await using var package = CreateMissingBlipPictureDocx();

        var imported = await new DocumentDocxImporter().ImportAsync(package);

        imported.Warnings.Should().Contain(warning =>
            warning.Code == "docx.imageBlipMissing"
            && warning.SourcePath == "word/document.xml");
    }

    [Fact]
    public async Task Phase36_ImportAsync_DrawingWithoutInlineOrAnchorWarnsWithPartPath()
    {
        await using var package = CreateBodyDocument(main => new W.Paragraph(
            new W.Run(new W.Text("Invalid ")),
            new W.Run(new W.Drawing()),
            new W.Run(new W.Text(" drawing"))));

        var imported = await new DocumentDocxImporter().ImportAsync(package);

        imported.Warnings.Should().Contain(warning =>
            warning.Code == "docx.drawingHostMissing"
            && warning.SourcePath == "word/document.xml");
        imported.PreservedParts.Should().Contain(part => part.Path.Contains("#drawing/", StringComparison.Ordinal));
    }

    private static MemoryStream CreateUnsupportedGraphicDataDocx(string uri, string description)
        => CreateBodyDocument(main => new W.Paragraph(
            new W.Run(new W.Text("Before ")),
            CreateDrawingRun(CreateDrawing(description, new A.Graphic(new A.GraphicData { Uri = uri }))),
            new W.Run(new W.Text(" after"))));

    private static MemoryStream CreateSmartArtAndCanvasDocx()
        => CreateBodyDocument(main => new W.Paragraph(
            new W.Run(new W.Text("Unsupported ")),
            CreateDrawingRun(CreateDrawing("SmartArt drawing", new A.Graphic(new A.GraphicData { Uri = "http://schemas.openxmlformats.org/drawingml/2006/diagram" }))),
            CreateDrawingRun(CreateDrawing("Canvas group drawing", new A.Graphic(new A.GraphicData { Uri = "http://schemas.microsoft.com/office/word/2010/wordprocessingCanvas" }))),
            new W.Run(new W.Text(" preserved"))));

    private static MemoryStream CreateMissingBlipPictureDocx()
        => CreateBodyDocument(main => new W.Paragraph(
            new W.Run(new W.Text("Missing blip ")),
            CreateDrawingRun(CreateDrawing("Missing blip picture", CreatePictureGraphic(null, includeBlip: false)))));

    private static MemoryStream CreateUnsupportedEffectPictureDocx()
        => CreateBodyDocument(main =>
        {
            var relId = AddImage(main);
            return new W.Paragraph(
                new W.Run(new W.Text("Effect ")),
                CreateDrawingRun(CreateDrawing("Unsupported effect picture", CreatePictureGraphic(relId, includeBlip: true, includeOuterShadow: true))));
        });

    private static MemoryStream CreateHeaderBrokenRelationshipDocx()
    {
        var memory = new MemoryStream();
        using (var word = WordprocessingDocument.Create(memory, WordprocessingDocumentType.Document, true))
        {
            var main = word.AddMainDocumentPart();
            var header = main.AddNewPart<HeaderPart>();
            header.Header = new W.Header(new W.Paragraph(
                new W.Run(new W.Text("Broken ")),
                CreateDrawingRun(CreateDrawing("Broken header picture", CreatePictureGraphic("rIdMissing", includeBlip: true)))));
            main.Document = new W.Document(new W.Body(
                new W.Paragraph(new W.Run(new W.Text("Body"))),
                new W.SectionProperties(new W.HeaderReference { Type = W.HeaderFooterValues.Default, Id = main.GetIdOfPart(header) })));
            header.Header.Save();
            main.Document.Save();
        }

        memory.Position = 0;
        return memory;
    }

    private static MemoryStream CreateBodyDocument(Func<MainDocumentPart, W.Paragraph> paragraphFactory)
    {
        var memory = new MemoryStream();
        using (var word = WordprocessingDocument.Create(memory, WordprocessingDocumentType.Document, true))
        {
            var main = word.AddMainDocumentPart();
            main.Document = new W.Document(new W.Body(paragraphFactory(main), new W.SectionProperties()));
            main.Document.Save();
        }

        memory.Position = 0;
        return memory;
    }

    private static string AddImage<TPart>(TPart owner)
        where TPart : OpenXmlPartContainer, ISupportedRelationship<ImagePart>
    {
        var imagePart = owner.AddImagePart(ImagePartType.Png);
        using (var stream = new MemoryStream(PngBytes))
        {
            imagePart.FeedData(stream);
        }

        return owner.GetIdOfPart(imagePart);
    }

    private static W.Run CreateDrawingRun(W.Drawing drawing)
        => new(drawing);

    private static W.Drawing CreateDrawing(string description, A.Graphic graphic)
        => new(new DW.Inline(
            new DW.Extent { Cx = Cx, Cy = Cy },
            new DW.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
            new DW.DocProperties { Id = 1U, Name = description, Description = description },
            new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
            graphic)
        {
            DistanceFromTop = 0U,
            DistanceFromBottom = 0U,
            DistanceFromLeft = 0U,
            DistanceFromRight = 0U
        });

    private static A.Graphic CreatePictureGraphic(string? relId, bool includeBlip, bool includeOuterShadow = false)
    {
        var blipFill = includeBlip
            ? new PIC.BlipFill(new A.Blip { Embed = relId })
            : new PIC.BlipFill();
        blipFill.Append(new A.Stretch(new A.FillRectangle()));

        var shapeProperties = new PIC.ShapeProperties(
            new A.Transform2D(
                new A.Offset { X = 0L, Y = 0L },
                new A.Extents { Cx = Cx, Cy = Cy }),
            new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle });
        if (includeOuterShadow)
        {
            shapeProperties.Append(new A.EffectList(new A.OuterShadow()));
        }

        return new A.Graphic(new A.GraphicData(
            new PIC.Picture(
                new PIC.NonVisualPictureProperties(
                    new PIC.NonVisualDrawingProperties { Id = 2U, Name = "Picture", Description = "Picture" },
                    new PIC.NonVisualPictureDrawingProperties(new A.PictureLocks { NoChangeAspect = true })),
                blipFill,
                shapeProperties))
        { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" });
    }

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
