using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentFormats.Docx;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace Tempo.Blazor.DocumentFormats.Tests.DocxDrawing;

public sealed class DocumentDocxDrawingPhase30Tests
{
    private static readonly byte[] PngBytes = Convert.FromBase64String(
        DocumentFormatTestData.TransparentPngDataUrl[(DocumentFormatTestData.TransparentPngDataUrl.IndexOf(',') + 1)..]);

    [Fact]
    public async Task Phase30_ExportAsync_AnchoredDrawingWritesNativeAnchorGeometry()
    {
        var exported = await new DocumentDocxExporter().ExportAsync(CreateAnchorGeometryDocument());

        using var stream = new MemoryStream(exported.Content);
        using var word = WordprocessingDocument.Open(stream, false);
        var anchor = FindAnchor(word, "Phase 30 offset anchor");
        var horizontal = anchor.GetFirstChild<DW.HorizontalPosition>()!;
        var vertical = anchor.GetFirstChild<DW.VerticalPosition>()!;

        anchor.SimplePos!.Value.Should().BeFalse();
        anchor.GetFirstChild<DW.SimplePosition>()!.X!.Value.Should().Be(111L);
        anchor.GetFirstChild<DW.SimplePosition>()!.Y!.Value.Should().Be(222L);
        horizontal.RelativeFrom!.Value.Should().Be(DW.HorizontalRelativePositionValues.Margin);
        horizontal.GetFirstChild<DW.PositionOffset>()!.Text.Should().Be(DocxUnitConverter.PointToEmu(36).ToString());
        horizontal.GetFirstChild<DW.HorizontalAlignment>().Should().BeNull();
        vertical.RelativeFrom!.Value.Should().Be(DW.VerticalRelativePositionValues.Page);
        vertical.GetFirstChild<DW.PositionOffset>()!.Text.Should().Be(DocxUnitConverter.PointToEmu(48).ToString());
        vertical.GetFirstChild<DW.VerticalAlignment>().Should().BeNull();
        anchor.RelativeHeight!.Value.Should().Be(42U);
        anchor.Locked!.Value.Should().BeTrue();
        anchor.LayoutInCell!.Value.Should().BeFalse();
        anchor.AllowOverlap!.Value.Should().BeFalse();
        anchor.Hidden!.Value.Should().BeTrue();
        anchor.AnchorId!.Value.Should().Be("4E9B3B91");
        anchor.EditId!.Value.Should().Be("2A1C4D88");
    }

    [Fact]
    public async Task Phase30_ExportAsync_FixedDrawingStillWritesAnchorWithAlignmentPresets()
    {
        var exported = await new DocumentDocxExporter().ExportAsync(CreateAnchorGeometryDocument());

        using var stream = new MemoryStream(exported.Content);
        using var word = WordprocessingDocument.Open(stream, false);
        var anchor = FindAnchor(word, "Phase 30 aligned fixed anchor");
        var horizontal = anchor.GetFirstChild<DW.HorizontalPosition>()!;
        var vertical = anchor.GetFirstChild<DW.VerticalPosition>()!;

        anchor.GetFirstChild<DW.Inline>().Should().BeNull();
        horizontal.RelativeFrom!.Value.Should().Be(DW.HorizontalRelativePositionValues.Page);
        horizontal.GetFirstChild<DW.HorizontalAlignment>()!.Text.Should().Be("right");
        horizontal.GetFirstChild<DW.PositionOffset>().Should().BeNull();
        vertical.RelativeFrom!.Value.Should().Be(DW.VerticalRelativePositionValues.Margin);
        vertical.GetFirstChild<DW.VerticalAlignment>()!.Text.Should().Be("bottom");
        vertical.GetFirstChild<DW.PositionOffset>().Should().BeNull();
        anchor.RelativeHeight!.Value.Should().Be(99U);
    }

