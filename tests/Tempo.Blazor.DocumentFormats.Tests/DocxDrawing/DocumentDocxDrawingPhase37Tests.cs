using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using Tempo.Blazor.DocumentFormats;
using Tempo.Blazor.DocumentFormats.Docx;

namespace Tempo.Blazor.DocumentFormats.Tests.DocxDrawing;

public sealed class DocumentDocxDrawingPhase37Tests
{
    private const long DefaultCx = 120 * 12700L;
    private const long DefaultCy = 80 * 12700L;

    [Fact]
    public async Task Phase37_RoundTrip_WordInlineFixture_ExportsInlinePicture()
    {
        using (var sourcePackage = DocxDrawingTestPackage.Open(DocxDrawingFixtureBuilder.CreateInlinePng()))
        {
            sourcePackage.AssertNoTempoAttributesRequiredForImport();
        }

        var exported = await ImportAndExportAsync(DocxDrawingFixtureBuilder.CreateInlinePng());

        using var package = DocxDrawingTestPackage.Open(exported.Content);
        var inline = package.AssertHasInlinePicture(altText: "Inline PNG picture");
        var extent = inline.Element(DocxDrawingTestPackage.Wp + "extent")!;

        DocxDrawingRoundTripAssert.ShouldBeWithinEmu(DocxDrawingRoundTripAssert.ReadEmuAttribute(extent, "cx"), 120);
        DocxDrawingRoundTripAssert.ShouldBeWithinEmu(DocxDrawingRoundTripAssert.ReadEmuAttribute(extent, "cy"), 80);
        package.AssertPictureRelationship(inline, package.DocumentRelationshipsXml, ".png");
        DocxDrawingRoundTripAssert.CanonicalSmallXml(extent)
            .Should()
            .Contain("cx=\"1524000\"");
    }

    [Fact]
    public async Task Phase37_RoundTrip_WordSquareAnchorFixture_ExportsAnchorAndWrapSquare()
    {
        using (var sourcePackage = DocxDrawingTestPackage.Open(DocxDrawingFixtureBuilder.CreateAnchor(DocxDrawingFixtureWrap.Square)))
        {
            sourcePackage.AssertNoTempoAttributesRequiredForImport();
        }

        var exported = await ImportAndExportAsync(DocxDrawingFixtureBuilder.CreateAnchor(DocxDrawingFixtureWrap.Square));

        using var package = DocxDrawingTestPackage.Open(exported.Content);
        var anchor = package.AssertHasAnchorPicture(altText: "Square picture");
        var wrap = package.AssertWrapMode(anchor, "wrapSquare");

        package.AssertPosition(anchor, "margin", "paragraph", 36 * 12700L, 24 * 12700L);
        package.AssertExtentEmu(anchor, DefaultCx, DefaultCy);
        package.AssertPictureRelationship(anchor, package.DocumentRelationshipsXml, ".png");
        DocxDrawingRoundTripAssert.CanonicalSmallXml(wrap)
            .Should()
            .Contain("wrapText=\"bothSides\"");
    }

    [Fact]
    public async Task Phase37_RoundTrip_OnlyOfficeLikeAnchorFixture_PreservesPositionExtentAndRelativeHeight()
    {
        using (var sourcePackage = DocxDrawingTestPackage.Open(DocxDrawingFixtureBuilder.CreateOnlyOfficeLikeAnchor()))
        {
            sourcePackage.AssertNoTempoAttributesRequiredForImport();
        }

        var exported = await ImportAndExportAsync(DocxDrawingFixtureBuilder.CreateOnlyOfficeLikeAnchor());

        using var package = DocxDrawingTestPackage.Open(exported.Content);
        var anchor = package.AssertHasAnchorPicture(altText: "OnlyOffice-like picture");
        var extent = anchor.Element(DocxDrawingTestPackage.Wp + "extent")!;

        package.AssertPosition(anchor, "page", "page");
        DocxDrawingRoundTripAssert.ShouldBeWithinEmu(
            DocxDrawingRoundTripAssert.ReadPositionOffset(anchor, DocxDrawingTestPackage.Wp + "positionH"),
            48);
        DocxDrawingRoundTripAssert.ShouldBeWithinEmu(
            DocxDrawingRoundTripAssert.ReadPositionOffset(anchor, DocxDrawingTestPackage.Wp + "positionV"),
            36);
        DocxDrawingRoundTripAssert.ShouldBeWithinEmu(DocxDrawingRoundTripAssert.ReadEmuAttribute(extent, "cx"), 120);
        DocxDrawingRoundTripAssert.ShouldBeWithinEmu(DocxDrawingRoundTripAssert.ReadEmuAttribute(extent, "cy"), 80);
        ((string?)anchor.Attribute("relativeHeight")).Should().Be("251659264");
        ((string?)anchor.Attribute("layoutInCell")).Should().Be("0");
        ((string?)anchor.Attribute("allowOverlap")).Should().Be("1");
    }

