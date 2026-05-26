using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Tests.Models.DocumentEditor;

public sealed class DocumentDrawingRunDocxMetadataTests
{
    [Fact]
    public void DrawingRun_CanCarryDocumentDocxDrawingMetadata()
    {
        var run = new DocumentDrawingRun
        {
            ObjectId = "drawing-1",
            Docx = new DocumentDocxDrawingMetadata
            {
                DocPrId = 42,
                RelationshipId = "rId5"
            }
        };

        run.Docx.Should().NotBeNull();
        run.Docx!.DocPrId.Should().Be(42);
        run.Docx.RelationshipId.Should().Be("rId5");
        run.Metadata.Should().BeEmpty();
    }

    [Fact]
    public void DocxMetadata_StoresDocPrAndPictureNonVisualProperties()
    {
        var metadata = CreateFullMetadata();

        metadata.DocPrId.Should().Be(7);
        metadata.DocPrName.Should().Be("Picture 7");
        metadata.DocPrTitle.Should().Be("Revenue chart");
        metadata.DocPrDescription.Should().Be("Quarterly revenue chart");
        metadata.PictureNonVisualId.Should().Be(9);
        metadata.PictureName.Should().Be("Revenue.png");
        metadata.PictureDescription.Should().Be("Embedded revenue image");
    }

    [Fact]
    public void DocxMetadata_StoresMediaAndBlipReferenceProperties()
    {
        var metadata = CreateFullMetadata();

        metadata.RelationshipId.Should().Be("rId12");
        metadata.BlipLinkRelationshipId.Should().Be("rIdExternal");
        metadata.ImageReferenceMode.Should().Be(DocumentDocxImageReferenceMode.External);
        metadata.BlipCompressionState.Should().Be("print");
        metadata.BlipFillMode.Should().Be(DocumentDocxBlipFillMode.Tile);
        metadata.RawBlipFillXml.Should().Be("<pic:blipFill><a:tile /></pic:blipFill>");
        metadata.PresetGeometry.Should().Be("roundRect");
        metadata.RawShapePropertiesXml.Should().Be("<pic:spPr><a:prstGeom prst=\"roundRect\" /></pic:spPr>");
        metadata.Media.SourcePartUri.Should().Be("/word/document.xml");
        metadata.Media.ImagePartUri.Should().Be("/word/media/image7.png");
        metadata.Media.ContentType.Should().Be("image/png");
        metadata.Media.OriginalFileName.Should().Be("revenue-source.png");
        metadata.Media.Extension.Should().Be(".png");
    }

    [Fact]
    public void DocxMetadata_StoresAnchorEffectRelativeAndRawPreserveProperties()
    {
        var metadata = CreateFullMetadata();

        metadata.EffectExtent.Left.Should().Be(11430);
        metadata.EffectExtent.Top.Should().Be(22860);
        metadata.EffectExtent.Right.Should().Be(34290);
        metadata.EffectExtent.Bottom.Should().Be(45720);
        metadata.LayoutInCell.Should().BeFalse();
        metadata.Hidden.Should().BeTrue();
        metadata.UsesSimplePosition.Should().BeTrue();
        metadata.SimplePosition.Should().NotBeNull();
        metadata.SimplePosition!.X.Should().Be(914400);
        metadata.SimplePosition.Y.Should().Be(1828800);
        metadata.AnchorId.Should().Be("4E9B3B91");
        metadata.EditId.Should().Be("2A1C4D88");
        metadata.RelativeWidth.Should().NotBeNull();
        metadata.RelativeWidth!.RelativeFrom.Should().Be("margin");
        metadata.RelativeWidth.Percent.Should().Be(65);
        metadata.RelativeWidth.RawValue.Should().Be("65000");
        metadata.RelativeHeight.Should().NotBeNull();
        metadata.RelativeHeight!.RelativeFrom.Should().Be("page");
        metadata.RelativeHeight.Percent.Should().Be(40);
        metadata.RelativeHeight.RawValue.Should().Be("40000");
        metadata.RawDrawingXml.Should().Be("<wp:anchor><a:extLst /></wp:anchor>");
    }

    [Fact]
    public void DocumentEditorJson_RoundTripsDocxMetadataAndTransformFlip()
    {
        var document = DocumentEditorDocument.Empty("docx-metadata-roundtrip");
        document.Blocks.Add(new DocumentBlock
        {
            Id = "paragraph-1",
            Type = DocumentBlockType.Paragraph,
            Content = new ParagraphBlockContent
            {
                Inlines =
                [
                    new TextRun { Id = "text-1", Text = "Before " },
                    CreateDrawingWithMetadata()
                ]
            }
        });

        var json = DocumentEditorJson.Serialize(document);
        var restored = DocumentEditorJson.Deserialize(json);

        json.Should().Contain("\"Docx\"");
        json.Should().Contain("\"RawDrawingXml\"");
        var drawing = ((ParagraphBlockContent)restored.Blocks.Single().Content).Inlines
            .OfType<DocumentDrawingRun>()
            .Single();
        drawing.Docx.Should().NotBeNull();
        drawing.Docx!.DocPrId.Should().Be(7);
        drawing.Docx.DocPrName.Should().Be("Picture 7");
        drawing.Docx.PictureNonVisualId.Should().Be(9);
        drawing.Docx.RelationshipId.Should().Be("rId12");
        drawing.Docx.BlipLinkRelationshipId.Should().Be("rIdExternal");
        drawing.Docx.BlipFillMode.Should().Be(DocumentDocxBlipFillMode.Tile);
        drawing.Docx.PresetGeometry.Should().Be("roundRect");
        drawing.Docx.RawShapePropertiesXml.Should().Contain("roundRect");
        drawing.Docx.Media.ImagePartUri.Should().Be("/word/media/image7.png");
        drawing.Docx.EffectExtent.Right.Should().Be(34290);
        drawing.Docx.SimplePosition!.Y.Should().Be(1828800);
        drawing.Docx.AnchorId.Should().Be("4E9B3B91");
        drawing.Docx.RelativeWidth!.RawValue.Should().Be("65000");
        drawing.Docx.RawDrawingXml.Should().Be("<wp:anchor><a:extLst /></wp:anchor>");
        drawing.Layout.Transform.Flip.Should().NotBeNull();
        drawing.Layout.Transform.Flip!.Horizontal.Should().BeTrue();
        drawing.Layout.Transform.Flip.Vertical.Should().BeFalse();
    }