    [Fact]
    public async Task Phase30_ImportAsync_NativeAnchorGeometryReadsWithoutTempoAttributes()
    {
        await using var package = CreateNativeAnchorDocx();

        var imported = await new DocumentDocxImporter().ImportAsync(package);

        var drawing = DocumentImagePersistence.EnumerateDrawingRuns(imported.Document).Single();
        var layout = drawing.Layout;
        var metadata = drawing.Docx;

        layout.Kind.Should().Be(DocumentObjectLayoutKind.Anchored);
        layout.Anchor.LockAnchor.Should().BeTrue();
        layout.Position.HorizontalRelativeTo.Should().Be(DocumentRelativePosition.Character);
        layout.Position.X.Should().BeApproximately(36, 0.1);
        layout.Position.VerticalRelativeTo.Should().Be(DocumentRelativePosition.Line);
        layout.Position.VerticalAlignment.Should().Be(DocumentObjectVerticalAlignment.Bottom);
        layout.Position.Y.Should().Be(0);
        layout.Stacking.ZIndex.Should().Be(73);
        layout.Stacking.AllowOverlap.Should().BeFalse();
        metadata.Should().NotBeNull();
        metadata!.UsesSimplePosition.Should().BeFalse();
        metadata.SimplePosition.Should().NotBeNull();
        metadata.SimplePosition!.X.Should().Be(111);
        metadata.SimplePosition.Y.Should().Be(222);
        metadata.LayoutInCell.Should().BeFalse();
        metadata.Hidden.Should().BeTrue();
        metadata.AnchorId.Should().Be("5F6A7B8C");
        metadata.EditId.Should().Be("1A2B3C4D");
    }

    [Fact]
    public async Task Phase30_ImportAsync_PageRelativeNativeAnchorBecomesFixedLayout()
    {
        var imported = await new DocumentDocxImporter().ImportAsync(new MemoryStream(DocxDrawingFixtureBuilder.CreateOnlyOfficeLikeAnchor()));

        var drawing = DocumentImagePersistence.EnumerateDrawingRuns(imported.Document).Single();

        drawing.Layout.Kind.Should().Be(DocumentObjectLayoutKind.Fixed);
        drawing.Layout.Anchor.MoveWithText.Should().BeFalse();
        drawing.Layout.Anchor.FixedOnPage.Should().BeTrue();
        drawing.Layout.Position.HorizontalRelativeTo.Should().Be(DocumentRelativePosition.Page);
        drawing.Layout.Position.VerticalRelativeTo.Should().Be(DocumentRelativePosition.Page);
        drawing.Layout.Position.X.Should().BeApproximately(48, 0.1);
        drawing.Layout.Position.Y.Should().BeApproximately(36, 0.1);
        drawing.Docx!.LayoutInCell.Should().BeFalse();
    }

    private static DW.Anchor FindAnchor(WordprocessingDocument word, string altText)
        => word.MainDocumentPart!.Document.Body!.Descendants<DW.Anchor>()
            .Single(anchor => anchor.Descendants<DW.DocProperties>().Any(properties => properties.Description?.Value == altText));

    private static DocumentEditorDocument CreateAnchorGeometryDocument()
    {
        var offsetLayout = new DocumentObjectLayout
        {
            Kind = DocumentObjectLayoutKind.Anchored,
            Anchor = new DocumentObjectAnchor { LockAnchor = true },
            Position = new DocumentObjectPosition
            {
                HorizontalRelativeTo = DocumentRelativePosition.Margin,
                VerticalRelativeTo = DocumentRelativePosition.Page,
                X = 36,
                Y = 48
            },
            Wrap = new DocumentObjectWrap { Mode = DocumentWrapMode.Square },
            Transform = new DocumentObjectTransform { Width = 160, Height = 90 },
            Stacking = new DocumentObjectStacking { ZIndex = 42, AllowOverlap = false }
        };
        var alignedLayout = new DocumentObjectLayout
        {
            Kind = DocumentObjectLayoutKind.Fixed,
            Anchor = new DocumentObjectAnchor { MoveWithText = false, FixedOnPage = true },
            Position = new DocumentObjectPosition
            {
                HorizontalRelativeTo = DocumentRelativePosition.Page,
                VerticalRelativeTo = DocumentRelativePosition.Margin,
                HorizontalAlignment = DocumentImageHorizontalPosition.Right,
                VerticalAlignment = DocumentObjectVerticalAlignment.Bottom,
                X = 12,
                Y = 24
            },
            Wrap = new DocumentObjectWrap { Mode = DocumentWrapMode.InFrontOfText },
            Transform = new DocumentObjectTransform { Width = 120, Height = 70 },
            Stacking = new DocumentObjectStacking { ZIndex = 99, AllowOverlap = true }
        };

        var document = DocumentEditorDocument.Empty("phase30-anchor-geometry");
        document.Blocks.Add(new DocumentBlock
        {
            Id = "paragraph-1",
            Type = DocumentBlockType.Paragraph,
            Content = new ParagraphBlockContent
            {
                Inlines =
                [
                    CreateDrawing("Phase 30 offset anchor", offsetLayout, new DocumentDocxDrawingMetadata
                    {
                        UsesSimplePosition = false,
                        SimplePosition = new DocumentObjectPoint { X = 111, Y = 222 },
                        LayoutInCell = false,
                        Hidden = true,
                        AnchorId = "4E9B3B91",
                        EditId = "2A1C4D88"
                    }),
                    CreateDrawing("Phase 30 aligned fixed anchor", alignedLayout, new DocumentDocxDrawingMetadata())
                ]
            }
        });
        return document;
    }

