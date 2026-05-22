using FluentAssertions;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;

namespace Tempo.Blazor.Tests.Models.DocumentEditor;

public class DocumentLayoutEngineTests
{
    [Fact]
    public void Layout_EmptyDocument_ReturnsSingleEmptyPage()
    {
        var document = DocumentEditorDocument.Empty("empty-layout");

        var snapshot = new DocumentLayoutEngine().Layout(document, TestPageSettings());

        snapshot.DocumentId.Should().Be("empty-layout");
        snapshot.Pages.Should().ContainSingle();
        snapshot.Pages[0].Paragraphs.Should().BeEmpty();
        snapshot.Pages[0].Objects.Should().BeEmpty();
        snapshot.Pages[0].Exclusions.Should().BeEmpty();
    }

    [Fact]
    public void Layout_OneParagraphWithoutImages_CreatesParagraphLineAndTextSegment()
    {
        var document = Document("paragraph-layout");
        document.Blocks.Add(Paragraph("p1", "Hello layout world."));

        var snapshot = new DocumentLayoutEngine().Layout(document, TestPageSettings());

        var paragraph = snapshot.Pages[0].Paragraphs.Should().ContainSingle().Subject;
        paragraph.BlockId.Should().Be("p1");
        paragraph.Lines.Should().ContainSingle();
        paragraph.Lines[0].Segments.Should().ContainSingle();
        paragraph.Lines[0].Segments[0].Text.Should().Be("Hello layout world.");
        paragraph.Lines[0].AvailableIntervals.Should().ContainSingle();
    }

    [Fact]
    public void Layout_InlineImage_CreatesObjectWithoutExclusion()
    {
        var document = Document("inline-image-layout");
        document.Blocks.Add(Image("img-inline", DocumentObjectLayout.Inline(), width: 96, height: 48));

        var snapshot = new DocumentLayoutEngine().Layout(document, TestPageSettings());

        var page = snapshot.Pages[0];
        page.Objects.Should().ContainSingle();
        page.Objects[0].ObjectRect.Width.Should().Be(96);
        page.Objects[0].ObjectRect.Height.Should().Be(48);
        page.Exclusions.Should().BeEmpty();
        page.Paragraphs.Should().ContainSingle();
        page.Paragraphs[0].Lines[0].Rect.Height.Should().BeGreaterThanOrEqualTo(48);
    }

    [Fact]
    public void Layout_AnchoredSquareImage_CreatesExclusionAndTextStartsBesideImage()
    {
        var document = Document("anchored-square-layout");
        document.Blocks.Add(Image("img-left", LeftSquareLayout(width: 100, height: 80), width: 100, height: 80, order: 0));
        document.Blocks.Add(Paragraph("p1", LongWrapText(), order: 1));

        var snapshot = new DocumentLayoutEngine().Layout(document, TestPageSettings());

        var page = snapshot.Pages[0];
        page.Objects.Should().ContainSingle();
        page.Exclusions.Should().ContainSingle();
        var firstLine = FirstLine(page, "p1");
        firstLine.AvailableIntervals.Should().ContainSingle();
        firstLine.AvailableIntervals[0].X.Should().BeGreaterThan(page.BodyRect.X);
        firstLine.Segments[0].Rect.X.Should().Be(firstLine.AvailableIntervals[0].X);
    }

