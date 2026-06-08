using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentFormats.Docx;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace Tempo.Blazor.DocumentFormats.Tests;

public class DocumentDocxFormatTests
{
    private const string TempoNamespace = "urn:tempo-blazor:document-editor:1.0";

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
        DocumentImagePersistence.EnumerateDrawingRuns(result.Document).Should().NotBeEmpty();
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

        var image = DocumentImagePersistence.EnumerateDrawingRuns(imported.Document).Single();
        image.Layout.IsInline.Should().BeFalse();
        image.Layout.Wrap.Mode.Should().Be(DocumentWrapMode.Square);
        image.Layout.Position.HorizontalRelativeTo.Should().Be(DocumentRelativePosition.Margin);
        image.Layout.Position.VerticalRelativeTo.Should().Be(DocumentRelativePosition.Paragraph);
        image.Layout.Position.X.Should().BeApproximately(36, 0.1);
        image.Layout.Position.Y.Should().BeApproximately(48, 0.1);
        image.Layout.Anchor.LockAnchor.Should().BeTrue();
    }

    [Fact]
    public async Task Phase19_ExportAsync_ImageSizeAndCaption_AreWrittenToDocx()
    {
        var exported = await new DocumentDocxExporter().ExportAsync(CreatePhase19DocxDocument());

        using var stream = new MemoryStream(exported.Content);
        using var word = WordprocessingDocument.Open(stream, false);
        var inline = word.MainDocumentPart!.Document.Body!.Descendants<DW.Inline>().Should().ContainSingle().Subject;

        inline.Extent!.Cx!.Value.Should().Be(240 * 12700);
        inline.Extent.Cy!.Value.Should().Be(120 * 12700);
        word.MainDocumentPart.Document.Body!.InnerText.Should().Contain("Phase 19 image caption");
    }

    [Fact]
    public async Task Phase19_RoundTrip_DocxImageProperties_PreserveSizeAndCaption()
    {
        var exported = await new DocumentDocxExporter().ExportAsync(CreatePhase19DocxDocument());
        var imported = await new DocumentDocxImporter().ImportAsync(new MemoryStream(exported.Content));

        var image = DocumentImagePersistence.EnumerateDrawingRuns(imported.Document).Single();
        image.Size.Width.Should().Be(240);
        image.Size.Height.Should().Be(120);
        image.Caption.Should().Be("Phase 19 image caption");
    }

    [Fact]
    public async Task Phase19_ExportAsync_TableWidthAndCellBackground_AreWrittenToDocx()
    {
        var exported = await new DocumentDocxExporter().ExportAsync(CreatePhase19DocxDocument());

        using var stream = new MemoryStream(exported.Content);
        using var word = WordprocessingDocument.Open(stream, false);
        var table = word.MainDocumentPart!.Document.Body!.Descendants<W.Table>().Should().ContainSingle().Subject;
        var tableProperties = table.GetFirstChild<W.TableProperties>()!;
        var cellProperties = table.Descendants<W.TableCell>().First().GetFirstChild<W.TableCellProperties>()!;

        tableProperties.GetFirstChild<W.TableWidth>()!.Type!.Value.Should().Be(W.TableWidthUnitValues.Dxa);
        tableProperties.GetFirstChild<W.TableWidth>()!.Width!.Value.Should().Be("7200");
        tableProperties.GetFirstChild<W.TableJustification>()!.Val!.Value.Should().Be(W.TableRowAlignmentValues.Center);
        cellProperties.GetFirstChild<W.TableCellWidth>()!.Width!.Value.Should().Be("2400");
        cellProperties.GetFirstChild<W.Shading>()!.Fill!.Value.Should().Be("FFEEAA");
        cellProperties.GetFirstChild<W.TableCellVerticalAlignment>()!.Val!.Value.Should().Be(W.TableVerticalAlignmentValues.Center);
    }

    [Fact]
    public async Task Phase19_RoundTrip_DocxTableProperties_PreserveSupportedLayout()
    {
        var exported = await new DocumentDocxExporter().ExportAsync(CreatePhase19DocxDocument());
        var imported = await new DocumentDocxImporter().ImportAsync(new MemoryStream(exported.Content));

        var table = imported.Document.Blocks.Select(block => block.Content).OfType<TableBlockContent>().Single();
        table.Layout.Width.Should().BeApproximately(360, 0.1);
        table.Layout.Alignment.Should().Be(TableHorizontalAlignment.Center);
        var cell = table.Rows[0].Cells[0];
        cell.Width.Should().BeApproximately(120, 0.1);
        cell.BackgroundColor.Should().Be("#FFEEAA");
        cell.VerticalAlignment.Should().Be(TableCellVerticalAlignment.Middle);
    }

    [Fact]
    public async Task Phase19_RoundTrip_DocxExtendedCanvasAnchors_PreserveEPhaseStructures()
    {
        var source = CreatePhase19ExtendedCanvasAnchorDocument();

        var exported = await new DocumentDocxExporter().ExportAsync(source);
        var imported = await new DocumentDocxImporter().ImportAsync(new MemoryStream(exported.Content));

        imported.Document.NumberingDefinitions.Should().Contain(definition => definition.Id == "phase19-legal-numbering" && definition.Levels.Count >= 2);
        imported.Document.ListStyles.Should().Contain(style => style.Id == "phase19-legal-list-style");
        imported.Document.Blocks.Select(block => block.Content).OfType<ListBlockContent>()
            .Should().Contain(list => list.NumberingId == "phase19-legal-numbering" && list.NumberingValue == 7);

        imported.Document.Styles.Should().Contain(style => style.Id == "heading-1" && style.HeadingLevel == 1);
        imported.Document.Sections.Should().Contain(section => section.Id == "phase19-columns-section" && section.Properties.Columns.Count == 2 && section.Properties.LineNumbering.Enabled);
        imported.Document.Sections.Should().Contain(section => section.Id == "phase19-landscape-section" && section.Properties.PageSettings.Landscape);
        imported.Document.Blocks.Select(block => block.Content).OfType<PageBreakBlockContent>()
            .Should().Contain(pageBreak => pageBreak.BreakType == DocumentSectionBreakType.NextPage && pageBreak.NextSectionId == "phase19-landscape-section");

        var inlines = EnumerateInlines(imported.Document.Blocks).ToList();
        inlines.OfType<DocumentFieldRun>().Should().Contain(field => field.Id == "phase19-field-page" && field.FieldType == DocumentFieldType.PageNumber);
        inlines.OfType<DocumentMathRun>().Should().Contain(math =>
            math.MathId == "phase19-equation"
            && math.Content.Elements.Any(element => element.Type == "fraction")
            && math.MathML != null
            && math.MathML.Contains("<mfrac>", StringComparison.Ordinal)
            && math.OmmlXml != null
            && math.OmmlXml.Contains("<m:oMath>", StringComparison.Ordinal));
        inlines.OfType<DocumentContentControlRun>().Should().Contain(control =>
            control.Control.ControlId == "phase19-inline-sdt"
            && control.Control.Kind == DocumentContentControlKind.DropDown
            && control.Control.Items.Count == 2);
        imported.Document.Blocks.Select(block => block.Content).OfType<ContentControlBlockContent>()
            .Should().Contain(control => control.Control.ControlId == "phase19-block-sdt" && control.Control.Kind == DocumentContentControlKind.RepeatingSection);
    }

    [Fact]
    public async Task ExportAsync_MathFlattening_IncludesSubSupAndNaryText()
    {
        var source = DocumentEditorDocument.Empty("docx-math-flatten");
        source.Blocks.Add(new DocumentBlock
        {
            Type = DocumentBlockType.Paragraph,
            Content = new ParagraphBlockContent
            {
                Inlines =
                [
                    new DocumentMathRun
                    {
                        MathId = "math-subsup-nary",
                        Content = new DocumentMathContent
                        {
                            Elements =
                            [
                                new DocumentMathElement
                                {
                                    Type = "subSup",
                                    Base = MathText("x"),
                                    Subscript = MathText("i"),
                                    Superscript = MathText("n")
                                },
                                new DocumentMathElement
                                {
                                    Type = "nary",
                                    Operator = "∑",
                                    LowerLimit = MathText("i=1"),
                                    UpperLimit = MathText("n"),
                                    Base = MathText("x_i")
                                }
                            ]
                        }
                    }
                ]
            }
        });

        var exported = await new DocumentDocxExporter().ExportAsync(source);

        using var stream = new MemoryStream(exported.Content);
        using var word = WordprocessingDocument.Open(stream, false);
        var bodyText = word.MainDocumentPart!.Document.Body!.InnerText;
        bodyText.Should().Contain("x_i^n");
        bodyText.Should().Contain("∑_i=1^n x_i");
    }

    [Fact]
    public async Task ImportAsync_UnknownSimpleField_PreservesRawInstructionForRoundTrip()
    {
        var package = CreateDocxWithSimpleField("MERGEFIELD Name", "Ada");
        var imported = await new DocumentDocxImporter().ImportAsync(new MemoryStream(package));

        var field = EnumerateInlines(imported.Document.Blocks).OfType<DocumentFieldRun>().Should().ContainSingle().Subject;
        field.FieldType.Should().Be(DocumentFieldType.Unknown);
        field.InstrText.Should().Be("MERGEFIELD Name");
        field.DisplayText.Should().Be("Ada");

        var exported = await new DocumentDocxExporter().ExportAsync(imported.Document);
        using var stream = new MemoryStream(exported.Content);
        using var word = WordprocessingDocument.Open(stream, false);
        word.MainDocumentPart!.Document.Body!.Descendants<W.SimpleField>().Should().ContainSingle()
            .Subject.Instruction!.Value.Should().Be("MERGEFIELD Name");
    }

    [Fact]
    public async Task ImportAsync_SectionPagesSimpleField_MapsToSectionPageCount()
    {
        var package = CreateDocxWithSimpleField("SECTIONPAGES", "4");
        var imported = await new DocumentDocxImporter().ImportAsync(new MemoryStream(package));

        var field = EnumerateInlines(imported.Document.Blocks).OfType<DocumentFieldRun>().Should().ContainSingle().Subject;
        field.FieldType.Should().Be(DocumentFieldType.SectionPageCount);
        field.InstrText.Should().Be("SECTIONPAGES");
    }

    [Fact]
    public async Task RoundTrip_SectionPageNumberAndCount_UseDistinctInstructions()
    {
        var source = DocumentEditorDocument.Empty("docx-section-page-fields");
        source.Blocks.Add(new DocumentBlock
        {
            Type = DocumentBlockType.Paragraph,
            Content = new ParagraphBlockContent
            {
                Inlines =
                [
                    new DocumentFieldRun { Id = "section-page-number", FieldType = DocumentFieldType.SectionPageNumber, FallbackText = "1" },
                    new TextRun { Text = " of " },
                    new DocumentFieldRun { Id = "section-page-count", FieldType = DocumentFieldType.SectionPageCount, FallbackText = "4" }
                ]
            }
        });

        var exported = await new DocumentDocxExporter().ExportAsync(source);
        using (var stream = new MemoryStream(exported.Content))
        using (var word = WordprocessingDocument.Open(stream, false))
        {
            word.MainDocumentPart!.Document.Body!.Descendants<W.SimpleField>()
                .Select(field => field.Instruction!.Value)
                .Should().Equal("PAGE", "SECTIONPAGES");
        }

        var imported = await new DocumentDocxImporter().ImportAsync(new MemoryStream(exported.Content));
        EnumerateInlines(imported.Document.Blocks).OfType<DocumentFieldRun>()
            .Should().Contain(field => field.Id == "section-page-number" && field.FieldType == DocumentFieldType.SectionPageNumber);
    }

    [Fact]
    public async Task Phase14_RoundTrip_DocxTables_PreserveCanvasTableParityGate()
    {
        var exported = await new DocumentDocxExporter().ExportAsync(CreatePhase14TableParityDocument());
        var imported = await new DocumentDocxImporter().ImportAsync(new MemoryStream(exported.Content), new DocumentFormatImportOptions
        {
            DocumentId = "phase14-docx-table-imported"
        });

        var table = imported.Document.Blocks.Select(block => block.Content).OfType<TableBlockContent>().Single();
        table.Layout.Width.Should().BeApproximately(420, 0.1);
        table.Layout.Alignment.Should().Be(TableHorizontalAlignment.Center);
        var horizontalMerge = table.Rows[0].Cells[0];
        horizontalMerge.ColumnSpan.Should().Be(2);
        horizontalMerge.Width.Should().BeApproximately(144, 0.1);
        horizontalMerge.BackgroundColor.Should().Be("#DCEBFF");
        horizontalMerge.VerticalAlignment.Should().Be(TableCellVerticalAlignment.Middle);
        GetBlockText(horizontalMerge.Blocks[0]).Should().Be("Merged columns");

        var verticalOrigin = table.Rows[1].Cells[0];
        var verticalContinuation = table.Rows[2].Cells[0];
        verticalOrigin.RowSpan.Should().Be(2);
        verticalOrigin.Merge.IsOrigin.Should().BeTrue();
        verticalContinuation.Merge.IsOrigin.Should().BeFalse();
        GetBlockText(verticalOrigin.Blocks[0]).Should().Be("Merged rows");
    }

    [Fact]
    public async Task Phase19_RoundTrip_DocxCommentsAndRevisions_RemainCompatibleWithMarkerMigration()
    {
        var source = DocumentEditorDocument.Empty("phase19-comments-revisions");
        source.Blocks.Add(new DocumentBlock
        {
            Type = DocumentBlockType.Paragraph,
            Content = new ParagraphBlockContent
            {
                Inlines =
                [
                    new TextRun
                    {
                        Text = "Commented",
                        Marks = [new InlineMark { Type = InlineMarkType.CommentAnchor, CommentAnchor = new CommentAnchorMarkData { CommentId = "comment-1" } }]
                    },
                    new TextRun
                    {
                        Text = " inserted",
                        Marks = [new InlineMark { Type = InlineMarkType.Revision, RevisionId = "revision-1" }]
                    }
                ]
            }
        });
        source.Comments.Add(new DocumentComment
        {
            Id = "comment-1",
            Entries =
            [
                new DocumentCommentEntry
                {
                    Author = new DocumentEditorAuthor { DisplayName = "Reviewer" },
                    Text = "Comment text"
                }
            ]
        });
        source.Revisions.Add(new DocumentRevision
        {
            Id = "revision-1",
            Type = DocumentRevisionType.Insertion,
            Author = new DocumentRevisionAuthor { DisplayName = "Reviewer" }
        });

        var exported = await new DocumentDocxExporter().ExportAsync(source);
        var imported = await new DocumentDocxImporter().ImportAsync(new MemoryStream(exported.Content));

        imported.Document.Comments.Should().NotBeEmpty();
        imported.Document.Revisions.Should().NotBeEmpty();
        imported.Document.Blocks.Select(block => block.Content).OfType<ParagraphBlockContent>()
            .SelectMany(paragraph => paragraph.Inlines)
            .OfType<TextRun>()
            .Should()
            .Contain(run => run.Marks.Any(mark => mark.Type == InlineMarkType.CommentAnchor || mark.Type == InlineMarkType.Revision));
    }

    [Fact]
    public async Task Phase19_ImportAsync_UnsupportedBodyConstruct_EmitsWarningAndKeepsFallbackContent()
    {
        await using var stream = CreateDocxWithUnsupportedBodyConstruct();

        var imported = await new DocumentDocxImporter().ImportAsync(stream);

        FlattenText(imported.Document).Should().Contain("Known paragraph");
        imported.Warnings.Should().Contain(warning =>
            warning.Code == "docx.unsupportedBodyElement"
            && warning.Severity == DocumentFormatCompatibilitySeverity.Warning);
    }

    [Fact]
    public async Task Phase14B_ExportAsync_ImageLayouts_WritesDocxNativeAndTempoMetadata()
    {
        var exported = await new DocumentDocxExporter().ExportAsync(DocumentFormatTestData.CreateImageLayoutParityDocument());

        using var stream = new MemoryStream(exported.Content);
        using var word = WordprocessingDocument.Open(stream, false);
        var anchors = word.MainDocumentPart!.Document.Body!.Descendants<DW.Anchor>().ToList();

        anchors.Should().HaveCount(5);
        anchors.Any(anchor => anchor.GetAttribute("layout-kind", TempoNamespace).Value == DocumentObjectLayoutKind.Fixed.ToString()).Should().BeTrue();
        anchors.Any(anchor => anchor.GetFirstChild<DW.WrapTopBottom>() is not null).Should().BeTrue();
        anchors.Any(anchor => anchor.BehindDoc?.Value == true).Should().BeTrue();
        anchors.Any(anchor => anchor.GetFirstChild<DW.HorizontalPosition>()?.GetFirstChild<DW.HorizontalAlignment>()?.Text == "left").Should().BeTrue();
        anchors.Any(anchor => anchor.GetFirstChild<DW.HorizontalPosition>()?.GetFirstChild<DW.HorizontalAlignment>()?.Text == "right").Should().BeTrue();
        anchors.Any(anchor => anchor.GetAttribute("allow-overlap", TempoNamespace).Value == "true").Should().BeTrue();
        word.MainDocumentPart.Document.Body!.Descendants<A.Transform2D>()
            .Any(transform => transform.Rotation?.Value != null)
            .Should()
            .BeTrue();
    }

    [Fact]
    public async Task Phase14B_RoundTrip_DocxImageLayouts_PreservesObjectLayout()
    {
        var exported = await new DocumentDocxExporter().ExportAsync(DocumentFormatTestData.CreateImageLayoutParityDocument());
        var imported = await new DocumentDocxImporter().ImportAsync(new MemoryStream(exported.Content));

        DocumentFormatTestData.AssertImageLayoutParity(imported.Document);
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

    private static DocumentMathContent MathText(string text) => new()
    {
        Elements = [new DocumentMathElement { Type = "run", Text = text }]
    };

    private static byte[] CreateDocxWithSimpleField(string instruction, string resultText)
    {
        using var memory = new MemoryStream();
        using (var word = WordprocessingDocument.Create(memory, WordprocessingDocumentType.Document, true))
        {
            var main = word.AddMainDocumentPart();
            var field = new W.SimpleField { Instruction = instruction };
            field.Append(new W.Run(new W.Text(resultText)));
            main.Document = new W.Document(new W.Body(new W.Paragraph(field)));
            main.Document.Save();
        }

        return memory.ToArray();
    }

    // ─── Phase 7: HorizontalPosition + Distance roundtrip ────────────────────

    [Fact]
    public async Task ExportAsync_HorizontalPositionRight_WritesHorizontalAlignRight()
    {
        var document = CreateFloatingImageDocumentWithPosition(
            DocumentImageHorizontalPosition.Right, distanceLeft: 8, distanceRight: 4);

        var exported = await new DocumentDocxExporter().ExportAsync(document);

        using var stream = new MemoryStream(exported.Content);
        using var word = WordprocessingDocument.Open(stream, false);
        var anchor = word.MainDocumentPart!.Document.Body!.Descendants<DW.Anchor>().Should().ContainSingle().Subject;
        var hAlign = anchor.GetFirstChild<DW.HorizontalPosition>()!.GetFirstChild<DW.HorizontalAlignment>();
        hAlign.Should().NotBeNull();
        hAlign!.Text.Should().Be("right");
        anchor.DistanceFromLeft!.Value.Should().BeGreaterThan(0u);
        anchor.DistanceFromRight!.Value.Should().BeGreaterThan(0u);
    }

    [Fact]
    public async Task ExportAsync_HorizontalPositionLeft_WritesHorizontalAlignLeft()
    {
        var document = CreateFloatingImageDocumentWithPosition(DocumentImageHorizontalPosition.Left);

        var exported = await new DocumentDocxExporter().ExportAsync(document);

        using var stream = new MemoryStream(exported.Content);
        using var word = WordprocessingDocument.Open(stream, false);
        var anchor = word.MainDocumentPart!.Document.Body!.Descendants<DW.Anchor>().Should().ContainSingle().Subject;
        var hAlign = anchor.GetFirstChild<DW.HorizontalPosition>()!.GetFirstChild<DW.HorizontalAlignment>();
        hAlign.Should().NotBeNull();
        hAlign!.Text.Should().Be("left");
    }

    [Fact]
    public async Task ImportAsync_HorizontalPositionRight_RoundTrips()
    {
        var source = CreateFloatingImageDocumentWithPosition(
            DocumentImageHorizontalPosition.Right, distanceLeft: 8, distanceRight: 4);

        var exported = await new DocumentDocxExporter().ExportAsync(source);
        var imported = await new DocumentDocxImporter().ImportAsync(new MemoryStream(exported.Content));

        var image = DocumentImagePersistence.EnumerateDrawingRuns(imported.Document).Single();
        image.Layout.Position.HorizontalAlignment.Should().Be(DocumentImageHorizontalPosition.Right);
        image.Layout.Wrap.DistanceLeft.Should().BeApproximately(8, 0.1);
        image.Layout.Wrap.DistanceRight.Should().BeApproximately(4, 0.1);
    }

    [Fact]
    public async Task ImportAsync_HorizontalPositionLeft_RoundTrips()
    {
        var source = CreateFloatingImageDocumentWithPosition(DocumentImageHorizontalPosition.Left);

        var exported = await new DocumentDocxExporter().ExportAsync(source);
        var imported = await new DocumentDocxImporter().ImportAsync(new MemoryStream(exported.Content));

        var image = DocumentImagePersistence.EnumerateDrawingRuns(imported.Document).Single();
        image.Layout.Position.HorizontalAlignment.Should().Be(DocumentImageHorizontalPosition.Left);
    }

    [Fact]
    public async Task ImportAsync_NoHorizontalPosition_ReturnsNullHorizontalPosition()
    {
        var exported = await new DocumentDocxExporter().ExportAsync(CreateFloatingImageDocument());
        var imported = await new DocumentDocxImporter().ImportAsync(new MemoryStream(exported.Content));

        var image = DocumentImagePersistence.EnumerateDrawingRuns(imported.Document).Single();
        image.Layout.Position.HorizontalAlignment.Should().BeNull();
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
                Url = DocumentFormatTestData.TransparentPngDataUrl,
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

    private static DocumentEditorDocument CreateFloatingImageDocumentWithPosition(
        DocumentImageHorizontalPosition position,
        double distanceLeft = 0,
        double distanceRight = 0)
    {
        var document = DocumentEditorDocument.Empty("floating-pos-docx");
        document.Blocks.Add(new DocumentBlock
        {
            Id = "img-pos-1",
            Type = DocumentBlockType.Image,
            Order = 10,
            Content = new ImageBlockContent
            {
                Source = DocumentImageSource.Url,
                Url = DocumentFormatTestData.TransparentPngDataUrl,
                AltText = "Positioned image",
                Size = new DocumentImageSize { Width = 160, Height = 90 },
                FloatingLayout = new DocumentFloatingLayout
                {
                    Inline = false,
                    HorizontalRelativeTo = DocumentRelativePosition.Margin,
                    VerticalRelativeTo = DocumentRelativePosition.Paragraph,
                    WrapMode = DocumentWrapMode.Square,
                    HorizontalPosition = position,
                    DistanceLeft = distanceLeft,
                    DistanceRight = distanceRight,
                    DistanceTop = 2,
                    DistanceBottom = 2
                }
            }
        });
        return document;
    }

    private static DocumentEditorDocument CreatePhase19DocxDocument()
    {
        var document = DocumentEditorDocument.Empty("phase19-docx");
        document.Blocks.Add(new DocumentBlock
        {
            Id = "image-1",
            Type = DocumentBlockType.Image,
            Order = 0,
            Content = new ImageBlockContent
            {
                Source = DocumentImageSource.Url,
                Url = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=",
                AltText = "Phase 19 image",
                Caption = "Phase 19 image caption",
                Size = new DocumentImageSize { Width = 240, Height = 120 }
            }
        });
        document.Blocks.Add(new DocumentBlock
        {
            Id = "table-1",
            Type = DocumentBlockType.Table,
            Order = 1,
            Content = new TableBlockContent
            {
                Layout = new TableLayoutContent
                {
                    Width = 360,
                    Alignment = TableHorizontalAlignment.Center
                },
                Rows =
                [
                    new TableRowContent
                    {
                        Cells =
                        [
                            new TableCellContent
                            {
                                Width = 120,
                                BackgroundColor = "#FFEEAA",
                                VerticalAlignment = TableCellVerticalAlignment.Middle,
                                Blocks = [DocumentFormatTestData.Paragraph("Styled cell")]
                            }
                        ]
                    }
                ]
            }
        });

        return document;
    }

    private static DocumentEditorDocument CreatePhase14TableParityDocument()
    {
        var document = DocumentEditorDocument.Empty("phase14-docx-table");
        document.Blocks.Add(new DocumentBlock
        {
            Id = "phase14-docx-table-block",
            Type = DocumentBlockType.Table,
            Order = 0,
            Content = new TableBlockContent
            {
                Layout = new TableLayoutContent
                {
                    Width = 420,
                    Alignment = TableHorizontalAlignment.Center,
                    BackgroundColor = "#FFFFFF"
                },
                Rows =
                [
                    new TableRowContent
                    {
                        Cells =
                        [
                            new TableCellContent
                            {
                                Id = "phase14-h-merge",
                                ColumnSpan = 2,
                                Width = 144,
                                BackgroundColor = "#DCEBFF",
                                VerticalAlignment = TableCellVerticalAlignment.Middle,
                                Blocks = [DocumentFormatTestData.Paragraph("Merged columns")]
                            },
                            new TableCellContent
                            {
                                Id = "phase14-h-side",
                                Blocks = [DocumentFormatTestData.Paragraph("Side")]
                            }
                        ]
                    },
                    new TableRowContent
                    {
                        Cells =
                        [
                            new TableCellContent
                            {
                                Id = "phase14-v-origin",
                                RowSpan = 2,
                                Merge = new TableCellMerge { IsOrigin = true },
                                Blocks = [DocumentFormatTestData.Paragraph("Merged rows")]
                            },
                            new TableCellContent
                            {
                                Id = "phase14-v-peer",
                                Blocks = [DocumentFormatTestData.Paragraph("Peer")]
                            }
                        ]
                    },
                    new TableRowContent
                    {
                        Cells =
                        [
                            new TableCellContent
                            {
                                Id = "phase14-v-continue",
                                Merge = new TableCellMerge { IsOrigin = false, OriginCellId = "phase14-v-origin" },
                                Blocks = [DocumentFormatTestData.Paragraph(string.Empty)]
                            },
                            new TableCellContent
                            {
                                Id = "phase14-v-tail",
                                Blocks = [DocumentFormatTestData.Paragraph("Tail")]
                            }
                        ]
                    }
                ]
            }
        });

        return document;
    }

    private static MemoryStream CreateDocxWithUnsupportedBodyConstruct()
    {
        var stream = new MemoryStream();
        using (var word = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var main = word.AddMainDocumentPart();
            main.Document = new W.Document(new W.Body(
                new W.Paragraph(new W.Run(new W.Text("Known paragraph"))),
                new W.AltChunk { Id = "missing-altchunk" }));
            main.Document.Save();
        }

        stream.Position = 0;
        return stream;
    }

    private static DocumentEditorDocument CreatePhase19ExtendedCanvasAnchorDocument()
    {
        var document = DocumentEditorDocument.Empty("phase19-extended-canvas-anchors");
        document.Sections[0].Id = "phase19-columns-section";
        document.Sections[0].Title = "Columns";
        document.Sections[0].Properties.Columns = new DocumentSectionColumns
        {
            Count = 2,
            Spacing = 24,
            SeparatorLine = true,
            Preset = "two"
        };
        document.Sections[0].Properties.LineNumbering = new DocumentLineNumbering
        {
            Enabled = true,
            StartAt = 1,
            Increment = 1,
            DistanceFromText = 10,
            Restart = DocumentLineNumberingRestart.Page
        };
        document.Sections.Add(new DocumentSection
        {
            Id = "phase19-landscape-section",
            Order = 1,
            Title = "Landscape",
            Properties = new DocumentSectionProperties
            {
                PageSettings = new DocumentPageSettings
                {
                    Size = DocumentPageSize.A4,
                    Landscape = true,
                    Margins = new DocumentPageMargins { Top = 48, Right = 48, Bottom = 48, Left = 48 }
                }
            }
        });

        document.Styles.Add(new DocumentStyleDefinition
        {
            Id = "heading-1",
            Name = "Heading 1",
            Type = DocumentStyleType.Paragraph,
            BasedOn = "normal",
            Next = "normal",
            IsQuickStyle = true,
            IsPrimary = true,
            HeadingLevel = 1
        });
        document.NumberingDefinitions.Add(new DocumentNumberingDefinition
        {
            Id = "phase19-legal-numbering",
            AbstractId = "phase19-legal-abstract",
            Name = "Phase 19 legal clauses",
            StyleId = "phase19-legal-list-style",
            Levels =
            [
                new DocumentNumberingLevel { Level = 0, Format = "decimal", Text = "%1.", StartAt = 1, Suffix = "tab", Indent = 0, Hanging = 18 },
                new DocumentNumberingLevel { Level = 1, Format = "decimal", Text = "%1.%2.", StartAt = 1, Suffix = "tab", Indent = 18, Hanging = 18 }
            ]
        });
        document.ListStyles.Add(new DocumentListStyle
        {
            Id = "phase19-legal-list-style",
            Name = "Phase 19 legal list",
            NumberingId = "phase19-legal-numbering",
            IsQuickStyle = true
        });

        document.Blocks =
        [
            new DocumentBlock
            {
                Id = "phase19-heading",
                SectionId = "phase19-columns-section",
                Type = DocumentBlockType.Heading,
                Order = 10,
                Content = new HeadingBlockContent
                {
                    Level = 1,
                    Inlines = [new TextRun { Id = "phase19-heading-run", Text = "Phase 19 extended DOCX anchors" }]
                }
            },
            new DocumentBlock
            {
                Id = "phase19-list",
                SectionId = "phase19-columns-section",
                Type = DocumentBlockType.List,
                Order = 20,
                Content = new ListBlockContent
                {
                    Ordered = true,
                    IndentLevel = 1,
                    StartNumber = 7,
                    NumberingId = "phase19-legal-numbering",
                    AbstractNumberingId = "phase19-legal-abstract",
                    ListStyleId = "phase19-legal-list-style",
                    NumberFormat = "legal",
                    LevelText = "%1.%2.",
                    Suffix = "tab",
                    LabelIndent = 18,
                    HangingIndent = 18,
                    RestartNumbering = true,
                    NumberingValue = 7,
                    Inlines = [new TextRun { Id = "phase19-list-run", Text = "Restarted legal clause" }]
                }
            },
            new DocumentBlock
            {
                Id = "phase19-inline-anchors",
                SectionId = "phase19-columns-section",
                Type = DocumentBlockType.Paragraph,
                Order = 30,
                Content = new ParagraphBlockContent
                {
                    Inlines =
                    [
                        new TextRun { Id = "phase19-inline-prefix", Text = "Anchors: page " },
                        new DocumentFieldRun { Id = "phase19-field-page", FieldType = DocumentFieldType.PageNumber, FallbackText = "1" },
                        new TextRun { Id = "phase19-math-prefix", Text = " equation " },
                        new DocumentMathRun
                        {
                            Id = "phase19-math-run",
                            MathId = "phase19-equation",
                            AltText = "(a+b)/c",
                            MathML = "<math><mfrac><mrow><mi>a</mi><mo>+</mo><mi>b</mi></mrow><mi>c</mi></mfrac></math>",
                            OmmlXml = "<m:oMath><m:f><m:num><m:r><m:t>a+b</m:t></m:r></m:num><m:den><m:r><m:t>c</m:t></m:r></m:den></m:f></m:oMath>",
                            Content = new DocumentMathContent
                            {
                                Elements =
                                [
                                    new DocumentMathElement
                                    {
                                        Type = "fraction",
                                        Numerator = MathRun("a+b"),
                                        Denominator = MathRun("c")
                                    }
                                ]
                            }
                        },
                        new TextRun { Id = "phase19-sdt-prefix", Text = " plan " },
                        new DocumentContentControlRun
                        {
                            Id = "phase19-inline-sdt-run",
                            Control = new DocumentContentControl
                            {
                                ControlId = "phase19-inline-sdt",
                                Kind = DocumentContentControlKind.DropDown,
                                Alias = "Plan",
                                Tag = "plan",
                                Value = new DocumentContentControlValue { SelectedValue = "pro" },
                                Items =
                                [
                                    new DocumentContentControlItem { DisplayText = "Basic", Value = "basic" },
                                    new DocumentContentControlItem { DisplayText = "Professional", Value = "pro" }
                                ]
                            }
                        }
                    ]
                }
            },
            new DocumentBlock
            {
                Id = "phase19-section-break",
                SectionId = "phase19-columns-section",
                Type = DocumentBlockType.PageBreak,
                Order = 40,
                Content = new PageBreakBlockContent
                {
                    BreakType = DocumentSectionBreakType.NextPage,
                    NextSectionId = "phase19-landscape-section"
                }
            },
            new DocumentBlock
            {
                Id = "phase19-block-sdt",
                SectionId = "phase19-landscape-section",
                Type = DocumentBlockType.ContentControl,
                Order = 50,
                Content = new ContentControlBlockContent
                {
                    Control = new DocumentContentControl
                    {
                        ControlId = "phase19-block-sdt",
                        Kind = DocumentContentControlKind.RepeatingSection,
                        Scope = DocumentContentControlScope.Block,
                        Alias = "Repeating clauses",
                        Tag = "clauses"
                    },
                    Blocks =
                    [
                        new DocumentBlock
                        {
                            Id = "phase19-block-sdt-child",
                            SectionId = "phase19-landscape-section",
                            Type = DocumentBlockType.Paragraph,
                            Order = 0,
                            Content = new ParagraphBlockContent
                            {
                                Inlines = [new TextRun { Id = "phase19-block-sdt-child-run", Text = "Nested clause item" }]
                            }
                        }
                    ]
                }
            }
        ];

        return document;

        static DocumentMathContent MathRun(string text) => new()
        {
            Elements = [new DocumentMathElement { Type = "run", Text = text }]
        };
    }

    private static IEnumerable<InlineContent> EnumerateInlines(IEnumerable<DocumentBlock> blocks)
    {
        foreach (var block in blocks)
        {
            foreach (var inline in EnumerateBlockInlines(block))
            {
                yield return inline;
            }
        }
    }

    private static IEnumerable<InlineContent> EnumerateBlockInlines(DocumentBlock block)
    {
        IEnumerable<InlineContent> ownInlines = block.Content switch
        {
            ParagraphBlockContent paragraph => paragraph.Inlines,
            HeadingBlockContent heading => heading.Inlines,
            ListBlockContent list => list.Inlines,
            QuoteBlockContent quote => quote.Inlines,
            _ => []
        };

        foreach (var inline in ownInlines)
        {
            yield return inline;
            if (inline is DocumentContentControlRun control)
            {
                foreach (var childInline in control.Inlines)
                {
                    yield return childInline;
                }
            }
        }

        if (block.Content is TableBlockContent table)
        {
            foreach (var cellBlock in table.Rows.SelectMany(row => row.Cells).SelectMany(cell => cell.Blocks))
            {
                foreach (var inline in EnumerateBlockInlines(cellBlock))
                {
                    yield return inline;
                }
            }
        }
        else if (block.Content is ContentControlBlockContent contentControl)
        {
            foreach (var childBlock in contentControl.Blocks)
            {
                foreach (var inline in EnumerateBlockInlines(childBlock))
                {
                    yield return inline;
                }
            }
        }
    }

    // ── Phase 13.4 – DOCX content control compatibility characterization tests ──

    [Fact]
    public async Task Export_ProtectedDocument_RoundTripsIsProtectedFlag()
    {
        var doc = DocumentFormatTestData.CreateDocument();
        var block = doc.Blocks.First(block => block.Content is ParagraphBlockContent);
        doc.IsProtected = true;
        doc.RestrictedMarkers.Add(new DocumentRestrictedMarker
        {
            Id = "editable-1",
            Label = "Editable clause",
            StartBlockId = block.Id,
            StartOffset = 0,
            EndBlockId = block.Id,
            EndOffset = 5
        });

        var exported = await new DocumentDocxExporter().ExportAsync(doc);

        using var stream = new MemoryStream(exported.Content);
        using var word = WordprocessingDocument.Open(stream, false);
        word.MainDocumentPart!.DocumentSettingsPart!.Settings!
            .GetFirstChild<W.DocumentProtection>()
            .Should()
            .NotBeNull();
        var sdt = word.MainDocumentPart!.Document.Body!.Descendants<W.SdtBlock>().Should().ContainSingle().Subject;
        sdt.SdtProperties!.GetFirstChild<W.Tag>()!.Val!.Value.Should().StartWith("tm-editable:editable-1");
    }

    [Fact]
    public async Task Import_ProtectedDocument_RoundTripsEditableRegion()
    {
        var source = DocumentFormatTestData.CreateDocument();
        var block = source.Blocks.First(block => block.Content is ParagraphBlockContent);
        source.IsProtected = true;
        source.RestrictedMarkers.Add(new DocumentRestrictedMarker
        {
            Id = "editable-1",
            Label = "Editable clause",
            StartBlockId = block.Id,
            StartOffset = 0,
            EndBlockId = block.Id,
            EndOffset = 5
        });

        var exported = await new DocumentDocxExporter().ExportAsync(source);
        var imported = await new DocumentDocxImporter().ImportAsync(new MemoryStream(exported.Content));

        imported.Document.IsProtected.Should().BeTrue();
        imported.Document.RestrictedMarkers.Should().ContainSingle(marker =>
            marker.Id == "editable-1"
            && marker.StartOffset == 0
            && marker.EndOffset == 5
            && marker.Label == "Editable clause");
    }

    [Fact]
    public async Task Import_RegularDocx_DoesNotSetIsProtected()
    {
        // Importing a standard DOCX that has no content-control restrictions should
        // leave IsProtected = false (no accidental protection injection).
        var exported = await new DocumentDocxExporter().ExportAsync(DocumentFormatTestData.CreateDocument());
        await using var stream = new MemoryStream(exported.Content);
        var result = await new DocumentDocxImporter().ImportAsync(stream, new DocumentFormatImportOptions
        {
            DocumentId = "compat-test",
            FileName = "test.docx"
        });

        result.Document.IsProtected.Should().BeFalse();
        result.Document.RestrictedMarkers.Should().BeEmpty();
    }

    private static string GetBlockText(DocumentBlock block)
        => block.Content is ParagraphBlockContent paragraph
            ? string.Concat(paragraph.Inlines.OfType<TextRun>().Select(run => run.Text))
            : string.Empty;
}
