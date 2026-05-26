using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;

namespace Tempo.Blazor.DocumentFormats.Tests.DocxDrawing;

internal sealed class DocxDrawingTestPackage : IDisposable
{
    public static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";
    public static readonly XNamespace Pic = "http://schemas.openxmlformats.org/drawingml/2006/picture";
    public static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    public static readonly XNamespace Rel = "http://schemas.openxmlformats.org/package/2006/relationships";
    public static readonly XNamespace Tm = "urn:tempo-blazor:document-editor:1.0";
    public static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    public static readonly XNamespace Wp = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing";

    private readonly MemoryStream _stream;
    private readonly ZipArchive _archive;
    private readonly Dictionary<string, XDocument> _xmlCache = new(StringComparer.OrdinalIgnoreCase);

    private DocxDrawingTestPackage(byte[] content)
    {
        _stream = new MemoryStream(content);
        _archive = new ZipArchive(_stream, ZipArchiveMode.Read, leaveOpen: false);
    }

    public XDocument DocumentXml => ReadXml("word/document.xml");

    public XDocument DocumentRelationshipsXml => ReadRelationshipsForPart("word/document.xml");

    public IReadOnlyList<string> HeaderPartPaths
        => PartPaths("word/header", ".xml");

    public IReadOnlyList<string> FooterPartPaths
        => PartPaths("word/footer", ".xml");

    public static DocxDrawingTestPackage Open(byte[] content)
        => new(content);

    public XDocument ReadXml(string path)
    {
        if (_xmlCache.TryGetValue(path, out var cached))
        {
            return cached;
        }

        var entry = _archive.GetEntry(path);
        entry.Should().NotBeNull($"DOCX package should contain {path}");
        using var stream = entry!.Open();
        var xml = XDocument.Load(stream, LoadOptions.PreserveWhitespace);
        _xmlCache[path] = xml;
        return xml;
    }

    public XDocument ReadRelationshipsForPart(string partPath)
    {
        var directory = Path.GetDirectoryName(partPath)?.Replace('\\', '/') ?? string.Empty;
        var fileName = Path.GetFileName(partPath);
        var relationshipPath = string.IsNullOrWhiteSpace(directory)
            ? $"_rels/{fileName}.rels"
            : $"{directory}/_rels/{fileName}.rels";
        return ReadXml(relationshipPath);
    }

    public XElement AssertHasInlinePicture(XDocument? partXml = null, string? altText = null)
    {
        var inline = FindDrawingHost(partXml ?? DocumentXml, "inline", altText);
        inline.Should().NotBeNull("the fixture should contain a wp:inline picture");
        return inline!;
    }

    public XElement AssertHasAnchorPicture(XDocument? partXml = null, string? altText = null)
    {
        var anchor = FindDrawingHost(partXml ?? DocumentXml, "anchor", altText);
        anchor.Should().NotBeNull("the fixture should contain a wp:anchor picture");
        return anchor!;
    }

    public XElement AssertPictureRelationship(XElement host, XDocument relationshipsXml, string? targetExtension = null)
    {
        var embedId = host.Descendants(A + "blip").Select(element => (string?)element.Attribute(R + "embed")).FirstOrDefault();
        embedId.Should().NotBeNullOrWhiteSpace("the picture should use an embedded image relationship");

        var relationship = relationshipsXml.Root!.Elements(Rel + "Relationship")
            .SingleOrDefault(element => string.Equals((string?)element.Attribute("Id"), embedId, StringComparison.Ordinal));
        relationship.Should().NotBeNull($"relationship {embedId} should be present in the owning .rels part");
        ((string?)relationship!.Attribute("Type")).Should().Contain("/image");

        if (!string.IsNullOrWhiteSpace(targetExtension))
        {
            ((string?)relationship.Attribute("Target")).Should().EndWith(targetExtension);
        }

        return relationship;
    }

    public XElement AssertWrapMode(XElement anchor, string wrapLocalName)
    {
        var wrap = anchor.Elements(Wp + wrapLocalName).SingleOrDefault();
        wrap.Should().NotBeNull($"anchor should contain wp:{wrapLocalName}");
        return wrap!;
    }