    [Fact]
    public void Layout_LeftSquareImage_AllowsAtLeastThreeLinesBesideImage()
    {
        var document = Document("three-lines-beside-image");
        document.Blocks.Add(Image("img-left", LeftSquareLayout(width: 110, height: 90), width: 110, height: 90, order: 0));
        document.Blocks.Add(Paragraph("p1", LongWrapText() + " " + LongWrapText(), order: 1));

        var snapshot = new DocumentLayoutEngine().Layout(document, TestPageSettings());
        var page = snapshot.Pages[0];

        var restrictedLines = Lines(page, "p1")
            .Where(line => line.AvailableIntervals.Count == 1 && line.AvailableIntervals[0].X > page.BodyRect.X)
            .ToList();

        restrictedLines.Should().HaveCountGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void Layout_LeftSquareImage_ReturnsToFullWidthAfterImageFootprintEnds()
    {
        var document = Document("full-width-after-image");
        document.Blocks.Add(Image("img-left", LeftSquareLayout(width: 110, height: 42), width: 110, height: 42, order: 0));
        document.Blocks.Add(Paragraph("p1", LongWrapText() + " " + LongWrapText() + " " + LongWrapText(), order: 1));

        var page = new DocumentLayoutEngine().Layout(document, TestPageSettings()).Pages[0];
        var image = page.Objects.Single(box => box.BlockId == "img-left");
        var firstLineAfterImage = Lines(page, "p1").First(line => line.Rect.Y >= image.WrapRect.Bottom - 0.01);

        firstLineAfterImage.AvailableIntervals.Should().ContainSingle();
        firstLineAfterImage.AvailableIntervals[0].X.Should().Be(page.BodyRect.X);
        firstLineAfterImage.AvailableIntervals[0].Width.Should().Be(page.BodyRect.Width);
    }

    [Fact]
    public void Layout_LeftSquareImage_WrapsNormalTextAtWordBoundaries()
    {
        var document = Document("word-boundary-wrap");
        document.Blocks.Add(Image("img-left", LeftSquareLayout(width: 200, height: 120), width: 200, height: 120, order: 0));
        document.Blocks.Add(Paragraph(
            "p1",
            "alpha bravo charlie delta echo foxtrot golf hotel india juliet kilo lima",
            order: 1));

        var snapshot = new DocumentLayoutEngine().Layout(document, TestPageSettings());
        var page = snapshot.Pages[0];
        var restrictedTexts = Lines(page, "p1")
            .Where(line => line.AvailableIntervals.Count == 1 && line.AvailableIntervals[0].X > page.BodyRect.X)
            .Select(line => string.Concat(line.Segments.Select(segment => segment.Text)))
            .Where(text => !string.IsNullOrEmpty(text))
            .ToList();

        restrictedTexts.Should().HaveCountGreaterThan(1);
        for (var index = 0; index < restrictedTexts.Count - 1; index++)
        {
            var previous = restrictedTexts[index];
            var next = restrictedTexts[index + 1];
            (char.IsLetterOrDigit(previous[^1]) && char.IsLetterOrDigit(next[0])).Should().BeFalse(
                "normal wrapped text should not split a word at line {0}: '{1}' / '{2}'",
                index,
                previous,
                next);
        }
    }

    [Fact]
    public void Layout_ImageMovedLower_ChangesFirstLineFromWrappedToFullWidth()
    {
        var atTop = Document("image-top");
        atTop.Blocks.Add(Image("img", LeftSquareLayout(width: 100, height: 80, y: 0), width: 100, height: 80, order: 0));
        atTop.Blocks.Add(Paragraph("p", LongWrapText(), order: 1));

        var lower = Document("image-lower");
        lower.Blocks.Add(Image("img", LeftSquareLayout(width: 100, height: 80, y: 80), width: 100, height: 80, order: 0));
        lower.Blocks.Add(Paragraph("p", LongWrapText(), order: 1));

        var engine = new DocumentLayoutEngine();
        var topFirstLine = FirstLine(engine.Layout(atTop, TestPageSettings()).Pages[0], "p");
        var lowerFirstLine = FirstLine(engine.Layout(lower, TestPageSettings()).Pages[0], "p");

        topFirstLine.AvailableIntervals[0].X.Should().BeGreaterThan(50);
        lowerFirstLine.AvailableIntervals[0].X.Should().Be(50);
    }

    [Fact]
    public void Layout_ImageHeightIncrease_RestrictsMoreTextLines()
    {
        var shortImage = Document("short-image");
        shortImage.Blocks.Add(Image("img", LeftSquareLayout(width: 100, height: 36), width: 100, height: 36, order: 0));
        shortImage.Blocks.Add(Paragraph("p", LongWrapText() + " " + LongWrapText(), order: 1));

        var tallImage = Document("tall-image");
        tallImage.Blocks.Add(Image("img", LeftSquareLayout(width: 100, height: 120), width: 100, height: 120, order: 0));
        tallImage.Blocks.Add(Paragraph("p", LongWrapText() + " " + LongWrapText(), order: 1));

        var engine = new DocumentLayoutEngine();
        var shortRestricted = CountRestrictedLines(engine.Layout(shortImage, TestPageSettings()).Pages[0], "p");
        var tallRestricted = CountRestrictedLines(engine.Layout(tallImage, TestPageSettings()).Pages[0], "p");

        tallRestricted.Should().BeGreaterThan(shortRestricted);
    }

    [Fact]
    public void Layout_MoveWithTextImage_FollowsAnchorParagraphWhenContentAboveGrows()
    {
        var shortDocument = AnchoredImageAfterIntro("move-short", LongWrapText());
        var longDocument = AnchoredImageAfterIntro("move-long", LongWrapText() + " " + LongWrapText() + " " + LongWrapText());
        var engine = new DocumentLayoutEngine();

        var shortImage = engine.Layout(shortDocument, TestPageSettings()).Pages[0].Objects.Single(box => box.BlockId == "img");
        var longImage = engine.Layout(longDocument, TestPageSettings()).Pages[0].Objects.Single(box => box.BlockId == "img");

        shortImage.AnchorBlockId.Should().Be("anchor");
        shortImage.Layout.Anchor.PageIndex.Should().Be(0);
        longImage.ObjectRect.Y.Should().BeGreaterThan(shortImage.ObjectRect.Y + 10);
    }

    [Fact]
    public void Layout_FixedOnPageImage_StaysOnPageWhenContentAboveAnchorGrows()
    {
        var shortDocument = AnchoredImageAfterIntro("fixed-short", LongWrapText(), fixedOnPage: true);
        var longDocument = AnchoredImageAfterIntro("fixed-long", LongWrapText() + " " + LongWrapText() + " " + LongWrapText(), fixedOnPage: true);
        var engine = new DocumentLayoutEngine();

        var shortImage = engine.Layout(shortDocument, TestPageSettings()).Pages[0].Objects.Single(box => box.BlockId == "img");
        var longImage = engine.Layout(longDocument, TestPageSettings()).Pages[0].Objects.Single(box => box.BlockId == "img");

        shortImage.Layout.Anchor.FixedOnPage.Should().BeTrue();
        shortImage.ObjectRect.Y.Should().BeApproximately(longImage.ObjectRect.Y, 0.01);
    }

    [Fact]
    public void Layout_LongWord_WrapsAcrossMultipleLines()
    {
        var document = Document("long-word");
        document.Blocks.Add(Paragraph("p", "SupercalifragilisticexpialidociousSupercalifragilisticexpialidocious"));

        var snapshot = new DocumentLayoutEngine().Layout(document, NarrowPageSettings());

        Lines(snapshot.Pages[0], "p").Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public void Layout_ExplicitLineBreak_CreatesSeparateVisualLines()
    {
        var document = Document("line-break");
        document.Blocks.Add(Paragraph("p", "Alpha\nBeta"));

        var snapshot = new DocumentLayoutEngine().Layout(document, TestPageSettings());

        Lines(snapshot.Pages[0], "p").Should().HaveCount(2);
        Lines(snapshot.Pages[0], "p")[0].Segments[0].Text.Should().Be("Alpha");
        Lines(snapshot.Pages[0], "p")[1].Segments[0].Text.Should().Be("Beta");
    }

    [Fact]
    public void Layout_RevisionInlineSegmentsCarryRevisionMarks()
    {
        var document = Document("revision-segment-marks");
        const string approvedText = "The provider will deliver implementation, training, and documentation services.";
        const string revisionText = " Priority support is included during the first thirty days.";
        document.Blocks.Add(new DocumentBlock
        {
            Id = "contract-scope",
            Type = DocumentBlockType.Paragraph,
            Content = new ParagraphBlockContent
            {
                Inlines =
                [
                    new TextRun
                    {
                        Id = "approved-run",
                        Text = approvedText
                    },
                    new TextRun
                    {
                        Id = "revision-run",
                        Text = revisionText,
                        Marks =
                        [
                            new InlineMark
                            {
                                Type = InlineMarkType.Revision,
                                RevisionId = "contract-revision-scope",
                                Value = "Insertion"
                            }
                        ]
                    }
                ]
            }
        });

        var page = new DocumentLayoutEngine().Layout(document, TestPageSettings()).Pages[0];
        var segments = Lines(page, "contract-scope").SelectMany(line => line.Segments).ToList();

        segments.Where(segment => segment.InlineId == "approved-run")
            .Should()
            .OnlyContain(segment => segment.Marks.All(mark => mark.Type != InlineMarkType.Revision));
        segments.Where(segment => segment.InlineId == "revision-run")
            .Should()
            .OnlyContain(segment => segment.Marks.Any(mark =>
                mark.Type == InlineMarkType.Revision
                && mark.RevisionId == "contract-revision-scope"
                && mark.Value == "Insertion"));
        segments.Where(segment => segment.InlineId == "revision-run")
            .Should()
            .OnlyContain(segment => segment.BlockStartOffset >= approvedText.Length);
        string.Concat(segments.Select(segment => segment.Text))
            .Should()
            .Be(approvedText + revisionText);
    }

    [Fact]
    public void Layout_LineSpacingAndParagraphSpacing_AffectLineAndNextParagraphY()
    {
        var document = Document("spacing");
        document.Blocks.Add(Paragraph("p1", "First", order: 0, props: new DocumentParagraphProperties
        {
            LineSpacing = 2,
            SpacingAfter = 20
        }));
        document.Blocks.Add(Paragraph("p2", "Second", order: 1));

        var snapshot = new DocumentLayoutEngine().Layout(document, TestPageSettings());
        var first = FirstLine(snapshot.Pages[0], "p1");
        var second = FirstLine(snapshot.Pages[0], "p2");

        first.Rect.Height.Should().BeGreaterThan(20);
        second.Rect.Y.Should().BeApproximately(first.Rect.Bottom + 20, 0.01);
    }

    [Fact]
    public void Layout_LongText_ContinuesOnSecondPage()
    {
        var document = Document("multi-page");
        document.Blocks.Add(Paragraph("p", string.Join(' ', Enumerable.Repeat("paragraph text wraps", 80))));

        var snapshot = new DocumentLayoutEngine().Layout(document, SmallPageSettings());

        snapshot.Pages.Should().HaveCountGreaterThan(1);
        snapshot.Pages.SelectMany(page => page.Paragraphs)
            .Where(paragraph => paragraph.BlockId == "p")
            .Select(paragraph => paragraph.PageIndex)
            .Should().Contain(1);
    }

    [Fact]
    public void Layout_AnchoredObjectOutsideBody_IsClampedAndDiagnosticIsRecorded()
    {
        var document = Document("clamped-image");
        document.Blocks.Add(Image("img", LeftSquareLayout(width: 100, height: 80, y: 999), width: 100, height: 80));

        var snapshot = new DocumentLayoutEngine().Layout(document, TestPageSettings());
        var page = snapshot.Pages[0];

        page.Objects[0].ObjectRect.Bottom.Should().BeLessThanOrEqualTo(page.BodyRect.Bottom);
        snapshot.Diagnostics.Should().Contain(message => message.Contains("clamped", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Layout_TopBottomImageFillingBody_MovesFollowingTextToNextPage()
    {
        var document = Document("top-bottom-boundary");
        var layout = TopBottomLayout(width: 180, height: 120);
        document.Blocks.Add(Image("img", layout, width: 180, height: 120, order: 0));
        document.Blocks.Add(Paragraph("p", "Text after a top and bottom image.", order: 1));

        var snapshot = new DocumentLayoutEngine().Layout(document, SmallPageSettings());

        snapshot.Pages[0].Exclusions.Should().ContainSingle();
        FirstLine(snapshot.Pages[1], "p").AvailableIntervals.Should().ContainSingle();
        FirstLine(snapshot.Pages[1], "p").AvailableIntervals[0].X.Should().Be(snapshot.Pages[1].BodyRect.X);
    }

    [Fact]
    public void Layout_FullWidthTopBottomImage_MovesFollowingTextBelowExclusion()
    {
        var document = Document("top-bottom-full-width");
        document.Blocks.Add(Image("img", TopBottomLayout(width: 220, height: 70), width: 220, height: 70, order: 0));
        document.Blocks.Add(Paragraph("p", LongWrapText(), order: 1));

        var page = new DocumentLayoutEngine().Layout(document, TestPageSettings()).Pages[0];
        var image = page.Objects.Single(box => box.BlockId == "img");
        var firstLine = FirstLine(page, "p");

        page.Exclusions.Single(zone => zone.BlockId == "img").Rect.Width.Should().Be(page.BodyRect.Width);
        firstLine.Rect.Y.Should().BeGreaterThanOrEqualTo(image.WrapRect.Bottom);
        firstLine.AvailableIntervals.Should().ContainSingle();
        firstLine.AvailableIntervals[0].X.Should().Be(page.BodyRect.X);
    }

    [Fact]
    public void Layout_AllowOverlapFalseMovesLaterObjectWithinSameLayer()
    {
        var document = Document("object-overlap");
        document.Blocks.Add(Image("img-1", LeftSquareLayout(width: 100, height: 80), width: 100, height: 80, order: 0));
        document.Blocks.Add(Image("img-2", LeftSquareLayout(width: 100, height: 80), width: 100, height: 80, order: 1));

        var page = new DocumentLayoutEngine().Layout(document, TestPageSettings()).Pages[0];

        var first = page.Objects.Single(box => box.BlockId == "img-1");
        var second = page.Objects.Single(box => box.BlockId == "img-2");
        first.AllowOverlap.Should().BeFalse();
        second.ObjectRect.Y.Should().BeGreaterThanOrEqualTo(first.ObjectRect.Bottom + 8);
        DocumentLayoutGeometryHelper.Intersects(first.ObjectRect, second.ObjectRect).Should().BeFalse();
    }

    [Fact]
    public void Layout_AllowOverlapTrueKeepsObjectsOverlapping()
    {
        var firstLayout = LeftSquareLayout(width: 100, height: 80);
        firstLayout.Stacking.AllowOverlap = true;
        var secondLayout = LeftSquareLayout(width: 100, height: 80);
        secondLayout.Stacking.AllowOverlap = true;
        var document = Document("object-overlap-allowed");
        document.Blocks.Add(Image("img-1", firstLayout, width: 100, height: 80, order: 0));
        document.Blocks.Add(Image("img-2", secondLayout, width: 100, height: 80, order: 1));

        var page = new DocumentLayoutEngine().Layout(document, TestPageSettings()).Pages[0];

        var first = page.Objects.Single(box => box.BlockId == "img-1");
        var second = page.Objects.Single(box => box.BlockId == "img-2");
        first.AllowOverlap.Should().BeTrue();
        second.ObjectRect.Y.Should().BeApproximately(first.ObjectRect.Y, 0.01);
        DocumentLayoutGeometryHelper.Intersects(first.ObjectRect, second.ObjectRect).Should().BeTrue();
    }

    [Fact]
    public void Layout_ImageWithoutCaptionKeepsFootprintEqualToMediaAndWrapUsesDistances()
    {
        var layout = LeftSquareLayout(width: 100, height: 60);
        layout.Wrap.DistanceLeft = 4;
        layout.Wrap.DistanceRight = 10;
        layout.Wrap.DistanceTop = 6;
        layout.Wrap.DistanceBottom = 12;
        var document = Document("image-footprint-without-caption");
        document.Blocks.Add(Image("img", layout, width: 100, height: 60, order: 0));

        var image = new DocumentLayoutEngine().Layout(document, TestPageSettings()).Pages[0].Objects.Single(box => box.BlockId == "img");

        image.CaptionRect.IsEmpty.Should().BeTrue();
        image.FootprintRect.X.Should().Be(image.MediaRect.X);
        image.FootprintRect.Y.Should().Be(image.MediaRect.Y);
        image.FootprintRect.Width.Should().Be(image.MediaRect.Width);
        image.FootprintRect.Height.Should().Be(image.MediaRect.Height);
        image.WrapRect.X.Should().Be(image.MediaRect.X - 4);
        image.WrapRect.Y.Should().Be(image.MediaRect.Y - 6);
        image.WrapRect.Right.Should().Be(image.MediaRect.Right + 10);
        image.WrapRect.Bottom.Should().Be(image.MediaRect.Bottom + 12);
    }

    [Fact]
    public void Layout_TopBottomImageWithCaption_ExclusionUsesCaptionFootprint()
    {
        var document = Document("top-bottom-caption-footprint");
        document.Blocks.Add(Image(
            "img",
            TopBottomLayout(width: 120, height: 50),
            width: 120,
            height: 50,
            order: 0,
            caption: "Long enough caption to make the footprint taller than the media rectangle"));

        var page = new DocumentLayoutEngine().Layout(document, TestPageSettings()).Pages[0];
        var image = page.Objects.Single(box => box.BlockId == "img");
        var zone = page.Exclusions.Single(zone => zone.BlockId == "img");

        image.CaptionRect.Height.Should().BeGreaterThan(0);
        image.FootprintRect.Bottom.Should().BeGreaterThan(image.MediaRect.Bottom);
        zone.Rect.Bottom.Should().Be(image.WrapRect.Bottom);
    }

    [Fact]
    public void Layout_TableFallbackAfterLeftSquare_MovesBelowWhenFullWidthWouldCollide()
    {
        var document = Document("table-after-left-square");
        document.Blocks.Add(Image("img", LeftSquareLayout(width: 220, height: 80), width: 220, height: 80, order: 0));
        document.Blocks.Add(Table("table", order: 1));

        var page = new DocumentLayoutEngine().Layout(document, TestPageSettings()).Pages[0];
        var image = page.Objects.Single(box => box.BlockId == "img");
        var table = page.Paragraphs.Single(paragraph => paragraph.BlockId == "table");

        table.Rect.Y.Should().BeGreaterThanOrEqualTo(image.WrapRect.Bottom);
        table.Rect.X.Should().Be(page.BodyRect.X);
        table.Rect.Width.Should().Be(page.BodyRect.Width);
    }

    [Fact]
    public void Layout_RotatedImageUsesConservativeBoundingFootprint()
    {
        var layout = LeftSquareLayout(width: 120, height: 60);
        layout.Transform.Rotation = 45;
        var document = Document("rotated-image-footprint");
        document.Blocks.Add(Image("img", layout, width: 120, height: 60, order: 0));

        var image = new DocumentLayoutEngine().Layout(document, TestPageSettings()).Pages[0].Objects.Single(box => box.BlockId == "img");

        image.MediaRect.Width.Should().Be(120);
        image.MediaRect.Height.Should().Be(60);
        image.FootprintRect.Width.Should().BeGreaterThan(image.MediaRect.Width);
        image.FootprintRect.Height.Should().BeGreaterThan(image.MediaRect.Height);
        image.WrapRect.Width.Should().BeGreaterThan(image.MediaRect.Width);
    }

    [Fact]
    public void Layout_CropDoesNotChangeLayoutFootprint()
    {
        var croppedLayout = LeftSquareLayout(width: 120, height: 60);
        croppedLayout.Transform.Crop = new DocumentObjectCrop { Left = 12, Top = 6, Right = 10, Bottom = 4 };
        var uncroppedDocument = Document("uncropped-image-footprint");
        uncroppedDocument.Blocks.Add(Image("img", LeftSquareLayout(width: 120, height: 60), width: 120, height: 60, order: 0));
        var croppedDocument = Document("cropped-image-footprint");
        croppedDocument.Blocks.Add(Image("img", croppedLayout, width: 120, height: 60, order: 0));

        var engine = new DocumentLayoutEngine();
        var uncropped = engine.Layout(uncroppedDocument, TestPageSettings()).Pages[0].Objects.Single(box => box.BlockId == "img");
        var cropped = engine.Layout(croppedDocument, TestPageSettings()).Pages[0].Objects.Single(box => box.BlockId == "img");

        cropped.FootprintRect.X.Should().Be(uncropped.FootprintRect.X);
        cropped.FootprintRect.Y.Should().Be(uncropped.FootprintRect.Y);
        cropped.FootprintRect.Width.Should().Be(uncropped.FootprintRect.Width);
        cropped.FootprintRect.Height.Should().Be(uncropped.FootprintRect.Height);
    }

    [Fact]
    public void Layout_LockedAspectRatioResizePreservesFootprintAndExclusion()
    {
        var layout = LeftSquareLayout(width: 180, height: 90);
        layout.Transform.NaturalWidth = 120;
        layout.Transform.NaturalHeight = 60;
        layout.Transform.LockAspectRatio = true;
        layout.Wrap.DistanceRight = 14;
        layout.Wrap.DistanceBottom = 8;
        var document = Document("locked-aspect-resize-footprint");
        document.Blocks.Add(Image("img", layout, width: 180, height: 90, order: 0));

        var page = new DocumentLayoutEngine().Layout(document, TestPageSettings()).Pages[0];
        var image = page.Objects.Single(box => box.BlockId == "img");
        var exclusion = page.Exclusions.Single(zone => zone.BlockId == "img");

        image.Layout.Transform.LockAspectRatio.Should().BeTrue();
        image.MediaRect.Width.Should().Be(180);
        image.MediaRect.Height.Should().Be(90);
        image.FootprintRect.Width.Should().Be(180);
        image.FootprintRect.Height.Should().Be(90);
        exclusion.Rect.Right.Should().Be(image.MediaRect.Right + 14);
        exclusion.Rect.Bottom.Should().Be(image.MediaRect.Bottom + 8);
    }

    [Fact]
    public void Layout_FixedOnPageObjectDoesNotAffectMoveWithTextObjectSemantics()
    {
        var shortDocument = AnchoredImagePairAfterIntro("fixed-move-short", LongWrapText());
        var longDocument = AnchoredImagePairAfterIntro("fixed-move-long", LongWrapText() + " " + LongWrapText() + " " + LongWrapText());
        var engine = new DocumentLayoutEngine();

        var shortPage = engine.Layout(shortDocument, TestPageSettings()).Pages[0];
        var longPage = engine.Layout(longDocument, TestPageSettings()).Pages[0];
        var shortFixed = shortPage.Objects.Single(box => box.BlockId == "fixed-img");
        var longFixed = longPage.Objects.Single(box => box.BlockId == "fixed-img");
        var shortMove = shortPage.Objects.Single(box => box.BlockId == "move-img");
        var longMove = longPage.Objects.Single(box => box.BlockId == "move-img");

        shortFixed.Layout.Anchor.FixedOnPage.Should().BeTrue();
        shortMove.Layout.Anchor.MoveWithText.Should().BeTrue();
        longFixed.ObjectRect.Y.Should().BeApproximately(shortFixed.ObjectRect.Y, 0.01);
        longMove.ObjectRect.Y.Should().BeGreaterThan(shortMove.ObjectRect.Y + 10);
    }

    [Fact]
    public void Layout_DefaultContractLikeWrappedImage_DoesNotOverlapFollowingInlineImages()
    {
        var document = Document("contract-like-wrap");
        document.Blocks.Add(Paragraph("title", "Service agreement", order: 0));
        document.Blocks.Add(Paragraph(
            "pending-revision",
            "Priority support is included during the first thirty days.",
            order: 0.5));
        document.Blocks.Add(Image(
            "wrapped-image",
            LeftSquareLayout(width: 160, height: 90),
            width: 160,
            height: 90,
            order: 1,
            caption: "Image loaded from favicon resolver"));
        document.Blocks.Add(Paragraph(
            "wrapped-text",
            "This longer clause demonstrates live text wrapping around the evidence image. "
            + "It should continue beside the image without intersecting the media or its caption.",
            order: 2));
        document.Blocks.Add(Image(
            "inline-image",
            DocumentObjectLayout.Inline(),
            width: 160,
            height: 90,
            order: 3,
            caption: "Inline provider image caption"));
        document.Blocks.Add(Image(
            "provider-image",
            DocumentObjectLayout.Inline(),
            width: 150,
            height: 84,
            order: 4,
            caption: "Image resolved through provider"));

        var snapshot = new DocumentLayoutEngine().Layout(document, TestPageSettings());
        var page = snapshot.Pages[0];

        var wrappedImage = page.Objects.Single(box => box.BlockId == "wrapped-image");
        wrappedImage.FootprintRect.Height.Should().BeGreaterThan(wrappedImage.ObjectRect.Height);
        DocumentLayoutGeometryHelper.GetObjectFootprintRect(wrappedImage).Should().BeSameAs(wrappedImage.FootprintRect);
        page.Exclusions.Single(zone => zone.BlockId == "wrapped-image").Rect.Bottom.Should().BeGreaterThan(wrappedImage.ObjectRect.Bottom);

        Lines(page, "wrapped-text")
            .Should()
            .OnlyContain(line => !DocumentLayoutGeometryHelper.Intersects(line.Rect, wrappedImage.WrapRect));

        foreach (var imageBox in page.Objects.Where(box => box.BlockId is "inline-image" or "provider-image"))
        {
            DocumentLayoutGeometryHelper.Intersects(imageBox.FootprintRect, wrappedImage.WrapRect)
                .Should()
                .BeFalse($"{imageBox.BlockId} must be placed into a free interval or below the active wrapped image footprint");
        }

        snapshot.Diagnostics.Should().NotContain(message => message.Contains("intersects active exclusion", StringComparison.OrdinalIgnoreCase));
        snapshot.DebugBlockLayouts.Select(debug => debug.BlockId).Should().Contain(["title", "pending-revision", "wrapped-image", "wrapped-text", "inline-image", "provider-image"]);
        snapshot.DebugObjectLayouts.Should().Contain(debug => debug.BlockId == "wrapped-image"
            && debug.WrapMode == DocumentWrapMode.Square
            && !debug.FootprintRect.IsEmpty
            && !debug.WrapRect.IsEmpty
            && debug.CaptionRect.Height > 0);
        snapshot.DebugLineLayouts.Should().Contain(debug => debug.BlockId == "wrapped-text"
            && debug.AvailableIntervals.Count > 0
            && debug.ExclusionRects.Count > 0
            && debug.Segments.Count > 0);
        var monotonicParagraphs = page.Paragraphs
            .Where(paragraph => paragraph.Lines.Count > 0)
            .OrderBy(paragraph => paragraph.Rect.Y)
            .ToList();
        monotonicParagraphs.Select(paragraph => paragraph.Rect.Y).Should().BeInAscendingOrder();
    }

    [Fact]
    public void Layout_FloatingImageAfterAnchorParagraph_PreAnchorsBeforeTextLayout()
    {
        var document = Document("pre-anchor-wrap");
        document.Blocks.Add(Paragraph(
            "anchor",
            "The provider will deliver implementation, training, and documentation services. "
            + "Priority support is included during the first thirty days.",
            order: 0));
        document.Blocks.Add(Image(
            "wrapped-image",
            LeftSquareLayout(width: 160, height: 90),
            width: 160,
            height: 90,
            order: 1,
            caption: "Evidence preview loaded from a URL"));

        var page = new DocumentLayoutEngine().Layout(document, TestPageSettings()).Pages[0];

        var image = page.Objects.Single(box => box.BlockId == "wrapped-image");
        image.AnchorBlockId.Should().Be("anchor");
        page.Exclusions.Single(zone => zone.BlockId == "wrapped-image").Rect.Bottom.Should().BeGreaterThan(image.ObjectRect.Bottom);
        Lines(page, "anchor")
            .SelectMany(line => line.Segments)
            .Should()
            .OnlyContain(segment => !DocumentLayoutGeometryHelper.Intersects(segment.Rect, image.WrapRect),
                "an image block that follows its anchor paragraph still has to participate in that paragraph's wrapping layout");
    }

    [Fact]
    public void Layout_FloatingImageBeforeExplicitAnchor_IsLaidOutOnceAtAnchor()
    {
        var document = Document("pre-anchor-before-explicit-anchor");
        document.Blocks.Add(Image(
            "wrapped-image",
            AnchoredToParagraphLayout("anchor", width: 160, height: 90),
            width: 160,
            height: 90,
            order: 0));
        document.Blocks.Add(Paragraph(
            "anchor",
            LongWrapText() + " " + LongWrapText(),
            order: 1));

        var page = new DocumentLayoutEngine().Layout(document, TestPageSettings()).Pages[0];

        var image = page.Objects.Should().ContainSingle(box => box.BlockId == "wrapped-image").Subject;
        image.AnchorBlockId.Should().Be("anchor");
        page.Exclusions.Should().ContainSingle(zone => zone.BlockId == "wrapped-image");
        Lines(page, "anchor")
            .Where(line => line.Rect.Y < image.WrapRect.Bottom && line.Rect.Bottom > image.WrapRect.Y)
            .Should()
            .OnlyContain(line => line.Segments.All(segment => !DocumentLayoutGeometryHelper.Intersects(segment.Rect, image.WrapRect)),
                "an explicitly pre-anchored image must not leave a duplicate object at its original block position");
    }

    [Fact]
    public void Layout_LeftAndRightSquareImages_LeaveMiddleIntervalWhenThereIsRoom()
    {
        var document = Document("left-right-middle-interval");
        document.Blocks.Add(Image("left", LeftSquareLayout(width: 80, height: 70), width: 80, height: 70, order: 0));
        document.Blocks.Add(Image("right", RightSquareLayout(width: 80, height: 70), width: 80, height: 70, order: 1));
        document.Blocks.Add(Paragraph("p", LongWrapText(), order: 2));

        var page = new DocumentLayoutEngine().Layout(document, TestPageSettings()).Pages[0];
        var left = page.Objects.Single(box => box.BlockId == "left");
        var right = page.Objects.Single(box => box.BlockId == "right");
        var firstLine = FirstLine(page, "p");

        firstLine.AvailableIntervals.Should().ContainSingle();
        firstLine.AvailableIntervals[0].X.Should().BeGreaterThan(left.WrapRect.Right - 0.01);
        firstLine.AvailableIntervals[0].End.Should().BeLessThan(right.WrapRect.X + 0.01);
        firstLine.Segments.Should().OnlyContain(segment => !DocumentLayoutGeometryHelper.Intersects(segment.Rect, left.WrapRect)
            && !DocumentLayoutGeometryHelper.Intersects(segment.Rect, right.WrapRect));
    }

    [Fact]
    public void Layout_LeftAndRightSquareImages_MoveLineBelowWhenMiddleIntervalIsTooSmall()
    {
        var document = Document("left-right-no-middle-interval");
        document.Blocks.Add(Image("left", LeftSquareLayout(width: 140, height: 70), width: 140, height: 70, order: 0));
        document.Blocks.Add(Image("right", RightSquareLayout(width: 140, height: 70), width: 140, height: 70, order: 1));
        document.Blocks.Add(Paragraph("p", LongWrapText(), order: 2));

        var page = new DocumentLayoutEngine().Layout(document, TestPageSettings()).Pages[0];
        var bottom = page.Objects.Where(box => box.BlockId is "left" or "right").Max(box => box.WrapRect.Bottom);
        var firstLine = FirstLine(page, "p");

        firstLine.Rect.Y.Should().BeGreaterThanOrEqualTo(bottom);
        firstLine.AvailableIntervals.Should().ContainSingle();
        firstLine.AvailableIntervals[0].X.Should().Be(page.BodyRect.X);
        firstLine.AvailableIntervals[0].Width.Should().Be(page.BodyRect.Width);
    }

    [Fact]
    public void Layout_TwoLeftSquareImages_StackWithoutObjectOrTextOverlap()
    {
        var document = Document("two-left-square-images");
        document.Blocks.Add(Image("img-1", LeftSquareLayout(width: 110, height: 70), width: 110, height: 70, order: 0));
        document.Blocks.Add(Image("img-2", LeftSquareLayout(width: 110, height: 70), width: 110, height: 70, order: 1));
        document.Blocks.Add(Paragraph("p", LongWrapText() + " " + LongWrapText(), order: 2));

        var page = new DocumentLayoutEngine().Layout(document, TestPageSettings()).Pages[0];
        var first = page.Objects.Single(box => box.BlockId == "img-1");
        var second = page.Objects.Single(box => box.BlockId == "img-2");

        DocumentLayoutGeometryHelper.Intersects(first.FootprintRect, second.FootprintRect).Should().BeFalse();
        second.FootprintRect.Y.Should().BeGreaterThanOrEqualTo(first.FootprintRect.Bottom + 8);
        Lines(page, "p").SelectMany(line => line.Segments)
            .Should()
            .OnlyContain(segment => !DocumentLayoutGeometryHelper.Intersects(segment.Rect, first.WrapRect)
                && !DocumentLayoutGeometryHelper.Intersects(segment.Rect, second.WrapRect));
    }

    [Fact]
    public void Layout_InlineImageAfterLeftSquare_MovesBelowWhenItDoesNotFitBesideImage()
    {
        var document = Document("inline-image-too-wide-for-side-interval");
        document.Blocks.Add(Image("wrapped", LeftSquareLayout(width: 220, height: 90), width: 220, height: 90, order: 0));
        document.Blocks.Add(Image("inline", DocumentObjectLayout.Inline(), width: 150, height: 70, order: 1, caption: "Inline caption"));

        var page = new DocumentLayoutEngine().Layout(document, TestPageSettings()).Pages[0];
        var wrapped = page.Objects.Single(box => box.BlockId == "wrapped");
        var inline = page.Objects.Single(box => box.BlockId == "inline");

        inline.FootprintRect.Y.Should().BeGreaterThanOrEqualTo(wrapped.WrapRect.Bottom);
        inline.FootprintRect.X.Should().BeGreaterThanOrEqualTo(page.BodyRect.X);
        DocumentLayoutGeometryHelper.Intersects(inline.FootprintRect, wrapped.WrapRect).Should().BeFalse();
    }

    [Fact]
    public void Layout_LongCaptionIncreasesFootprintByMultipleCaptionLines()
    {
        var shortDocument = Document("short-caption");
        shortDocument.Blocks.Add(Image("img", LeftSquareLayout(width: 90, height: 50), width: 90, height: 50, order: 0, caption: "Short caption"));

        var longDocument = Document("long-caption");
        longDocument.Blocks.Add(Image(
            "img",
            LeftSquareLayout(width: 90, height: 50),
            width: 90,
            height: 50,
            order: 0,
            caption: "This caption is intentionally long enough to wrap over multiple caption lines in the layout footprint"));

        var engine = new DocumentLayoutEngine();
        var shortImage = engine.Layout(shortDocument, TestPageSettings()).Pages[0].Objects.Single(box => box.BlockId == "img");
        var longImage = engine.Layout(longDocument, TestPageSettings()).Pages[0].Objects.Single(box => box.BlockId == "img");

        longImage.CaptionRect.Height.Should().BeGreaterThan(shortImage.CaptionRect.Height);
        longImage.FootprintRect.Height.Should().BeGreaterThan(shortImage.FootprintRect.Height);
        longImage.WrapRect.Height.Should().BeGreaterThan(shortImage.WrapRect.Height);
    }

    [Fact]
    public void Layout_BehindAndInFrontImages_DoNotCreateTextExclusions()
    {
        var behind = LeftSquareLayout(width: 120, height: 70);
        behind.Wrap.Mode = DocumentWrapMode.BehindText;
        var inFront = RightSquareLayout(width: 120, height: 70);
        inFront.Wrap.Mode = DocumentWrapMode.InFrontOfText;
        var document = Document("non-blocking-overlap-modes");
        document.Blocks.Add(Image("behind", behind, width: 120, height: 70, order: 0));
        document.Blocks.Add(Image("front", inFront, width: 120, height: 70, order: 1));
        document.Blocks.Add(Paragraph("p", LongWrapText(), order: 2));

        var page = new DocumentLayoutEngine().Layout(document, TestPageSettings()).Pages[0];
        var firstLine = FirstLine(page, "p");

        page.Exclusions.Should().BeEmpty();
        firstLine.AvailableIntervals.Should().ContainSingle();
        firstLine.AvailableIntervals[0].X.Should().Be(page.BodyRect.X);
        firstLine.AvailableIntervals[0].Width.Should().Be(page.BodyRect.Width);
    }

    [Fact]
    public void ApproximateTextMeasurer_CachesByFontAndReturnsDifferentBoldItalicWidths()
    {
        var measurer = new ApproximateDocumentTextMeasurer();
        var normal = measurer.Measure(new DocumentTextMeasurementRequest { Text = "Measure me", FontSize = 12, FontFamily = "Arial" });
        var normalAgain = measurer.Measure(new DocumentTextMeasurementRequest { Text = "Measure me", FontSize = 12, FontFamily = "Arial" });
        var boldItalic = measurer.Measure(new DocumentTextMeasurementRequest
        {
            Text = "Measure me",
            FontSize = 12,
            FontFamily = "Arial",
            FontWeight = "700",
            FontStyle = "italic"
        });

        normal.Width.Should().BeGreaterThan(0);
        normalAgain.Width.Should().Be(normal.Width);
        boldItalic.Width.Should().BeGreaterThan(normal.Width);
        measurer.GetCacheStats().CacheHits.Should().Be(1);

        measurer.ClearCache();
        measurer.GetCacheStats().Invalidations.Should().Be(1);
        measurer.GetCacheStats().CacheSize.Should().Be(0);
    }

    [Fact]
    public void Layout_PerformanceMetrics_RecordPassDurationAndTextMeasurementCacheRatio()
    {
        var document = Document("layout-performance");
        document.Blocks.Add(Paragraph("p1", "aaaa aaaa aaaa"));

        var snapshot = new DocumentLayoutEngine().Layout(document, TestPageSettings());

        snapshot.Performance.LayoutPassMs.Should().BeGreaterThanOrEqualTo(0);
        snapshot.Performance.TextMeasureCount.Should().BeGreaterThan(0);
        snapshot.Performance.TextMeasureCacheHits.Should().BeGreaterThan(0);
        snapshot.Performance.TextMeasureCacheHitRatio.Should().BeGreaterThan(0);
        snapshot.Performance.Reason.Should().Be(DocumentLayoutInvalidationReason.Unknown.ToString());
    }

    [Fact]
    public void Layout_ImageDragAndResizeInvalidation_RecordReflowDurations()
    {
        var document = Document("layout-reflow");
        document.Blocks.Add(Image("img", LeftSquareLayout(width: 100, height: 80), width: 100, height: 80));
        var engine = new DocumentLayoutEngine();
        var previous = engine.Layout(document, TestPageSettings());

        var drag = engine.Layout(document, TestPageSettings(), invalidationRequest: new DocumentLayoutInvalidationRequest
        {
            Reason = DocumentLayoutInvalidationReason.ImageDragReflow,
            BlockId = "img",
            PreviousSnapshot = previous
        });
        var resize = engine.Layout(document, TestPageSettings(), invalidationRequest: new DocumentLayoutInvalidationRequest
        {
            Reason = DocumentLayoutInvalidationReason.ImageResizeReflow,
            BlockId = "img",
            PreviousSnapshot = previous
        });

        drag.Performance.ReflowAfterDragMs.Should().BeGreaterThanOrEqualTo(0);
        drag.Performance.ReflowAfterResizeMs.Should().Be(0);
        drag.Performance.InvalidatedPageIndices.Should().Equal([0]);
        resize.Performance.ReflowAfterResizeMs.Should().BeGreaterThanOrEqualTo(0);
        resize.Performance.ReflowAfterDragMs.Should().Be(0);
        resize.Performance.InvalidatedPageIndices.Should().Equal([0]);
    }

    [Fact]
    public void Invalidation_TextChange_InvalidatesChangedParagraphPageAndFollowingFlow()
    {
        var document = Document("text-invalidation");
        document.Blocks.Add(Paragraph("p1", string.Join(' ', Enumerable.Repeat("first page text", 120)), order: 0));
        document.Blocks.Add(Paragraph("p2", string.Join(' ', Enumerable.Repeat("changed paragraph text", 40)), order: 1));
        document.Blocks.Add(Paragraph("p3", "following paragraph", order: 2));
        var previous = new DocumentLayoutEngine().Layout(document, SmallPageSettings());
        var changedPage = previous.Pages.First(page => page.Paragraphs.Any(paragraph => paragraph.BlockId == "p2")).PageIndex;

        var result = DocumentLayoutInvalidationPlanner.Plan(new DocumentLayoutInvalidationRequest
        {
            Reason = DocumentLayoutInvalidationReason.TextChanged,
            BlockId = "p2",
            PreviousSnapshot = previous
        });

        result.InvalidatedPageIndices.Should().Equal(Enumerable.Range(changedPage, previous.Pages.Count - changedPage));
        result.InvalidatesModel.Should().BeTrue();
        result.InvalidatesMeasurementsOnly.Should().BeFalse();
    }

    [Fact]
    public void Invalidation_ImageChange_InvalidatesTouchedObjectPageOnly()
    {
        var document = Document("image-invalidation");
        document.Blocks.Add(Paragraph("p1", "Before", order: 0));
        document.Blocks.Add(new DocumentBlock { Type = DocumentBlockType.PageBreak, Order = 1, Content = new PageBreakBlockContent() });
        document.Blocks.Add(Image("img", LeftSquareLayout(width: 100, height: 80), width: 100, height: 80, order: 2));
        document.Blocks.Add(new DocumentBlock { Type = DocumentBlockType.PageBreak, Order = 3, Content = new PageBreakBlockContent() });
        document.Blocks.Add(Paragraph("p2", "After", order: 4));
        var previous = new DocumentLayoutEngine().Layout(document, TestPageSettings());
        var imagePage = previous.Pages.Single(page => page.Objects.Any(obj => obj.BlockId == "img")).PageIndex;

        var result = DocumentLayoutInvalidationPlanner.Plan(new DocumentLayoutInvalidationRequest
        {
            Reason = DocumentLayoutInvalidationReason.ImageChanged,
            BlockId = "img",
            PreviousSnapshot = previous
        });

        result.InvalidatedPageIndices.Should().Equal([imagePage]);
        result.InvalidatesWholeDocument.Should().BeFalse();
    }

    [Fact]
    public void Invalidation_PageLayoutAndZoom_SeparateModelAndMeasurementInvalidation()
    {
        var document = Document("layout-kind-invalidation");
        document.Blocks.Add(Paragraph("p1", string.Join(' ', Enumerable.Repeat("page text", 120)), order: 0));
        var previous = new DocumentLayoutEngine().Layout(document, SmallPageSettings());

        var pageLayout = DocumentLayoutInvalidationPlanner.Plan(new DocumentLayoutInvalidationRequest
        {
            Reason = DocumentLayoutInvalidationReason.PageLayoutChanged,
            PreviousSnapshot = previous
        });
        var zoom = DocumentLayoutInvalidationPlanner.Plan(new DocumentLayoutInvalidationRequest
        {
            Reason = DocumentLayoutInvalidationReason.ZoomChanged,
            PreviousSnapshot = previous
        });

        pageLayout.InvalidatedPageIndices.Should().Equal(Enumerable.Range(0, previous.Pages.Count));
        pageLayout.InvalidatesWholeDocument.Should().BeTrue();
        pageLayout.InvalidatesModel.Should().BeTrue();
        zoom.InvalidatedPageIndices.Should().Equal(Enumerable.Range(0, previous.Pages.Count));
        zoom.InvalidatesModel.Should().BeFalse();
        zoom.InvalidatesMeasurementsOnly.Should().BeTrue();
    }

    private static DocumentEditorDocument Document(string id)
    {
        var document = DocumentEditorDocument.Empty(id);
        document.PageSettings = TestPageSettings();
        document.Theme = new DocumentEditorTheme
        {
            BodyFontFamily = "Arial",
            BodyFontSize = 11,
            BodyLineHeight = 1.15,
            ParagraphSpacingAfter = 0
        };
        return document;
    }

    private static DocumentBlock Paragraph(
        string id,
        string text,
        double order = 0,
        DocumentParagraphProperties? props = null)
        => new()
        {
            Id = id,
            Type = DocumentBlockType.Paragraph,
            Order = order,
            ParagraphProperties = props ?? new DocumentParagraphProperties(),
            Content = new ParagraphBlockContent
            {
                Inlines =
                [
                    new TextRun
                    {
                        Id = id + "-text",
                        Text = text
                    }
                ]
            }
        };

    private static DocumentBlock Image(string id, DocumentObjectLayout layout, double width, double height, double order = 0, string? caption = null)
        => new()
        {
            Id = id,
            Type = DocumentBlockType.Image,
            Order = order,
            Content = new ImageBlockContent
            {
                AltText = id,
                Size = new DocumentImageSize { Width = width, Height = height },
                NaturalSize = new DocumentImageSize { Width = width, Height = height },
                Layout = layout,
                Caption = caption
            }
        };

    private static DocumentBlock Table(string id, double order = 0)
        => new()
        {
            Id = id,
            Type = DocumentBlockType.Table,
            Order = order,
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
                                Blocks =
                                [
                                    Paragraph(id + "-cell", "Cell content")
                                ]
                            }
                        ]
                    }
                ]
            }
        };

