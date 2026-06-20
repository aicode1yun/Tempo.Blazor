using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using A = DocumentFormat.OpenXml.Drawing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace Tempo.Blazor.DocumentFormats.Tests.DocxDrawing;

public enum DocxDrawingFixtureWrap
{
    Inline,
    Square,
    Tight,
    Through,
    TopBottom,
    BehindText,
    InFrontOfText
}

internal sealed record DocxDrawingFixture(string Name, string Description, Func<byte[]> Create);

internal static class DocxDrawingFixtureBuilder
{
    private const long DefaultCx = 120 * 12700L;
    private const long DefaultCy = 80 * 12700L;
    private const long AnchorX = 36 * 12700L;
    private const long AnchorY = 24 * 12700L;

    private static readonly byte[] PngBytes = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=");

    private static readonly byte[] JpegBytes = Convert.FromBase64String(
        "/9j/4AAQSkZJRgABAQAAAQABAAD/2wCEAAkGBxAQEBAQEA8QDw8PDw8PDw8PDw8PDw8PFREWFhURFRUYHSggGBolGxUVITEhJSkrLi4uFx8zODMsNygtLisBCgoKDg0OGxAQGy0lICUtLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLf/AABEIAAEAAQMBIgACEQEDEQH/xAAXAAADAQAAAAAAAAAAAAAAAAAAAQID/8QAFBABAAAAAAAAAAAAAAAAAAAAAP/aAAwDAQACEAMQAAAB6g//xAAUEAEAAAAAAAAAAAAAAAAAAAAA/9oACAEBAAEFAqf/xAAUEQEAAAAAAAAAAAAAAAAAAAAA/9oACAEDAQE/ASP/xAAUEQEAAAAAAAAAAAAAAAAAAAAA/9oACAECAQE/ASP/xAAUEAEAAAAAAAAAAAAAAAAAAAAA/9oACAEBAAY/Al//xAAUEAEAAAAAAAAAAAAAAAAAAAAA/9oACAEBAAE/IV//2gAMAwEAAgADAAAAEAP/xAAUEQEAAAAAAAAAAAAAAAAAAAAA/9oACAEDAQE/EH//xAAUEQEAAAAAAAAAAAAAAAAAAAAA/9oACAECAQE/EH//xAAUEAEAAAAAAAAAAAAAAAAAAAAA/9oACAEBAAE/EH//2Q==");

    public static IReadOnlyList<DocxDrawingFixture> RequiredFixtures { get; } =
    [
        new("inline-png", "Inline PNG picture in the middle of a sentence.", CreateInlinePng),
        new("inline-jpeg-alt", "Inline JPEG picture with alt text.", CreateInlineJpegWithAltText),
        new("anchor-square", "Floating anchored picture with square wrapping.", () => CreateAnchor(DocxDrawingFixtureWrap.Square)),
        new("anchor-top-bottom", "Floating anchored picture with top-bottom wrapping.", () => CreateAnchor(DocxDrawingFixtureWrap.TopBottom)),
        new("anchor-behind-text", "Floating anchored picture behind text.", () => CreateAnchor(DocxDrawingFixtureWrap.BehindText)),
        new("anchor-in-front-of-text", "Floating anchored picture in front of text.", () => CreateAnchor(DocxDrawingFixtureWrap.InFrontOfText)),
        new("anchor-tight", "Floating anchored picture with tight wrap polygon.", () => CreateAnchor(DocxDrawingFixtureWrap.Tight)),
        new("anchor-through", "Floating anchored picture with through wrap polygon.", () => CreateAnchor(DocxDrawingFixtureWrap.Through)),
        new("crop", "Inline picture with DrawingML source rectangle crop.", CreateCroppedInline),
        new("rotation", "Inline picture with DrawingML transform rotation.", CreateRotatedInline),
        new("header-footer-table", "Header, footer and table-cell pictures with scoped relationships.", CreateHeaderFooterAndTableCell),
        new("onlyoffice-like-anchor", "OnlyOffice-like anchor shape with native position and picture graph.", CreateOnlyOfficeLikeAnchor)
    ];

