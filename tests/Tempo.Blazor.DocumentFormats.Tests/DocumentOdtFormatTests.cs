using System.IO.Compression;
using System.Xml.Linq;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentFormats.Odt;

namespace Tempo.Blazor.DocumentFormats.Tests;

public class DocumentOdtFormatTests
{
    [Fact]
    public async Task ExportAsync_CreatesOpenableOdtZipPackage()
    {
        var document = DocumentFormatTestData.CreateDocument();

        var result = await new DocumentOdtExporter().ExportAsync(document);

        result.ContentType.Should().Be("application/vnd.oasis.opendocument.text");
        result.FileName.Should().EndWith(".odt");
        using var archive = new ZipArchive(new MemoryStream(result.Content), ZipArchiveMode.Read);
        archive.GetEntry("mimetype").Should().NotBeNull();
        archive.GetEntry("content.xml").Should().NotBeNull();
        archive.GetEntry("styles.xml").Should().NotBeNull();
        archive.GetEntry("META-INF/manifest.xml").Should().NotBeNull();

        using var contentStream = archive.GetEntry("content.xml")!.Open();
        var xml = await XDocument.LoadAsync(contentStream, LoadOptions.None, CancellationToken.None);
        xml.ToString().Should().Contain("Agreement");
        xml.ToString().Should().Contain("Numbered item");
        archive.Entries.Should().Contain(entry => entry.FullName.StartsWith("Pictures/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ImportAsync_ReadsOdtParagraphsHeadingsListsTablesMergedCellsAndImages()
    {
        var exported = await new DocumentOdtExporter().ExportAsync(DocumentFormatTestData.CreateDocument());

        var result = await new DocumentOdtImporter().ImportAsync(new MemoryStream(exported.Content), new DocumentFormatImportOptions
        {
            DocumentId = "imported-odt",
            FileName = "sample.odt"
        });

        result.Format.Should().Be(DocumentFormatKind.Odt);
        result.Document.DocumentId.Should().Be("imported-odt");
        result.Document.Blocks.Any(block => block.Content is HeadingBlockContent).Should().BeTrue();
        result.Document.Blocks.Any(block => block.Content is ListBlockContent { Ordered: true }).Should().BeTrue();
        result.Document.Blocks.Any(block => block.Content is TableBlockContent).Should().BeTrue();
        result.Document.Blocks.Any(block => block.Content is ImageBlockContent).Should().BeTrue();
        var table = result.Document.Blocks.Select(block => block.Content).OfType<TableBlockContent>().Single();
        table.Rows[0].Cells[0].ColumnSpan.Should().Be(2);
    }

    [Fact]
    public async Task RoundTrip_OdtModelOdt_PreservesVisibleText()
    {
        var source = DocumentFormatTestData.CreateDocument();
        var exported = await new DocumentOdtExporter().ExportAsync(source);
        var imported = await new DocumentOdtImporter().ImportAsync(new MemoryStream(exported.Content));

        FlattenText(imported.Document).Should().Contain("Agreement");
        FlattenText(imported.Document).Should().Contain("Bold and link");
        FlattenText(imported.Document).Should().Contain("Merged");
    }

    [Fact]
    public async Task RoundTrip_OdtParagraphMarks_PreservesEmptyParagraphsAndPageBreaks()
    {
        var source = DocumentEditorDocument.Empty("odt-paragraph-marks");
        source.Blocks =
        [
            new DocumentBlock { Type = DocumentBlockType.Paragraph, Order = 0, Content = new ParagraphBlockContent { Inlines = [new TextRun { Text = "Before empty paragraph" }] } },
            new DocumentBlock { Type = DocumentBlockType.Paragraph, Order = 1, Content = new ParagraphBlockContent() },
            new DocumentBlock { Type = DocumentBlockType.PageBreak, Order = 2, Content = new PageBreakBlockContent() },
            new DocumentBlock { Type = DocumentBlockType.Paragraph, Order = 3, Content = new ParagraphBlockContent { Inlines = [new TextRun { Text = "After page break" }] } }
        ];

        var exported = await new DocumentOdtExporter().ExportAsync(source);
        var imported = await new DocumentOdtImporter().ImportAsync(new MemoryStream(exported.Content));

        imported.Document.Blocks.Any(block =>
            block.Content is ParagraphBlockContent paragraph && paragraph.Inlines.Count == 0).Should().BeTrue();
        imported.Document.Blocks.Should().Contain(block => block.Content is PageBreakBlockContent);
        FlattenText(imported.Document).Should().Contain("After page break");
    }

    [Fact]
    public async Task RoundTrip_OdtMergedCells_PreservesCoveredCells()
    {
        var source = DocumentEditorDocument.Empty("odt-merged-cells");
        source.Blocks =
        [
            new DocumentBlock
            {
                Type = DocumentBlockType.Table,
                Order = 0,
                Content = new TableBlockContent
                {
                    Rows =
                    [
                        new TableRowContent
                        {
                            Cells =
                            [
                                new TableCellContent { Id = "origin", ColumnSpan = 2, RowSpan = 2, Blocks = [DocumentFormatTestData.Paragraph("Merged origin")] },
                                new TableCellContent { Merge = new TableCellMerge { IsOrigin = false, OriginCellId = "origin" }, Blocks = [DocumentFormatTestData.Paragraph(string.Empty)] },
                                new TableCellContent { Blocks = [DocumentFormatTestData.Paragraph("Right")] }
                            ]
                        },
                        new TableRowContent
                        {
                            Cells =
                            [
                                new TableCellContent { Merge = new TableCellMerge { IsOrigin = false, OriginCellId = "origin" }, Blocks = [DocumentFormatTestData.Paragraph(string.Empty)] },
                                new TableCellContent { Merge = new TableCellMerge { IsOrigin = false, OriginCellId = "origin" }, Blocks = [DocumentFormatTestData.Paragraph(string.Empty)] },
                                new TableCellContent { Blocks = [DocumentFormatTestData.Paragraph("Bottom right")] }
                            ]
                        }
                    ]
                }
            }
        ];

        var exported = await new DocumentOdtExporter().ExportAsync(source);
        var imported = await new DocumentOdtImporter().ImportAsync(new MemoryStream(exported.Content));

        var table = imported.Document.Blocks.Select(block => block.Content).OfType<TableBlockContent>().Single();
        table.Rows[0].Cells[0].ColumnSpan.Should().Be(2);
        table.Rows[0].Cells[0].RowSpan.Should().Be(2);
        table.Rows.SelectMany(row => row.Cells).Count(cell => !cell.Merge.IsOrigin).Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task RoundTrip_OdtHeadersFooters_PreservesTempoCompatibilityMetadata()
    {
        var source = DocumentEditorDocument.Empty("odt-headers-footers");
        source.Blocks = [DocumentFormatTestData.Paragraph("Body")];
        source.HeadersFooters.Add(new DocumentHeaderFooter
        {
            Type = DocumentHeaderFooterType.Header,
            Scope = DocumentHeaderFooterScope.Primary,
            Blocks = [DocumentFormatTestData.Paragraph("ODT header")]
        });
        source.HeadersFooters.Add(new DocumentHeaderFooter
        {
            Type = DocumentHeaderFooterType.Footer,
            Scope = DocumentHeaderFooterScope.FirstPage,
            Blocks = [DocumentFormatTestData.Paragraph("ODT footer")]
        });

        var exported = await new DocumentOdtExporter().ExportAsync(source);
        var imported = await new DocumentOdtImporter().ImportAsync(new MemoryStream(exported.Content));

        imported.Document.HeadersFooters.Should().Contain(headerFooter =>
            headerFooter.Type == DocumentHeaderFooterType.Header
            && headerFooter.Scope == DocumentHeaderFooterScope.Primary
            && FlattenHeaderFooterText(headerFooter).Contains("ODT header", StringComparison.Ordinal));
        imported.Document.HeadersFooters.Should().Contain(headerFooter =>
            headerFooter.Type == DocumentHeaderFooterType.Footer
            && headerFooter.Scope == DocumentHeaderFooterScope.FirstPage
            && FlattenHeaderFooterText(headerFooter).Contains("ODT footer", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RoundTrip_OdtComments_PreservesTempoCompatibilityMetadata()
    {
        var source = DocumentEditorDocument.Empty("odt-comments");
        source.Blocks =
        [
            new DocumentBlock
            {
                Type = DocumentBlockType.Paragraph,
                Order = 0,
                Content = new ParagraphBlockContent
                {
                    Inlines =
                    [
                        new TextRun
                        {
                            Text = "Commented ODT text",
                            Marks = [new InlineMark { Type = InlineMarkType.CommentAnchor, CommentAnchor = new CommentAnchorMarkData { CommentId = "comment-1" } }]
                        }
                    ]
                }
            }
        ];
        source.Comments.Add(new DocumentComment
        {
            Id = "comment-1",
            SourceFormat = "odt",
            Anchor = new DocumentCommentAnchor { Type = DocumentCommentAnchorType.TextRange, BlockId = source.Blocks[0].Id },
            Entries =
            [
                new DocumentCommentEntry
                {
                    Author = new DocumentEditorAuthor { DisplayName = "Reviewer" },
                    Text = "ODT comment"
                }
            ]
        });

        var exported = await new DocumentOdtExporter().ExportAsync(source);
        var imported = await new DocumentOdtImporter().ImportAsync(new MemoryStream(exported.Content));

        imported.Document.Comments.Should().Contain(comment =>
            comment.Entries.Any(entry => entry.Text == "ODT comment"));
        imported.Document.Blocks.Select(block => block.Content).OfType<ParagraphBlockContent>()
            .SelectMany(paragraph => paragraph.Inlines).OfType<TextRun>()
            .Should().Contain(run => run.Marks.Any(mark => mark.Type == InlineMarkType.CommentAnchor));
    }

    [Fact]
    public async Task RoundTrip_OdtFloatingImage_PreservesSupportedAnchorMetadata()
    {
        var exported = await new DocumentOdtExporter().ExportAsync(CreateFloatingImageDocument());
        var imported = await new DocumentOdtImporter().ImportAsync(new MemoryStream(exported.Content));

        var image = imported.Document.Blocks.Select(block => block.Content).OfType<ImageBlockContent>().Single();
        image.FloatingLayout.Should().NotBeNull();
        image.FloatingLayout!.Inline.Should().BeFalse();
        image.FloatingLayout.WrapMode.Should().Be(DocumentWrapMode.TopBottom);
        image.FloatingLayout.HorizontalRelativeTo.Should().Be(DocumentRelativePosition.Margin);
        image.FloatingLayout.VerticalRelativeTo.Should().Be(DocumentRelativePosition.Paragraph);
        image.FloatingLayout.X.Should().Be(36);
        image.FloatingLayout.Y.Should().Be(48);
        image.FloatingLayout.LockAnchor.Should().BeTrue();
        image.Size.Width.Should().Be(160);
        image.Size.Height.Should().Be(90);
    }

    private static string FlattenText(DocumentEditorDocument document)
    {
        return string.Join("\n", document.Blocks.Select(block => block.Content switch
        {
            ParagraphBlockContent paragraph => string.Concat(paragraph.Inlines.OfType<TextRun>().Select(run => run.Text)),
            HeadingBlockContent heading => string.Concat(heading.Inlines.OfType<TextRun>().Select(run => run.Text)),
            ListBlockContent list => string.Concat(list.Inlines.OfType<TextRun>().Select(run => run.Text)),
            TableBlockContent table => string.Join("\n", table.Rows.SelectMany(row => row.Cells).SelectMany(cell => cell.Blocks).Select(block => block.Content is ParagraphBlockContent paragraph ? string.Concat(paragraph.Inlines.OfType<TextRun>().Select(run => run.Text)) : string.Empty)),
            _ => string.Empty
        }));
    }

    private static string FlattenHeaderFooterText(DocumentHeaderFooter headerFooter)
    {
        return string.Join("\n", headerFooter.Blocks.Select(block => block.Content is ParagraphBlockContent paragraph
            ? string.Concat(paragraph.Inlines.OfType<TextRun>().Select(run => run.Text))
            : string.Empty));
    }

    private static DocumentEditorDocument CreateFloatingImageDocument()
    {
        var document = DocumentEditorDocument.Empty("floating-odt");
        document.Blocks.Add(new DocumentBlock
        {
            Id = "img-1",
            Type = DocumentBlockType.Image,
            Order = 10,
            Content = new ImageBlockContent
            {
                Source = DocumentImageSource.Url,
                Url = "https://example.test/image.png",
                AltText = "Floating image",
                Size = new DocumentImageSize { Width = 160, Height = 90 },
                FloatingLayout = new DocumentFloatingLayout
                {
                    Inline = false,
                    HorizontalRelativeTo = DocumentRelativePosition.Margin,
                    VerticalRelativeTo = DocumentRelativePosition.Paragraph,
                    X = 36,
                    Y = 48,
                    WrapMode = DocumentWrapMode.TopBottom,
                    ZIndex = 7,
                    LockAnchor = true
                }
            }
        });
        return document;
    }
}
