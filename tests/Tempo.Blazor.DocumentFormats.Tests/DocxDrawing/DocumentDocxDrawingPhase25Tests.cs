using System.Globalization;

namespace Tempo.Blazor.DocumentFormats.Tests.DocxDrawing;

public class DocumentDocxDrawingPhase25Tests
{
    private const long DefaultCx = 120 * 12700L;
    private const long DefaultCy = 80 * 12700L;
    private const long AnchorX = 36 * 12700L;
    private const long AnchorY = 24 * 12700L;

    [Fact]
    public void Phase25_FixtureCatalog_CoversRequiredDrawingScenarios()
    {
        DocxDrawingFixtureBuilder.RequiredFixtures.Select(fixture => fixture.Name)
            .Should()
            .Contain([
                "inline-png",
                "inline-jpeg-alt",
                "anchor-square",
                "anchor-top-bottom",
                "anchor-behind-text",
                "anchor-in-front-of-text",
                "anchor-tight",
                "anchor-through",
                "crop",
                "rotation",
                "header-footer-table",
                "onlyoffice-like-anchor"
            ]);

        DocxDrawingFixtureBuilder.RequiredFixtures
            .Should()
            .OnlyContain(fixture => fixture.Create().Length > 0);
    }

    [Fact]
    public void Phase25_InlinePngFixture_HasInlinePictureRelationshipAltAndExtent()
    {
        using var package = DocxDrawingTestPackage.Open(DocxDrawingFixtureBuilder.CreateInlinePng());

        var inline = package.AssertHasInlinePicture(altText: "Inline PNG picture");

        package.AssertExtentEmu(inline, DefaultCx, DefaultCy);
        package.AssertDocPrAltText(inline, "Inline PNG picture");
        package.AssertPictureRelationship(inline, package.DocumentRelationshipsXml, ".png");
        package.AssertNoTempoAttributesRequiredForImport();
    }

    [Fact]
    public void Phase25_InlineJpegFixture_UsesJpegImageRelationshipAndAltText()
    {
        using var package = DocxDrawingTestPackage.Open(DocxDrawingFixtureBuilder.CreateInlineJpegWithAltText());

        var inline = package.AssertHasInlinePicture(altText: "Inline JPEG picture");

        package.AssertDocPrAltText(inline, "Inline JPEG picture");
        package.AssertPictureRelationship(inline, package.DocumentRelationshipsXml, ".jpg");
        package.AssertNoTempoAttributesRequiredForImport();
    }

    [Theory]
    [InlineData(DocxDrawingFixtureWrap.Square, "wrapSquare", false)]
    [InlineData(DocxDrawingFixtureWrap.TopBottom, "wrapTopAndBottom", false)]
    [InlineData(DocxDrawingFixtureWrap.BehindText, "wrapNone", true)]
    [InlineData(DocxDrawingFixtureWrap.InFrontOfText, "wrapNone", false)]
    public void Phase25_AnchorFixtures_HaveNativeWrapPositionAndRelationship(
        DocxDrawingFixtureWrap wrap,
        string expectedWrapElement,
        bool expectedBehindDoc)
    {
        using var package = DocxDrawingTestPackage.Open(DocxDrawingFixtureBuilder.CreateAnchor(wrap));

        var anchor = package.AssertHasAnchorPicture(altText: $"{wrap} picture");

        package.AssertWrapMode(anchor, expectedWrapElement);
        package.AssertPosition(anchor, "margin", "paragraph", AnchorX, AnchorY);
        package.AssertExtentEmu(anchor, DefaultCx, DefaultCy);
        package.AssertPictureRelationship(anchor, package.DocumentRelationshipsXml, ".png");
        ((string?)anchor.Attribute("behindDoc")).Should().Be(expectedBehindDoc ? "1" : "0");
        package.AssertNoTempoAttributesRequiredForImport();
    }