    private static DocumentObjectLayout LeftSquareLayout(double width, double height, double y = 0)
        => new()
        {
            Kind = DocumentObjectLayoutKind.Anchored,
            Position = new DocumentObjectPosition
            {
                HorizontalRelativeTo = DocumentRelativePosition.Margin,
                VerticalRelativeTo = DocumentRelativePosition.Paragraph,
                HorizontalAlignment = DocumentImageHorizontalPosition.Left,
                Y = y
            },
            Wrap = new DocumentObjectWrap
            {
                Mode = DocumentWrapMode.Square,
                DistanceRight = 10
            },
            Transform = new DocumentObjectTransform
            {
                Width = width,
                Height = height,
                NaturalWidth = width,
                NaturalHeight = height
            }
        };

    private static DocumentObjectLayout RightSquareLayout(double width, double height, double y = 0)
        => new()
        {
            Kind = DocumentObjectLayoutKind.Anchored,
            Position = new DocumentObjectPosition
            {
                HorizontalRelativeTo = DocumentRelativePosition.Margin,
                VerticalRelativeTo = DocumentRelativePosition.Paragraph,
                HorizontalAlignment = DocumentImageHorizontalPosition.Right,
                Y = y
            },
            Wrap = new DocumentObjectWrap
            {
                Mode = DocumentWrapMode.Square,
                DistanceLeft = 10
            },
            Transform = new DocumentObjectTransform
            {
                Width = width,
                Height = height,
                NaturalWidth = width,
                NaturalHeight = height
            }
        };