    [Fact]
    public void Sanitize_PreservesDocxMetadataAndRepairsNestedDefaults()
    {
        var drawing = CreateDrawingWithMetadata();
        drawing.Source = DocumentImageSource.Asset;
        drawing.AssetId = "asset-1";
        drawing.Url = "blob:https://app.test/display-only";
        var metadata = drawing.Docx!;

        DocumentImagePersistence.Sanitize(drawing);

        drawing.Url.Should().BeNull();
        drawing.Docx.Should().BeSameAs(metadata);
        drawing.Docx!.DocPrId.Should().Be(7);
        drawing.Docx.Media.ContentType.Should().Be("image/png");
        drawing.Docx.BlipFillMode.Should().Be(DocumentDocxBlipFillMode.Tile);
        drawing.Docx.RawBlipFillXml.Should().Contain("tile");
        drawing.Docx.EffectExtent.Left.Should().Be(11430);
        drawing.Docx.RawDrawingXml.Should().Be("<wp:anchor><a:extLst /></wp:anchor>");

        drawing.Docx.Media = null!;
        drawing.Docx.EffectExtent = null!;

        DocumentImagePersistence.Sanitize(drawing);

        drawing.Docx.Media.Should().NotBeNull();
        drawing.Docx.EffectExtent.Should().NotBeNull();
    }

    private static DocumentDrawingRun CreateDrawingWithMetadata()
        => new()
        {
            Id = "drawing-inline-1",
            ObjectId = "drawing-object-1",
            Source = DocumentImageSource.Url,
            Url = "https://cdn.example.test/revenue.png",
            AltText = "Revenue chart",
            Size = new DocumentImageSize { Width = 320, Height = 180 },
            NaturalSize = new DocumentImageSize { Width = 640, Height = 360 },
            Layout = new DocumentObjectLayout
            {
                Kind = DocumentObjectLayoutKind.Anchored,
                Transform = new DocumentObjectTransform
                {
                    Width = 320,
                    Height = 180,
                    Flip = new DocumentObjectFlip { Horizontal = true }
                }
            },
            Docx = CreateFullMetadata()
        };

    private static DocumentDocxDrawingMetadata CreateFullMetadata()
        => new()
        {
            DocPrId = 7,
            DocPrName = "Picture 7",
            DocPrTitle = "Revenue chart",
            DocPrDescription = "Quarterly revenue chart",
            PictureNonVisualId = 9,
            PictureName = "Revenue.png",
            PictureDescription = "Embedded revenue image",
            RelationshipId = "rId12",
            BlipLinkRelationshipId = "rIdExternal",
            ImageReferenceMode = DocumentDocxImageReferenceMode.External,
            BlipCompressionState = "print",
            BlipFillMode = DocumentDocxBlipFillMode.Tile,
            RawBlipFillXml = "<pic:blipFill><a:tile /></pic:blipFill>",
            PresetGeometry = "roundRect",
            RawShapePropertiesXml = "<pic:spPr><a:prstGeom prst=\"roundRect\" /></pic:spPr>",
            Media = new DocumentImageMediaInfo
            {
                SourcePartUri = "/word/document.xml",
                ImagePartUri = "/word/media/image7.png",
                ContentType = "image/png",
                OriginalFileName = "revenue-source.png",
                Extension = ".png"
            },
            EffectExtent = new DocumentObjectEffectExtent
            {
                Left = 11430,
                Top = 22860,
                Right = 34290,
                Bottom = 45720
            },
            LayoutInCell = false,
            Hidden = true,
            UsesSimplePosition = true,
            SimplePosition = new DocumentObjectPoint
            {
                X = 914400,
                Y = 1828800
            },
            AnchorId = "4E9B3B91",
            EditId = "2A1C4D88",
            RelativeWidth = new DocumentObjectRelativeSize
            {
                RelativeFrom = "margin",
                Percent = 65,
                RawValue = "65000"
            },
            RelativeHeight = new DocumentObjectRelativeSize
            {
                RelativeFrom = "page",
                Percent = 40,
                RawValue = "40000"
            },
            RawDrawingXml = "<wp:anchor><a:extLst /></wp:anchor>"
        };
}
