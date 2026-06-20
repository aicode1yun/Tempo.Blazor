using DocumentFormat.OpenXml.Packaging;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentFormats.Docx;
using A = DocumentFormat.OpenXml.Drawing;

namespace Tempo.Blazor.DocumentFormats.Tests.DocxDrawing;

public sealed class DocumentDocxDrawingPhase34Tests
{
    private static readonly byte[] PngBytes = Convert.FromBase64String(
        DocumentFormatTestData.TransparentPngDataUrl[(DocumentFormatTestData.TransparentPngDataUrl.IndexOf(',') + 1)..]);

    [Fact]
    public async Task Phase34_ExportAsync_WritesImageRelationshipsInOwningPackageParts()
    {
        var exported = await new DocumentDocxExporter().ExportAsync(CreateEveryPartDocument());

        using var package = DocxDrawingTestPackage.Open(exported.Content);

        AssertOwningRelationship(package, "word/document.xml", "Body image");
        AssertOwningRelationship(package, package.HeaderPartPaths.Single(), "Header image");
        AssertOwningRelationship(package, package.FooterPartPaths.Single(), "Footer image");
        AssertOwningRelationship(package, "word/footnotes.xml", "Footnote image");
        AssertOwningRelationship(package, "word/endnotes.xml", "Endnote image");
        AssertOwningRelationship(package, "word/comments.xml", "Comment image");
    }

    [Fact]
    public async Task Phase34_ImportAsync_ReadsHeaderImageRelationshipFromHeaderPart()
    {
        var exported = await new DocumentDocxExporter().ExportAsync(CreateEveryPartDocument());

        var imported = await new DocumentDocxImporter().ImportAsync(new MemoryStream(exported.Content));
        var headerDrawing = imported.Document.HeadersFooters
            .Single(headerFooter => headerFooter.Type == DocumentHeaderFooterType.Header)
            .Blocks
            .SelectMany(GetInlines)
            .OfType<DocumentDrawingRun>()
            .Single(drawing => drawing.AltText == "Header image");

        headerDrawing.Url.Should().StartWith("data:image/png;base64,", "the image bytes should be read through the header part relationship");
        imported.Warnings.Should().NotContain(warning => warning.Code == "docx.imageMissingPart");
    }

    [Fact]
    public async Task Phase34_ExportAsync_ProviderAssetWritesResolvedImagePartData()
    {
        var assetBytes = PngBytes.Concat(new byte[] { 0x13, 0x37, 0x42 }).ToArray();
        var document = DocumentEditorDocument.Empty("phase34-provider-asset");
        document.Blocks.Add(Paragraph(Drawing("Provider image", DocumentImageSource.Asset, assetId: "asset-1")));

        var exported = await new DocumentDocxExporter().ExportAsync(document, new DocumentFormatExportOptions
        {
            ImageResolver = (request, _) => Task.FromResult<DocumentFormatImageExportResult?>(new DocumentFormatImageExportResult
            {
                ContentType = "image/png",
                FileName = $"{request.AssetId}.png",
                Content = assetBytes
            })
        });

        using var stream = new MemoryStream(exported.Content);
        using var word = WordprocessingDocument.Open(stream, false);
        var imagePart = word.MainDocumentPart!.ImageParts.Single();
        using var imageStream = imagePart.GetStream(FileMode.Open, FileAccess.Read);
        using var imageMemory = new MemoryStream();
        await imageStream.CopyToAsync(imageMemory);

        imageMemory.ToArray().Should().Equal(assetBytes);
    }