    public void AssertPosition(
        XElement anchor,
        string expectedHorizontalRelativeFrom,
        string expectedVerticalRelativeFrom,
        long? expectedX = null,
        long? expectedY = null)
    {
        var horizontal = anchor.Element(Wp + "positionH");
        var vertical = anchor.Element(Wp + "positionV");
        horizontal.Should().NotBeNull("anchor should contain wp:positionH");
        vertical.Should().NotBeNull("anchor should contain wp:positionV");
        ((string?)horizontal!.Attribute("relativeFrom")).Should().Be(expectedHorizontalRelativeFrom);
        ((string?)vertical!.Attribute("relativeFrom")).Should().Be(expectedVerticalRelativeFrom);
        if (expectedX.HasValue)
        {
            long.Parse(horizontal.Element(Wp + "posOffset")!.Value, CultureInfo.InvariantCulture).Should().Be(expectedX.Value);
        }

        if (expectedY.HasValue)
        {
            long.Parse(vertical.Element(Wp + "posOffset")!.Value, CultureInfo.InvariantCulture).Should().Be(expectedY.Value);
        }
    }

    public void AssertExtentEmu(XElement host, long expectedCx, long expectedCy)
    {
        var extent = host.Element(Wp + "extent");
        extent.Should().NotBeNull("drawing host should contain wp:extent");
        long.Parse((string)extent!.Attribute("cx")!, CultureInfo.InvariantCulture).Should().Be(expectedCx);
        long.Parse((string)extent.Attribute("cy")!, CultureInfo.InvariantCulture).Should().Be(expectedCy);
    }

    public XElement AssertCropSrcRect(XElement host, int left, int top, int right, int bottom)
    {
        var sourceRectangle = host.Descendants(A + "srcRect").SingleOrDefault();
        sourceRectangle.Should().NotBeNull("picture should contain a:srcRect crop data");
        ((string?)sourceRectangle!.Attribute("l")).Should().Be(left.ToString(CultureInfo.InvariantCulture));
        ((string?)sourceRectangle.Attribute("t")).Should().Be(top.ToString(CultureInfo.InvariantCulture));
        ((string?)sourceRectangle.Attribute("r")).Should().Be(right.ToString(CultureInfo.InvariantCulture));
        ((string?)sourceRectangle.Attribute("b")).Should().Be(bottom.ToString(CultureInfo.InvariantCulture));
        return sourceRectangle;
    }

    public void AssertDocPrAltText(XElement host, string expectedDescription)
    {
        var docProperties = host.Element(Wp + "docPr");
        docProperties.Should().NotBeNull("drawing host should contain wp:docPr");
        ((string?)docProperties!.Attribute("descr")).Should().Be(expectedDescription);
    }

    public void AssertNoTempoAttributesRequiredForImport()
    {
        _archive.Entries
            .Where(entry => entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            .Select(entry => ReadXml(entry.FullName))
            .SelectMany(xml => xml.Descendants().SelectMany(element => element.Attributes()))
            .Should()
            .NotContain(attribute => attribute.Name.Namespace == Tm);
    }

    public XElement AssertTableCellDrawing()
    {
        var tableCellDrawing = DocumentXml
            .Descendants(W + "tc")
            .Descendants(W + "drawing")
            .SingleOrDefault();
        tableCellDrawing.Should().NotBeNull("document.xml should contain a table-cell drawing");
        return tableCellDrawing!;
    }

    public void Dispose()
    {
        _archive.Dispose();
        _stream.Dispose();
    }

    private IReadOnlyList<string> PartPaths(string prefix, string suffix)
        => _archive.Entries
            .Select(entry => entry.FullName)
            .Where(path => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                && path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
                && !path.Contains("/_rels/", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static XElement? FindDrawingHost(XDocument xml, string localName, string? altText)
    {
        var candidates = xml.Descendants(Wp + localName);
        if (string.IsNullOrWhiteSpace(altText))
        {
            return candidates.FirstOrDefault();
        }

        return candidates.FirstOrDefault(element =>
            string.Equals((string?)element.Element(Wp + "docPr")?.Attribute("descr"), altText, StringComparison.Ordinal));
    }
}