    [Fact]
    public async Task Phase37_RoundTrip_CropFixture_ExportsSourceRectangle()
    {
        var exported = await ImportAndExportAsync(DocxDrawingFixtureBuilder.CreateCroppedInline());

        using var package = DocxDrawingTestPackage.Open(exported.Content);
        var inline = package.AssertHasInlinePicture(altText: "Cropped picture");
        var crop = package.AssertCropSrcRect(inline, left: 10000, top: 20000, right: 30000, bottom: 40000);

        DocxDrawingRoundTripAssert.CanonicalSmallXml(crop)
            .Should()
            .Contain("srcRect");
    }

    [Fact]
    public async Task Phase37_RoundTrip_HeaderFixture_ExportsHeaderImageRelationship()
    {
        var exported = await ImportAndExportAsync(DocxDrawingFixtureBuilder.CreateHeaderFooterAndTableCell());

        using var package = DocxDrawingTestPackage.Open(exported.Content);
        var headerPath = package.HeaderPartPaths.Single();
        var headerXml = package.ReadXml(headerPath);
        var headerInline = package.AssertHasInlinePicture(headerXml, "Header picture");

        package.AssertPictureRelationship(headerInline, package.ReadRelationshipsForPart(headerPath), ".png");
    }

    [Fact]
    public async Task Phase37_RoundTrip_TableFixture_ExportsDrawingInsideCell()
    {
        var exported = await ImportAndExportAsync(DocxDrawingFixtureBuilder.CreateHeaderFooterAndTableCell());

        using var package = DocxDrawingTestPackage.Open(exported.Content);
        var tableDrawing = package.AssertTableCellDrawing();
        var tableInline = tableDrawing.Descendants(DocxDrawingTestPackage.Wp + "inline").Single();

        package.AssertPictureRelationship(tableInline, package.DocumentRelationshipsXml, ".png");
    }

    [Fact]
    public async Task Phase37_TempoFixture_ExportImport_ModelLayoutValuesMatchWithinTolerance()
    {
        var exported = await new DocumentDocxExporter().ExportAsync(DocumentFormatTestData.CreateImageLayoutParityDocument());
        DocxDrawingRoundTripAssert.ShouldHaveNoUnexpectedWarnings(exported.Warnings);

        var imported = await new DocumentDocxImporter().ImportAsync(new MemoryStream(exported.Content));
        DocxDrawingRoundTripAssert.ShouldHaveNoUnexpectedWarnings(imported.Warnings);

        DocumentFormatTestData.AssertImageLayoutParity(imported.Document);
    }

    [Fact]
    public async Task Phase37_TempoFixture_ExportedDocxPassesOpenXmlValidatorWithoutMajorSchemaErrors()
    {
        var exported = await new DocumentDocxExporter().ExportAsync(DocumentFormatTestData.CreateImageLayoutParityDocument());
        DocxDrawingRoundTripAssert.ShouldHaveNoUnexpectedWarnings(exported.Warnings);

        using var stream = new MemoryStream(exported.Content);
        using var word = WordprocessingDocument.Open(stream, false);
        var validator = new OpenXmlValidator();
        var errors = validator.Validate(word)
            .Select(error => $"{error.Path?.XPath}: {error.Description}")
            .ToArray();

        errors.Should().BeEmpty("generated drawing DOCX should not rely on tolerant Word auto-fix");
    }

    private static async Task<DocumentFormatExportResult> ImportAndExportAsync(byte[] fixture)
    {
        var imported = await new DocumentDocxImporter().ImportAsync(new MemoryStream(fixture));
        DocxDrawingRoundTripAssert.ShouldHaveNoUnexpectedWarnings(imported.Warnings);

        var exported = await new DocumentDocxExporter().ExportAsync(imported.Document);
        DocxDrawingRoundTripAssert.ShouldHaveNoUnexpectedWarnings(exported.Warnings);

        return exported;
    }
}