    public static byte[] CreateInlinePng()
        => CreateBodyDocument(owner => new W.Paragraph(
            new W.Run(new W.Text("Before ")),
            CreateDrawingRun(owner, DrawingOptions.Inline("Inline PNG picture")),
            new W.Run(new W.Text(" after"))));

    public static byte[] CreateInlineJpegWithAltText()
        => CreateBodyDocument(owner => new W.Paragraph(
            new W.Run(new W.Text("JPEG ")),
            CreateDrawingRun(owner, DrawingOptions.Inline("Inline JPEG picture") with
            {
                ImageType = DocumentFormat.OpenXml.Packaging.ImagePartType.Jpeg,
                ImageBytes = JpegBytes,
                FileName = "image1.jpeg"
            })));

    public static byte[] CreateAnchor(DocxDrawingFixtureWrap wrap)
        => CreateBodyDocument(owner => new W.Paragraph(
            new W.Run(new W.Text($"Anchor {wrap} before ")),
            CreateDrawingRun(owner, DrawingOptions.Anchor($"{wrap} picture", wrap)),
            new W.Run(new W.Text(" after"))));

    public static byte[] CreateCroppedInline()
        => CreateBodyDocument(owner => new W.Paragraph(
            new W.Run(new W.Text("Crop ")),
            CreateDrawingRun(owner, DrawingOptions.Inline("Cropped picture") with
            {
                Crop = new CropRect(10000, 20000, 30000, 40000)
            })));

    public static byte[] CreateRotatedInline()
        => CreateBodyDocument(owner => new W.Paragraph(
            new W.Run(new W.Text("Rotation ")),
            CreateDrawingRun(owner, DrawingOptions.Inline("Rotated picture") with
            {
                Rotation = 15 * 60000
            })));

    public static byte[] CreateOnlyOfficeLikeAnchor()
        => CreateBodyDocument(owner => new W.Paragraph(
            new W.Run(new W.Text("OnlyOffice-like anchor ")),
            CreateDrawingRun(owner, DrawingOptions.Anchor("OnlyOffice-like picture", DocxDrawingFixtureWrap.InFrontOfText) with
            {
                LayoutInCell = false,
                AllowOverlap = true,
                RelativeHeight = 251659264,
                PositionRelativeFromHorizontal = DW.HorizontalRelativePositionValues.Page,
                PositionRelativeFromVertical = DW.VerticalRelativePositionValues.Page,
                X = 48 * 12700L,
                Y = 36 * 12700L
            })));

    public static byte[] CreateHeaderFooterAndTableCell()
    {
        using var memory = new MemoryStream();
        using (var word = WordprocessingDocument.Create(memory, WordprocessingDocumentType.Document, true))
        {
            var main = word.AddMainDocumentPart();
            var header = main.AddNewPart<HeaderPart>("rIdHeaderDrawing");
            var footer = main.AddNewPart<FooterPart>("rIdFooterDrawing");

            header.Header = new W.Header(new W.Paragraph(
                new W.Run(new W.Text("Header ")),
                CreateDrawingRun(header, DrawingOptions.Inline("Header picture"))));
            footer.Footer = new W.Footer(new W.Paragraph(
                new W.Run(new W.Text("Footer ")),
                CreateDrawingRun(footer, DrawingOptions.Inline("Footer picture"))));

            var table = new W.Table(new W.TableRow(new W.TableCell(new W.Paragraph(
                new W.Run(new W.Text("Cell ")),
                CreateDrawingRun(main, DrawingOptions.Inline("Table cell picture"))))));

            main.Document = new W.Document(new W.Body(
                new W.Paragraph(new W.Run(new W.Text("Body"))),
                table,
                new W.SectionProperties(
                    new W.HeaderReference { Type = W.HeaderFooterValues.Default, Id = main.GetIdOfPart(header) },
                    new W.FooterReference { Type = W.HeaderFooterValues.Default, Id = main.GetIdOfPart(footer) })));
            header.Header.Save();
            footer.Footer.Save();
            main.Document.Save();
        }

        return memory.ToArray();
    }

