using FluentAssertions;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Tests.Models.DocumentEditor;

public class DocumentDrawingRunModelTests
{
    [Fact]
    public void Paragraph_CanContainDocumentDrawingRun()
    {
        var paragraph = new ParagraphBlockContent
        {
            Inlines =
            [
                new TextRun { Id = "before", Text = "Text before " },
                new DocumentDrawingRun
                {
                    Id = "drawing-inline-1",
                    ObjectId = "image-object-1",
                    Source = DocumentImageSource.Asset,
                    AssetId = "asset-1",
                    AltText = "Evidence preview"
                },
                new TextRun { Id = "after", Text = " text after." }
            ]
        };

        paragraph.Inlines.Should().HaveCount(3);
        paragraph.Inlines[1].Should().BeOfType<DocumentDrawingRun>();
        paragraph.Inlines[1].Should().BeAssignableTo<InlineContent>();
    }

    [Fact]
    public void DrawingRun_DefaultsToImageWithStableObjectIdAndInlineLayout()
    {
        var run = new DocumentDrawingRun();

        run.ObjectId.Should().NotBeNullOrWhiteSpace();
        run.Kind.Should().Be(DocumentDrawingKind.Image);
        run.Source.Should().Be(DocumentImageSource.Url);
        run.Layout.Kind.Should().Be(DocumentObjectLayoutKind.Inline);
        run.Layout.IsInline.Should().BeTrue();
        run.Layout.Wrap.Mode.Should().Be(DocumentWrapMode.Inline);
        run.Layout.Anchor.MoveWithText.Should().BeTrue();
        run.Size.Should().NotBeNull();
        run.NaturalSize.Should().NotBeNull();
        run.Metadata.Should().BeEmpty();
    }

    [Fact]
    public void DrawingRun_StoresImagePayloadAndCanonicalLayout()
    {
        var run = new DocumentDrawingRun
        {
            ObjectId = "drawing-image-1",
            Kind = DocumentDrawingKind.Image,
            Source = DocumentImageSource.Url,
            Url = "https://cdn.test/evidence.png",
            AltText = "Contract evidence",
            Caption = "Figure 1: Contract evidence",
            IsDecorative = true,
            LinkUrl = "https://example.test/evidence",
            Size = new DocumentImageSize { Width = 320, Height = 180, LockAspectRatio = true },
            NaturalSize = new DocumentImageSize { Width = 640, Height = 360, LockAspectRatio = true },
            Layout = DocumentObjectLayout.Anchored(DocumentWrapMode.Square, DocumentImageHorizontalPosition.Right)
        };
        run.Layout.Anchor.BlockId = "paragraph-1";
        run.Layout.Anchor.InlineIndex = 1;
        run.Layout.Anchor.Offset = 12;
        run.Layout.Transform.Width = 320;
        run.Layout.Transform.Height = 180;
        run.Metadata["origin"] = "phase-1";

        run.ObjectId.Should().Be("drawing-image-1");
        run.Source.Should().Be(DocumentImageSource.Url);
        run.Url.Should().Be("https://cdn.test/evidence.png");
        run.AltText.Should().Be("Contract evidence");
        run.Caption.Should().Be("Figure 1: Contract evidence");
        run.IsDecorative.Should().BeTrue();
        run.LinkUrl.Should().Be("https://example.test/evidence");
        run.Size.Width.Should().Be(320);
        run.NaturalSize.Width.Should().Be(640);
        run.Layout.Kind.Should().Be(DocumentObjectLayoutKind.Anchored);
        run.Layout.Anchor.BlockId.Should().Be("paragraph-1");
        run.Layout.Anchor.InlineIndex.Should().Be(1);
        run.Layout.Anchor.Offset.Should().Be(12);
        run.Layout.Wrap.Mode.Should().Be(DocumentWrapMode.Square);
        run.Layout.Position.HorizontalAlignment.Should().Be(DocumentImageHorizontalPosition.Right);
        run.Layout.Transform.Width.Should().Be(320);
        run.Metadata.Should().ContainKey("origin").WhoseValue.Should().Be("phase-1");
    }

    [Fact]
    public void DrawingRun_AnchorCanTargetParentParagraphOffset()
    {
        var run = new DocumentDrawingRun
        {
            ObjectId = "anchored-drawing",
            Layout = DocumentObjectLayout.Anchored(DocumentWrapMode.Square, DocumentImageHorizontalPosition.Left)
        };
        run.Layout.Anchor.BlockId = "paragraph-parent";
        run.Layout.Anchor.InlineIndex = 2;
        run.Layout.Anchor.Offset = 18;
        run.Layout.Anchor.Region = DocumentRenditionAnchorScope.Body;
        run.Layout.Anchor.LockAnchor = true;

        run.Layout.Kind.Should().Be(DocumentObjectLayoutKind.Anchored);
        run.Layout.Anchor.BlockId.Should().Be("paragraph-parent");
        run.Layout.Anchor.InlineIndex.Should().Be(2);
        run.Layout.Anchor.Offset.Should().Be(18);
        run.Layout.Anchor.Region.Should().Be(DocumentRenditionAnchorScope.Body);
        run.Layout.Anchor.LockAnchor.Should().BeTrue();
    }

    [Fact]
    public void DrawingRun_MarksStayIndependentFromAdjacentTextRuns()
    {
        var text = new TextRun
        {
            Text = "Bold text",
            Marks = [new InlineMark { Type = InlineMarkType.Bold }]
        };
        var drawing = new DocumentDrawingRun
        {
            ObjectId = "drawing-with-independent-marks"
        };

        drawing.Marks.Should().BeEmpty();
        drawing.Marks.Should().NotBeSameAs(text.Marks);
    }
}