    private static DocumentEditorDocument AnchoredImageAfterIntro(string id, string introText, bool fixedOnPage = false)
    {
        var document = Document(id);
        document.Blocks.Add(Paragraph("intro", introText, order: 0));
        document.Blocks.Add(Paragraph("anchor", "Anchor paragraph", order: 1));
        document.Blocks.Add(Image(
            "img",
            fixedOnPage ? FixedLayout("anchor", width: 90, height: 60) : AnchoredToParagraphLayout("anchor", width: 90, height: 60),
            width: 90,
            height: 60,
            order: 2));
        return document;
    }

    private static DocumentEditorDocument AnchoredImagePairAfterIntro(string id, string introText)
    {
        var document = Document(id);
        document.Blocks.Add(Paragraph("intro", introText, order: 0));
        document.Blocks.Add(Paragraph("anchor", "Anchor paragraph", order: 1));
        document.Blocks.Add(Image("fixed-img", FixedLayout("anchor", width: 90, height: 60), width: 90, height: 60, order: 2));
        document.Blocks.Add(Image("move-img", AnchoredToParagraphLayout("anchor", width: 90, height: 60), width: 90, height: 60, order: 3));
        return document;
    }

    private static DocumentObjectLayout AnchoredToParagraphLayout(string anchorBlockId, double width, double height)
    {
        var layout = LeftSquareLayout(width, height);
        layout.Anchor.BlockId = anchorBlockId;
        layout.Anchor.MoveWithText = true;
        layout.Anchor.FixedOnPage = false;
        return layout;
    }