    private static byte[] CreateBodyDocument(Func<MainDocumentPart, W.Paragraph> paragraphFactory)
    {
        using var memory = new MemoryStream();
        using (var word = WordprocessingDocument.Create(memory, WordprocessingDocumentType.Document, true))
        {
            var main = word.AddMainDocumentPart();
            main.Document = new W.Document(new W.Body(
                paragraphFactory(main),
                new W.SectionProperties()));
            main.Document.Save();
        }

        return memory.ToArray();
    }

    private static W.Run CreateDrawingRun<TPart>(TPart owner, DrawingOptions options)
        where TPart : OpenXmlPartContainer, ISupportedRelationship<ImagePart>
    {
        var imagePart = owner.AddImagePart(options.ImageType);
        using (var stream = new MemoryStream(options.ImageBytes))
        {
            imagePart.FeedData(stream);
        }

        var relationshipId = owner.GetIdOfPart(imagePart);
        var graphic = CreatePictureGraphic(relationshipId, options);
        OpenXmlElement drawingBody = options.Wrap == DocxDrawingFixtureWrap.Inline
            ? CreateInline(options, graphic)
            : CreateAnchor(options, graphic);

        return new W.Run(new W.Drawing(drawingBody));
    }

    private static DW.Inline CreateInline(DrawingOptions options, A.Graphic graphic)
        => new(
            new DW.Extent { Cx = options.Cx, Cy = options.Cy },
            new DW.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
            new DW.DocProperties
            {
                Id = options.DocPrId,
                Name = options.Name,
                Description = options.Description,
                Title = options.Title
            },
            new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
            graphic)
        {
            DistanceFromTop = 0U,
            DistanceFromBottom = 0U,
            DistanceFromLeft = 0U,
            DistanceFromRight = 0U
        };

    private static DW.Anchor CreateAnchor(DrawingOptions options, A.Graphic graphic)
        => new(
            new DW.SimplePosition { X = 0L, Y = 0L },
            new DW.HorizontalPosition(new DW.PositionOffset(options.X.ToString(CultureInfo.InvariantCulture)))
            {
                RelativeFrom = options.PositionRelativeFromHorizontal
            },
            new DW.VerticalPosition(new DW.PositionOffset(options.Y.ToString(CultureInfo.InvariantCulture)))
            {
                RelativeFrom = options.PositionRelativeFromVertical
            },
            new DW.Extent { Cx = options.Cx, Cy = options.Cy },
            new DW.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
            CreateWrap(options),
            new DW.DocProperties
            {
                Id = options.DocPrId,
                Name = options.Name,
                Description = options.Description,
                Title = options.Title
            },
            new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
            graphic)
        {
            SimplePos = false,
            RelativeHeight = options.RelativeHeight,
            BehindDoc = options.Wrap == DocxDrawingFixtureWrap.BehindText,
            Locked = options.Locked,
            LayoutInCell = options.LayoutInCell,
            AllowOverlap = options.AllowOverlap,
            DistanceFromTop = 25400U,
            DistanceFromBottom = 25400U,
            DistanceFromLeft = 25400U,
            DistanceFromRight = 25400U
        };

    private static OpenXmlElement CreateWrap(DrawingOptions options)
    {
        return options.Wrap switch
        {
            DocxDrawingFixtureWrap.TopBottom => new DW.WrapTopBottom(),
            DocxDrawingFixtureWrap.BehindText or DocxDrawingFixtureWrap.InFrontOfText => new DW.WrapNone(),
            DocxDrawingFixtureWrap.Tight => new DW.WrapTight(CreateWrapPolygon(options)) { WrapText = DW.WrapTextValues.BothSides },
            DocxDrawingFixtureWrap.Through => new DW.WrapThrough(CreateWrapPolygon(options)) { WrapText = DW.WrapTextValues.BothSides },
            _ => new DW.WrapSquare { WrapText = DW.WrapTextValues.BothSides }
        };
    }