    [Fact]
    public async Task Phase34_ExportAsync_SameAssetIsWrittenAsSeparateImagePartsDeterministically()
    {
        var resolverCalls = 0;
        var document = DocumentEditorDocument.Empty("phase34-same-assets");
        document.Blocks.Add(Paragraph(
            Drawing("Repeated asset 1", DocumentImageSource.Asset, assetId: "asset-shared"),
            new TextRun { Text = " " },
            Drawing("Repeated asset 2", DocumentImageSource.Asset, assetId: "asset-shared")));

        var exported = await new DocumentDocxExporter().ExportAsync(document, new DocumentFormatExportOptions
        {
            ImageResolver = (_, _) =>
            {
                resolverCalls++;
                return Task.FromResult<DocumentFormatImageExportResult?>(new DocumentFormatImageExportResult
                {
                    ContentType = "image/png",
                    FileName = "shared.png",
                    Content = PngBytes
                });
            }
        });

        using var stream = new MemoryStream(exported.Content);
        using var word = WordprocessingDocument.Open(stream, false);
        var relationshipIds = word.MainDocumentPart!.Document.Body!
            .Descendants<A.Blip>()
            .Select(blip => blip.Embed?.Value)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToArray();

        resolverCalls.Should().Be(1, "phase 40 caches provider asset bytes while keeping separate deterministic image parts");
        word.MainDocumentPart.ImageParts.Should().HaveCount(2);
        relationshipIds.Should().HaveCount(2).And.OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task Phase34_ImportAsync_DropsImagePartAboveConfiguredSecurityLimit()
    {
        var document = DocumentEditorDocument.Empty("phase34-image-limit");
        document.Blocks.Add(Paragraph(Drawing("Too large image")));
        var exported = await new DocumentDocxExporter().ExportAsync(document);

        var imported = await new DocumentDocxImporter().ImportAsync(new MemoryStream(exported.Content), new DocumentFormatImportOptions
        {
            MaxImagePartBytes = PngBytes.Length - 1
        });

        imported.Warnings.Should().Contain(warning =>
            warning.Code == "docx.imagePartTooLarge"
            && warning.Severity == DocumentFormatCompatibilitySeverity.Dropped);
        DocumentImagePersistence.EnumerateDrawingRuns(imported.Document).Should().BeEmpty();
    }

    private static void AssertOwningRelationship(DocxDrawingTestPackage package, string partPath, string altText)
    {
        var partXml = package.ReadXml(partPath);
        var host = partXml.Descendants(DocxDrawingTestPackage.Wp + "inline")
            .Concat(partXml.Descendants(DocxDrawingTestPackage.Wp + "anchor"))
            .SingleOrDefault(element => string.Equals((string?)element.Element(DocxDrawingTestPackage.Wp + "docPr")?.Attribute("descr"), altText, StringComparison.Ordinal));

        host.Should().NotBeNull($"{partPath} should contain picture '{altText}'");
        package.AssertPictureRelationship(host!, package.ReadRelationshipsForPart(partPath), ".png");
    }

    private static DocumentEditorDocument CreateEveryPartDocument()
    {
        var document = DocumentEditorDocument.Empty("phase34-every-part");
        document.Blocks.Add(Paragraph(
            new TextRun { Text = "Body " },
            Drawing("Body image"),
            new TextRun { Text = " footnote " },
            new DocumentNoteReferenceRun { NoteId = "1", NoteType = DocumentNoteType.Footnote },
            new TextRun { Text = " endnote " },
            new DocumentNoteReferenceRun { NoteId = "2", NoteType = DocumentNoteType.Endnote },
            new TextRun
            {
                Text = " comment anchor",
                Marks = [new InlineMark { Type = InlineMarkType.CommentAnchor, CommentAnchor = new CommentAnchorMarkData { CommentId = "comment-1" } }]
            }));
        document.HeadersFooters.Add(new DocumentHeaderFooter
        {
            Type = DocumentHeaderFooterType.Header,
            Blocks = [Paragraph(new TextRun { Text = "Header " }, Drawing("Header image"))]
        });
        document.HeadersFooters.Add(new DocumentHeaderFooter
        {
            Type = DocumentHeaderFooterType.Footer,
            Blocks = [Paragraph(new TextRun { Text = "Footer " }, Drawing("Footer image"))]
        });
        document.Notes.Add(new DocumentNote
        {
            Id = "1",
            Type = DocumentNoteType.Footnote,
            Blocks = [Paragraph(new TextRun { Text = "Footnote " }, Drawing("Footnote image"))]
        });
        document.Notes.Add(new DocumentNote
        {
            Id = "2",
            Type = DocumentNoteType.Endnote,
            Blocks = [Paragraph(new TextRun { Text = "Endnote " }, Drawing("Endnote image"))]
        });
        document.Comments.Add(new DocumentComment
        {
            Id = "comment-1",
            Entries =
            [
                new DocumentCommentEntry
                {
                    Author = new DocumentEditorAuthor { DisplayName = "Reviewer" },
                    Text = "Comment image",
                    Inlines = [new TextRun { Text = "Comment " }, Drawing("Comment image")]
                }
            ]
        });

        return document;
    }

    private static DocumentBlock Paragraph(params InlineContent[] inlines)
        => new()
        {
            Type = DocumentBlockType.Paragraph,
            Content = new ParagraphBlockContent
            {
                Inlines = inlines.ToList()
            }
        };

    private static DocumentDrawingRun Drawing(
        string altText,
        DocumentImageSource source = DocumentImageSource.Url,
        string? assetId = null)
        => new()
        {
            Source = source,
            Url = source == DocumentImageSource.Url ? DocumentFormatTestData.TransparentPngDataUrl : null,
            AssetId = assetId,
            AltText = altText,
            Size = new DocumentImageSize { Width = 24, Height = 24 },
            Layout = new DocumentObjectLayout
            {
                Kind = DocumentObjectLayoutKind.Inline,
                Wrap = new DocumentObjectWrap { Mode = DocumentWrapMode.Inline },
                Transform = new DocumentObjectTransform { Width = 24, Height = 24 }
            }
        };

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