    private static DocumentObjectLayout FixedLayout(string anchorBlockId, double width, double height)
        => new()
        {
            Kind = DocumentObjectLayoutKind.Fixed,
            Anchor = new DocumentObjectAnchor
            {
                BlockId = anchorBlockId,
                MoveWithText = false,
                FixedOnPage = true
            },
            Position = new DocumentObjectPosition
            {
                HorizontalRelativeTo = DocumentRelativePosition.Page,
                VerticalRelativeTo = DocumentRelativePosition.Page,
                X = 80,
                Y = 120
            },
            Wrap = new DocumentObjectWrap
            {
                Mode = DocumentWrapMode.InFrontOfText
            },
            Transform = new DocumentObjectTransform
            {
                Width = width,
                Height = height,
                NaturalWidth = width,
                NaturalHeight = height
            }
        };

    private static DocumentObjectLayout TopBottomLayout(double width, double height)
        => new()
        {
            Kind = DocumentObjectLayoutKind.Anchored,
            Position = new DocumentObjectPosition
            {
                HorizontalRelativeTo = DocumentRelativePosition.Margin,
                VerticalRelativeTo = DocumentRelativePosition.Paragraph,
                HorizontalAlignment = DocumentImageHorizontalPosition.Center
            },
            Wrap = new DocumentObjectWrap
            {
                Mode = DocumentWrapMode.TopBottom
            },
            Transform = new DocumentObjectTransform
            {
                Width = width,
                Height = height,
                NaturalWidth = width,
                NaturalHeight = height
            }
        };

