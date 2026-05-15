using DocumentFormat.OpenXml.Packaging;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentFormats.Docx;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;

namespace Tempo.Blazor.DocumentFormats.Tests;

public class DocumentDocxFormatTests
{
    [Fact]
    public async Task ExportAsync_CreatesOpenableDocxPackage()
    {
        var document = DocumentFormatTestData.CreateDocument();

        var result = await new DocumentDocxExporter().ExportAsync(document);

        result.ContentType.Should().Be("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        result.FileName.Should().EndWith(".docx");
        result.Content.Should().NotBeEmpty();

        using var stream = new MemoryStream(result.Content);
        using var word = WordprocessingDocument.Open(stream, false);
        word.MainDocumentPart.Should().NotBeNull();
        word.MainDocumentPart!.Document.Body!.InnerText.Should().Contain("Agreement");
        word.MainDocumentPart.Document.Body!.InnerText.Should().Contain("Numbered item");
        word.MainDocumentPart.Document.Body!.Descendants<DocumentFormat.OpenXml.Wordprocessing.Table>().Should().NotBeEmpty();
        word.MainDocumentPart.ImageParts.Should().NotBeEmpty();
        word.MainDocumentPart.HeaderParts.Should().NotBeEmpty();
        word.MainDocumentPart.FootnotesPart.Should().NotBeNull();
        word.MainDocumentPart.WordprocessingCommentsPart.Should().NotBeNull();
    }

    [Fact]
    public async Task ImportAsync_ReadsDocxParagraphsHeadingsStylesLinksListsTablesImagesAndNotes()
    {
        var exported = await new DocumentDocxExporter().ExportAsync(DocumentFormatTestData.CreateDocument());

        await using var stream = new MemoryStream(exported.Content);
        var result = await new DocumentDocxImporter().ImportAsync(stream, new DocumentFormatImportOptions
        {
            DocumentId = "imported-docx",
            FileName = "sample.docx"
        });

        result.Format.Should().Be(DocumentFormatKind.Docx);
        result.Document.DocumentId.Should().Be("imported-docx");
        result.Document.Blocks.OfType<DocumentBlock>().Any(b => b.Content is HeadingBlockContent).Should().BeTrue();
        result.Document.Blocks.OfType<DocumentBlock>().Any(b => b.Content is ListBlockContent { Ordered: true }).Should().BeTrue();
        result.Document.Blocks.OfType<DocumentBlock>().Any(b => b.Content is TableBlockContent).Should().BeTrue();
        result.Document.Blocks.OfType<DocumentBlock>().Any(b => b.Content is ImageBlockContent).Should().BeTrue();
        result.Document.Blocks.OfType<DocumentBlock>().Any(b => b.Content is PageBreakBlockContent).Should().BeTrue();
        result.Document.HeadersFooters.Should().NotBeEmpty();
        result.Document.Notes.Should().Contain(note => note.Type == DocumentNoteType.Footnote);
        result.Document.Comments.Should().NotBeEmpty();
        result.Document.Sections[0].Properties.PageSettings.Landscape.Should().BeTrue();

        var paragraph = result.Document.Blocks.Select(b => b.Content).OfType<ParagraphBlockContent>().First();
        paragraph.Inlines.OfType<TextRun>().Any(run => run.Marks.Any(mark => mark.Type == InlineMarkType.Bold)).Should().BeTrue();
        paragraph.Inlines.OfType<TextRun>().Any(run => run.Marks.Any(mark => mark.Type == InlineMarkType.Link && mark.Link is not null && mark.Link.Href == "https://example.test/")).Should().BeTrue();
    }

    [Fact]
    public async Task RoundTrip_DocxModelDocx_PreservesVisibleText()
    {
        var source = DocumentFormatTestData.CreateDocument();
        var exported = await new DocumentDocxExporter().ExportAsync(source);
        var imported = await new DocumentDocxImporter().ImportAsync(new MemoryStream(exported.Content));

        FlattenText(imported.Document).Should().Contain("Agreement");
        FlattenText(imported.Document).Should().Contain("Bold and link");
        FlattenText(imported.Document).Should().Contain("Merged");
    }

    [Fact]
    public async Task RoundTrip_DocxParagraphMarks_PreservesEmptyParagraphsAndPageBreaks()
    {
        var source = DocumentEditorDocument.Empty("docx-paragraph-marks");
        source.Blocks =
        [
            new DocumentBlock { Type = DocumentBlockType.Paragraph, Order = 0, Content = new ParagraphBlockContent { Inlines = [new TextRun { Text = "Before empty paragraph" }] } },
            new DocumentBlock { Type = DocumentBlockType.Paragraph, Order = 1, Content = new ParagraphBlockContent() },
            new DocumentBlock { Type = DocumentBlockType.PageBreak, Order = 2, Content = new PageBreakBlockContent() },
            new DocumentBlock { Type = DocumentBlockType.Paragraph, Order = 3, Content = new ParagraphBlockContent { Inlines = [new TextRun { Text = "After page break" }] } }
        ];

        var exported = await new DocumentDocxExporter().ExportAsync(source);
        var imported = await new DocumentDocxImporter().ImportAsync(new MemoryStream(exported.Content));

        imported.Document.Blocks.Any(block =>
            block.Content is ParagraphBlockContent paragraph && paragraph.Inlines.Count == 0).Should().BeTrue();
        imported.Document.Blocks.Should().Contain(block => block.Content is PageBreakBlockContent);
        FlattenText(imported.Document).Should().Contain("After page break");
    }

    [Fact]
    public async Task RoundTrip_DocxMergedCells_PreservesColumnAndRowSpans()
    {
        var source = DocumentEditorDocument.Empty("docx-merged-cells");
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
                                new TableCellContent { Blocks = [DocumentFormatTestData.Paragraph("Right")] }
                            ]
                        },
                        new TableRowContent
                        {
                            Cells =
                            [
                                new TableCellContent { ColumnSpan = 2, RowSpan = 2, Merge = new TableCellMerge { IsOrigin = false, OriginCellId = "origin" }, Blocks = [DocumentFormatTestData.Paragraph(string.Empty)] },
                                new TableCellContent { Blocks = [DocumentFormatTestData.Paragraph("Bottom right")] }
                            ]
                        }
                    ]
                }
            }
        ];

        var exported = await new DocumentDocxExporter().ExportAsync(source);
        var imported = await new DocumentDocxImporter().ImportAsync(new MemoryStream(exported.Content));

        var table = imported.Document.Blocks.Select(block => block.Content).OfType<TableBlockContent>().Single();
        table.Rows[0].Cells[0].ColumnSpan.Should().Be(2);
        table.Rows[0].Cells[0].RowSpan.Should().Be(2);
        table.Rows[1].Cells[0].Merge.IsOrigin.Should().BeFalse();
    }

    [Fact]
    public async Task RoundTrip_DocxHeadersFooters_PreservesSupportedScopes()
    {
        var source = DocumentEditorDocument.Empty("docx-headers-footers");
        source.Blocks = [new DocumentBlock { Type = DocumentBlockType.Paragraph, Order = 0, Content = new ParagraphBlockContent { Inlines = [new TextRun { Text = "Body" }] } }];
        source.HeadersFooters.Add(new DocumentHeaderFooter
        {
            Type = DocumentHeaderFooterType.Header,
            Scope = DocumentHeaderFooterScope.Primary,
            Blocks = [DocumentFormatTestData.Paragraph("Primary header")]
        });
        source.HeadersFooters.Add(new DocumentHeaderFooter
        {
            Type = DocumentHeaderFooterType.Footer,
            Scope = DocumentHeaderFooterScope.FirstPage,
            Blocks = [DocumentFormatTestData.Paragraph("First footer")]
        });

        var exported = await new DocumentDocxExporter().ExportAsync(source);
        var imported = await new DocumentDocxImporter().ImportAsync(new MemoryStream(exported.Content));

        imported.Document.HeadersFooters.Should().Contain(headerFooter =>
            headerFooter.Type == DocumentHeaderFooterType.Header
            && headerFooter.Scope == DocumentHeaderFooterScope.Primary
            && FlattenHeaderFooterText(headerFooter).Contains("Primary header", StringComparison.Ordinal));
        imported.Document.HeadersFooters.Should().Contain(headerFooter =>
            headerFooter.Type == DocumentHeaderFooterType.Footer
            && headerFooter.Scope == DocumentHeaderFooterScope.FirstPage
            && FlattenHeaderFooterText(headerFooter).Contains("First footer", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RoundTrip_DocxComments_PreservesCommentTextAndInlineAnchor()
    {
        var source = DocumentEditorDocument.Empty("docx-comments");
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
                            Text = "Commented text",
                            Marks = [new InlineMark { Type = InlineMarkType.CommentAnchor, CommentAnchor = new CommentAnchorMarkData { CommentId = "comment-1" } }]
                        }
                    ]
                }
            }
        ];
        source.Comments.Add(new DocumentComment
        {
            Id = "comment-1",
            Anchor = new DocumentCommentAnchor { Type = DocumentCommentAnchorType.TextRange, BlockId = source.Blocks[0].Id },
            Entries =
            [
                new DocumentCommentEntry
                {
                    Author = new DocumentEditorAuthor { DisplayName = "Reviewer" },
                    Text = "DOCX comment"
                }
            ]
        });

        var exported = await new DocumentDocxExporter().ExportAsync(source);
        var imported = await new DocumentDocxImporter().ImportAsync(new MemoryStream(exported.Content));

        imported.Document.Comments.Should().Contain(comment =>
            comment.Entries.Any(entry => entry.Text == "DOCX comment"));
        imported.Document.Blocks.Select(block => block.Content).OfType<ParagraphBlockContent>()
            .SelectMany(paragraph => paragraph.Inlines).OfType<TextRun>()
            .Should().Contain(run => run.Marks.Any(mark => mark.Type == InlineMarkType.CommentAnchor));
    }

    [Fact]
    public async Task RoundTrip_DocxTrackedChanges_PreservesSupportedInsertedAndDeletedRuns()
    {
        var source = DocumentEditorDocument.Empty("docx-track-changes");
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
                        new TextRun { Text = "Inserted", Marks = [new InlineMark { Type = InlineMarkType.Revision, RevisionId = "rev-ins" }] },
                        new TextRun { Text = " Deleted", Marks = [new InlineMark { Type = InlineMarkType.Revision, RevisionId = "rev-del" }] }
                    ]
                }
            }
        ];
        source.Revisions.Add(new DocumentRevision { Id = "rev-ins", Type = DocumentRevisionType.Insertion, Author = new DocumentRevisionAuthor { DisplayName = "Reviewer" } });
        source.Revisions.Add(new DocumentRevision { Id = "rev-del", Type = DocumentRevisionType.Deletion, Author = new DocumentRevisionAuthor { DisplayName = "Reviewer" } });

        var exported = await new DocumentDocxExporter().ExportAsync(source);
        var imported = await new DocumentDocxImporter().ImportAsync(new MemoryStream(exported.Content));

        imported.Document.Revisions.Should().Contain(revision => revision.Type == DocumentRevisionType.Insertion);
        imported.Document.Revisions.Should().Contain(revision => revision.Type == DocumentRevisionType.Deletion);
        imported.Document.Blocks.Select(block => block.Content).OfType<ParagraphBlockContent>()
            .SelectMany(paragraph => paragraph.Inlines).OfType<TextRun>()
            .Should().Contain(run => run.Marks.Any(mark => mark.Type == InlineMarkType.Revision));
    }

    [Fact]
    public async Task ExportAsync_FloatingImage_WritesAnchorMetadata()
    {
        var exported = await new DocumentDocxExporter().ExportAsync(CreateFloatingImageDocument());

        using var stream = new MemoryStream(exported.Content);
        using var word = WordprocessingDocument.Open(stream, false);
        var anchor = word.MainDocumentPart!.Document.Body!.Descendants<DW.Anchor>().Should().ContainSingle().Subject;

        anchor.GetFirstChild<DW.WrapSquare>().Should().NotBeNull();
        anchor.GetFirstChild<DW.HorizontalPosition>()!.RelativeFrom!.Value.Should().Be(DW.HorizontalRelativePositionValues.Margin);
        anchor.GetFirstChild<DW.VerticalPosition>()!.RelativeFrom!.Value.Should().Be(DW.VerticalRelativePositionValues.Paragraph);
        anchor.Locked!.Value.Should().BeTrue();
    }

    [Fact]
    public async Task ImportAsync_FloatingImage_ReadsAnchorMetadata()
    {
        var exported = await new DocumentDocxExporter().ExportAsync(CreateFloatingImageDocument());
        var imported = await new DocumentDocxImporter().ImportAsync(new MemoryStream(exported.Content));

        var image = imported.Document.Blocks.Select(block => block.Content).OfType<ImageBlockContent>().Single();
        image.FloatingLayout.Should().NotBeNull();
        image.FloatingLayout!.Inline.Should().BeFalse();
        image.FloatingLayout.WrapMode.Should().Be(DocumentWrapMode.Square);
        image.FloatingLayout.HorizontalRelativeTo.Should().Be(DocumentRelativePosition.Margin);
        image.FloatingLayout.VerticalRelativeTo.Should().Be(DocumentRelativePosition.Paragraph);
        image.FloatingLayout.X.Should().Be(36);
        image.FloatingLayout.Y.Should().Be(48);
        image.FloatingLayout.LockAnchor.Should().BeTrue();
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
        var document = DocumentEditorDocument.Empty("floating-docx");
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
                    WrapMode = DocumentWrapMode.Square,
                    ZIndex = 7,
                    LockAnchor = true
                }
            }
        });
        return document;
    }
}