    private static DW.WrapPolygon CreateWrapPolygon(DrawingOptions options)
        => new(
            new DW.StartPoint { X = 0L, Y = 0L },
            new DW.LineTo { X = options.Cx, Y = 0L },
            new DW.LineTo { X = options.Cx, Y = options.Cy },
            new DW.LineTo { X = 0L, Y = options.Cy })
        {
            Edited = false
        };

    private static A.Graphic CreatePictureGraphic(string relationshipId, DrawingOptions options)
        => new(new A.GraphicData(
            new PIC.Picture(
                new PIC.NonVisualPictureProperties(
                    new PIC.NonVisualDrawingProperties
                    {
                        Id = options.PictureId,
                        Name = options.Name,
                        Description = options.Description,
                        Title = options.Title
                    },
                    new PIC.NonVisualPictureDrawingProperties(new A.PictureLocks { NoChangeAspect = true })),
                CreateBlipFill(relationshipId, options),
                new PIC.ShapeProperties(
                    new A.Transform2D(
                        new A.Offset { X = 0L, Y = 0L },
                        new A.Extents { Cx = options.Cx, Cy = options.Cy })
                    {
                        Rotation = options.Rotation
                    },
                    new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })))
        { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" });

    private static PIC.BlipFill CreateBlipFill(string relationshipId, DrawingOptions options)
    {
        var blipFill = new PIC.BlipFill(new A.Blip { Embed = relationshipId });
        if (options.Crop is not null)
        {
            blipFill.Append(new A.SourceRectangle
            {
                Left = options.Crop.Left,
                Top = options.Crop.Top,
                Right = options.Crop.Right,
                Bottom = options.Crop.Bottom
            });
        }

        blipFill.Append(new A.Stretch(new A.FillRectangle()));
        return blipFill;
    }

    private sealed record CropRect(int Left, int Top, int Right, int Bottom);

    private sealed record DrawingOptions
    {
        public required DocxDrawingFixtureWrap Wrap { get; init; }

        public required string Description { get; init; }

        public string Name { get; init; } = "Picture 1";

        public string Title { get; init; } = "DrawingML fixture picture";

        public UInt32Value DocPrId { get; init; } = 1U;

        public UInt32Value PictureId { get; init; } = 2U;

        public PartTypeInfo ImageType { get; init; } = DocumentFormat.OpenXml.Packaging.ImagePartType.Png;

        public byte[] ImageBytes { get; init; } = PngBytes;

        public string FileName { get; init; } = "image1.png";

        public long Cx { get; init; } = DefaultCx;

        public long Cy { get; init; } = DefaultCy;

        public long X { get; init; } = AnchorX;

        public long Y { get; init; } = AnchorY;

        public UInt32Value RelativeHeight { get; init; } = 42U;

        public bool Locked { get; init; }

        public bool LayoutInCell { get; init; } = true;

        public bool AllowOverlap { get; init; } = true;

        public Int32Value? Rotation { get; init; }

        public CropRect? Crop { get; init; }

        public DW.HorizontalRelativePositionValues PositionRelativeFromHorizontal { get; init; } = DW.HorizontalRelativePositionValues.Margin;

        public DW.VerticalRelativePositionValues PositionRelativeFromVertical { get; init; } = DW.VerticalRelativePositionValues.Paragraph;

        public static DrawingOptions Inline(string description)
            => new() { Wrap = DocxDrawingFixtureWrap.Inline, Description = description };

        public static DrawingOptions Anchor(string description, DocxDrawingFixtureWrap wrap)
            => new() { Wrap = wrap, Description = description };
    }
}