    private static DocumentDrawingRun CreateDrawing(string altText, DocumentObjectLayout layout, DocumentDocxDrawingMetadata metadata)
        => new()
        {
            Source = DocumentImageSource.Url,
            Url = DocumentFormatTestData.TransparentPngDataUrl,
            AltText = altText,
            Size = new DocumentImageSize { Width = layout.Transform.Width, Height = layout.Transform.Height },
            Layout = layout,
            Docx = metadata
        };

    private static MemoryStream CreateNativeAnchorDocx()
    {
        var memory = new MemoryStream();
        using (var word = WordprocessingDocument.Create(memory, WordprocessingDocumentType.Document, true))
        {
            var main = word.AddMainDocumentPart();
            main.Document = new W.Document(new W.Body(
                new W.Paragraph(new W.Run(new W.Text("Before ")), CreateNativeAnchorRun(main)),
                new W.SectionProperties()));
            main.Document.Save();
        }

        memory.Position = 0;
        return memory;
    }

    private static W.Run CreateNativeAnchorRun(MainDocumentPart owner)
    {
        var imagePart = owner.AddImagePart(ImagePartType.Png);
        using (var stream = new MemoryStream(PngBytes))
        {
            imagePart.FeedData(stream);
        }

        var relationshipId = owner.GetIdOfPart(imagePart);
        var cx = DocxUnitConverter.PointToEmu(160);
        var cy = DocxUnitConverter.PointToEmu(90);
        var anchor = new DW.Anchor(
            new DW.SimplePosition { X = 111L, Y = 222L },
            new DW.HorizontalPosition(new DW.PositionOffset(DocxUnitConverter.PointToEmu(36).ToString()))
            {
                RelativeFrom = DW.HorizontalRelativePositionValues.Character
            },
            new DW.VerticalPosition(new DW.VerticalAlignment("bottom"))
            {
                RelativeFrom = DW.VerticalRelativePositionValues.Line
            },
            new DW.Extent { Cx = cx, Cy = cy },
            new DW.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
            new DW.WrapSquare { WrapText = DW.WrapTextValues.BothSides },
            new DW.DocProperties { Id = 1U, Name = "Native anchor", Description = "Native anchor" },
            new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
            CreatePictureGraphic(relationshipId, cx, cy))
        {
            SimplePos = false,
            RelativeHeight = 73U,
            BehindDoc = false,
            Locked = true,
            LayoutInCell = false,
            Hidden = true,
            AllowOverlap = false,
            AnchorId = "5F6A7B8C",
            EditId = "1A2B3C4D",
            DistanceFromTop = 0U,
            DistanceFromBottom = 0U,
            DistanceFromLeft = 0U,
            DistanceFromRight = 0U
        };

        return new W.Run(new W.Drawing(anchor));
    }

    private static A.Graphic CreatePictureGraphic(string relationshipId, long cx, long cy)
        => new(new A.GraphicData(
            new PIC.Picture(
                new PIC.NonVisualPictureProperties(
                    new PIC.NonVisualDrawingProperties { Id = 2U, Name = "Native anchor", Description = "Native anchor" },
                    new PIC.NonVisualPictureDrawingProperties()),
                new PIC.BlipFill(new A.Blip { Embed = relationshipId }, new A.Stretch(new A.FillRectangle())),
                new PIC.ShapeProperties(
                    new A.Transform2D(new A.Offset { X = 0L, Y = 0L }, new A.Extents { Cx = cx, Cy = cy }),
                    new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })))
        { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" });
}