    [Theory]
    [InlineData(DocxDrawingFixtureWrap.Tight, "wrapTight")]
    [InlineData(DocxDrawingFixtureWrap.Through, "wrapThrough")]
    public void Phase25_TightThroughFixtures_HaveWrapPolygons(DocxDrawingFixtureWrap wrap, string expectedWrapElement)
    {
        using var package = DocxDrawingTestPackage.Open(DocxDrawingFixtureBuilder.CreateAnchor(wrap));

        var anchor = package.AssertHasAnchorPicture(altText: $"{wrap} picture");
        var wrapElement = package.AssertWrapMode(anchor, expectedWrapElement);

        var polygon = wrapElement.Element(DocxDrawingTestPackage.Wp + "wrapPolygon");
        polygon.Should().NotBeNull();
        polygon!.Element(DocxDrawingTestPackage.Wp + "start").Should().NotBeNull();
        polygon.Elements(DocxDrawingTestPackage.Wp + "lineTo").Should().HaveCount(3);
        package.AssertNoTempoAttributesRequiredForImport();
    }

    [Fact]
    public void Phase25_CropFixture_HasDrawingSourceRectangle()
    {
        using var package = DocxDrawingTestPackage.Open(DocxDrawingFixtureBuilder.CreateCroppedInline());

        var inline = package.AssertHasInlinePicture(altText: "Cropped picture");

        package.AssertCropSrcRect(inline, left: 10000, top: 20000, right: 30000, bottom: 40000);
    }

    [Fact]
    public void Phase25_RotationFixture_HasDrawingTransformRotation()
    {
        using var package = DocxDrawingTestPackage.Open(DocxDrawingFixtureBuilder.CreateRotatedInline());

        var inline = package.AssertHasInlinePicture(altText: "Rotated picture");
        var transform = inline.Descendants(DocxDrawingTestPackage.A + "xfrm").Single();

        ((string?)transform.Attribute("rot")).Should().Be((15 * 60000).ToString(CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Phase25_HeaderFooterTableFixture_UsesScopedPartRelationships()
    {
        using var package = DocxDrawingTestPackage.Open(DocxDrawingFixtureBuilder.CreateHeaderFooterAndTableCell());

        package.HeaderPartPaths.Should().ContainSingle();
        package.FooterPartPaths.Should().ContainSingle();

        var headerPath = package.HeaderPartPaths.Single();
        var footerPath = package.FooterPartPaths.Single();
        var headerXml = package.ReadXml(headerPath);
        var footerXml = package.ReadXml(footerPath);
        var headerInline = package.AssertHasInlinePicture(headerXml, "Header picture");
        var footerInline = package.AssertHasInlinePicture(footerXml, "Footer picture");
        var tableDrawing = package.AssertTableCellDrawing();
        var tableInline = tableDrawing.Descendants(DocxDrawingTestPackage.Wp + "inline").Single();

        package.AssertPictureRelationship(headerInline, package.ReadRelationshipsForPart(headerPath), ".png");
        package.AssertPictureRelationship(footerInline, package.ReadRelationshipsForPart(footerPath), ".png");
        package.AssertPictureRelationship(tableInline, package.DocumentRelationshipsXml, ".png");
    }

    [Fact]
    public void Phase25_OnlyOfficeLikeFixture_HasAnchorShapeWithoutTempoMetadata()
    {
        using var package = DocxDrawingTestPackage.Open(DocxDrawingFixtureBuilder.CreateOnlyOfficeLikeAnchor());

        var anchor = package.AssertHasAnchorPicture(altText: "OnlyOffice-like picture");

        package.AssertWrapMode(anchor, "wrapNone");
        package.AssertPosition(anchor, "page", "page", 48 * 12700L, 36 * 12700L);
        ((string?)anchor.Attribute("relativeHeight")).Should().Be("251659264");
        ((string?)anchor.Attribute("layoutInCell")).Should().Be("0");
        package.AssertNoTempoAttributesRequiredForImport();
    }
}