    private static IReadOnlyList<DocumentLineBox> Lines(DocumentPageLayoutBox page, string blockId)
        => page.Paragraphs.Where(paragraph => paragraph.BlockId == blockId).SelectMany(paragraph => paragraph.Lines).ToList();

    private static DocumentLineBox FirstLine(DocumentPageLayoutBox page, string blockId)
        => Lines(page, blockId).First();

    private static int CountRestrictedLines(DocumentPageLayoutBox page, string blockId)
        => Lines(page, blockId).Count(line => line.AvailableIntervals.Count == 1 && line.AvailableIntervals[0].X > page.BodyRect.X);

    private static string LongWrapText()
        => "This paragraph is intentionally long enough to wrap around the anchored image and continue underneath it.";

    private static DocumentPageSettings TestPageSettings()
        => new()
        {
            Size = new DocumentPageSize { Name = "Test", Width = 400, Height = 500 },
            Margins = new DocumentPageMargins { Top = 50, Right = 50, Bottom = 50, Left = 50 }
        };

    private static DocumentPageSettings NarrowPageSettings()
        => new()
        {
            Size = new DocumentPageSize { Name = "Narrow", Width = 180, Height = 300 },
            Margins = new DocumentPageMargins { Top = 30, Right = 30, Bottom = 30, Left = 30 }
        };

    private static DocumentPageSettings SmallPageSettings()
        => new()
        {
            Size = new DocumentPageSize { Name = "Small", Width = 260, Height = 180 },
            Margins = new DocumentPageMargins { Top = 30, Right = 30, Bottom = 30, Left = 30 }
        };
}
