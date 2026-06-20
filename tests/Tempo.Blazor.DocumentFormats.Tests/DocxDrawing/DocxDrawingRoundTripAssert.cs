using System.Globalization;
using System.Xml.Linq;
using Tempo.Blazor.DocumentFormats;
using Tempo.Blazor.DocumentFormats.Docx;

namespace Tempo.Blazor.DocumentFormats.Tests.DocxDrawing;

internal static class DocxDrawingRoundTripAssert
{
    public static long PointsToEmu(double points)
        => DocxUnitConverter.PointToEmu(points);

    public static void ShouldBeWithinEmu(long actual, double expectedPoints, long toleranceEmu = 2)
    {
        var expected = PointsToEmu(expectedPoints);
        Math.Abs(actual - expected).Should().BeLessThanOrEqualTo(toleranceEmu);
    }

    public static void ShouldHaveNoUnexpectedWarnings(
        IEnumerable<DocumentFormatCompatibilityWarning> warnings,
        params string[] expectedCodes)
    {
        var allowedCodes = expectedCodes.ToHashSet(StringComparer.Ordinal);
        var unexpected = warnings
            .Where(warning => !allowedCodes.Contains(warning.Code))
            .Select(warning => string.IsNullOrWhiteSpace(warning.SourcePath)
                ? $"{warning.Code}: {warning.Message}"
                : $"{warning.Code} ({warning.SourcePath}): {warning.Message}")
            .ToArray();

        unexpected.Should().BeEmpty("roundtrip tests must either be lossless or explicitly document every compatibility warning");
    }

    public static string CanonicalSmallXml(XElement element, int maxLength = 4096)
    {
        var xml = element.ToString(SaveOptions.DisableFormatting);
        xml.Length.Should().BeLessThanOrEqualTo(maxLength, "small XML snapshots should not cover an entire DOCX part");
        return xml;
    }

    public static long ReadEmuAttribute(XElement element, string attributeName)
        => long.Parse((string)element.Attribute(attributeName)!, CultureInfo.InvariantCulture);

    public static long ReadPositionOffset(XElement host, XName positionName)
    {
        var offset = host.Element(positionName)?.Element(DocxDrawingTestPackage.Wp + "posOffset")?.Value;
        offset.Should().NotBeNullOrWhiteSpace();
        return long.Parse(offset!, CultureInfo.InvariantCulture);
    }
}
