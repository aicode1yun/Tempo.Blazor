using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using FluentAssertions;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentFormats.Docx;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace Tempo.Blazor.DocumentFormats.Tests;

/// <summary>
/// Phase 9 DOCX fidelity: a third-party regression corpus. The packages here are built with RAW
/// OpenXml (never our exporter) and mimic the structures real producers emit — a Word-style
/// contract (style chains, formatted runs, merged table header, sectPr), a LibreOffice-style memo
/// (justified text, fragmented same-format runs, tabs, szCs-only run properties) and a Czech court
/// filing (w:lnNumType line numbering, č.l. header, diacritics). Each sample asserts (a) what the
/// first import must preserve and (b) import → export → re-import structural stability.
/// </summary>
public sealed class DocumentDocxThirdPartyCorpusTests
{
    // ── Word-style contract ────────────────────────────────────────────────

    [Fact]
    public async Task WordStyleContract_ImportPreservesHeadingsMarksAndMergedTable()
    {
        var document = await ImportAsync(BuildWordStyleContract());

        var heading = document.Blocks.Select(block => block.Content).OfType<HeadingBlockContent>().First();
        heading.Level.Should().Be(1);
        PlainText(heading.Inlines).Should().Be("Kupní smlouva");

        var inlines = document.Blocks.Select(block => block.Content)
            .OfType<ParagraphBlockContent>()
            .SelectMany(paragraph => paragraph.Inlines)
            .OfType<TextRun>()
            .ToList();
        inlines.Should().Contain(run => run.Text.Contains("prodávající")
            && run.Marks.Any(mark => mark.Type == InlineMarkType.Bold));
        inlines.Should().Contain(run => run.Text.Contains("kupující")
            && run.Marks.Any(mark => mark.Type == InlineMarkType.Italic));

        var table = document.Blocks.Select(block => block.Content).OfType<TableBlockContent>().Single();
        table.Rows.Should().HaveCount(2);
        table.Rows[0].Cells[0].ColumnSpan.Should().Be(2, "the merged header cell uses gridSpan=2");
        PlainText(table.Rows[1].Cells[1].Blocks).Should().Contain("100 000 Kč");
    }

    [Fact]
    public async Task WordStyleContract_SurvivesRoundTripStructurally()
        => await AssertRoundTripStableAsync(BuildWordStyleContract());

    // ── LibreOffice-style memo ─────────────────────────────────────────────

    [Fact]
    public async Task LibreOfficeStyleMemo_ImportPreservesJustificationFragmentedRunsAndTabs()
    {
        var document = await ImportAsync(BuildLibreOfficeStyleMemo());

        var paragraphs = document.Blocks.Select(block => block.Content).OfType<ParagraphBlockContent>().ToList();
        var justified = paragraphs.First(paragraph => PlainText(paragraph.Inlines).Contains("rozdělený do více runů"));
        var justifiedBlock = document.Blocks.First(block => ReferenceEquals(block.Content, justified));
        justifiedBlock.ParagraphProperties!.Alignment.Should().Be(DocumentTextAlignment.Justify);

        // LibreOffice fragments identically formatted text into several runs — the TEXT must
        // survive verbatim regardless of how runs are merged.
        PlainText(justified.Inlines).Should().Be("Tento odstavec je rozdělený do více runů se stejným formátem.");

        paragraphs.Select(paragraph => PlainText(paragraph.Inlines))
            .Should().Contain(text => text.Contains('\t'), "the tab character from w:tab must survive");
    }

    [Fact]
    public async Task LibreOfficeStyleMemo_SurvivesRoundTripStructurally()
        => await AssertRoundTripStableAsync(BuildLibreOfficeStyleMemo());

    // ── Czech court filing ─────────────────────────────────────────────────

    [Fact]
    public async Task CourtFiling_ImportPreservesLineNumberingHeaderAndDiacritics()
    {
        var document = await ImportAsync(BuildCourtFiling());

        var lineNumbering = document.Sections[0].Properties.LineNumbering;
        lineNumbering.Enabled.Should().BeTrue("w:lnNumType must be imported");
        lineNumbering.Increment.Should().Be(1);
        lineNumbering.Restart.Should().Be(DocumentLineNumberingRestart.Page);
        lineNumbering.DistanceFromText.Should().BeApproximately(18, 0.5, "360 twips = 18 pt");

        document.HeadersFooters.Should().Contain(part =>
            part.Blocks.SelectMany(block => BlockText(block)).Any(text => text.Contains("č.l.")),
            "the case-file margin note (č.l.) lives in the header");

        var texts = document.Blocks.Select(block => string.Concat(BlockText(block))).ToList();
        texts.Should().Contain(text => text.Contains("Okresnímu soudu v Praze"));
        texts.Should().Contain(text => text.Contains("Sp. zn.: 12 C 34/2026"));
        texts.Should().Contain(text => text.Contains("žalobce se domáhá zaplacení částky"));
    }

    [Fact]
    public async Task CourtFiling_SurvivesRoundTripStructurally()
    {
        var (first, second) = await RoundTripAsync(BuildCourtFiling());

        var firstLineNumbering = first.Sections[0].Properties.LineNumbering;
        var secondLineNumbering = second.Sections[0].Properties.LineNumbering;
        secondLineNumbering.Enabled.Should().Be(firstLineNumbering.Enabled);
        secondLineNumbering.Increment.Should().Be(firstLineNumbering.Increment);
        secondLineNumbering.Restart.Should().Be(firstLineNumbering.Restart);
        secondLineNumbering.DistanceFromText.Should().BeApproximately(firstLineNumbering.DistanceFromText, 0.5);

        second.HeadersFooters.Should().Contain(part =>
            part.Blocks.SelectMany(block => BlockText(block)).Any(text => text.Contains("č.l.")),
            "the č.l. header must survive the round trip");

        AssertSameBlockStructure(first, second);
    }

    // ── shared helpers ─────────────────────────────────────────────────────

    private static async Task AssertRoundTripStableAsync(MemoryStream source)
    {
        var (first, second) = await RoundTripAsync(source);
        AssertSameBlockStructure(first, second);
    }

    private static async Task<(DocumentEditorDocument First, DocumentEditorDocument Second)> RoundTripAsync(MemoryStream source)
    {
        var first = await ImportAsync(source);
        var exported = await new DocumentDocxExporter().ExportAsync(first);
        var second = (await new DocumentDocxImporter().ImportAsync(new MemoryStream(exported.Content))).Document!;
        return (first, second);
    }

    private static void AssertSameBlockStructure(DocumentEditorDocument first, DocumentEditorDocument second)
    {
        var firstShape = first.Blocks.Select(block => (block.Type, Text: string.Concat(BlockText(block)))).ToList();
        var secondShape = second.Blocks.Select(block => (block.Type, Text: string.Concat(BlockText(block)))).ToList();
        secondShape.Should().Equal(firstShape,
            "import → export → re-import must preserve block order, types and text verbatim");
    }

    private static async Task<DocumentEditorDocument> ImportAsync(MemoryStream stream)
    {
        stream.Position = 0;
        var result = await new DocumentDocxImporter().ImportAsync(stream);
        result.Document.Should().NotBeNull();
        return result.Document!;
    }

    private static IEnumerable<string> BlockText(DocumentBlock block)
        => block.Content switch
        {
            ParagraphBlockContent paragraph => [PlainText(paragraph.Inlines)],
            HeadingBlockContent heading => [PlainText(heading.Inlines)],
            ListBlockContent list => [PlainText(list.Inlines)],
            QuoteBlockContent quote => [PlainText(quote.Inlines)],
            TableBlockContent table => table.Rows.SelectMany(row => row.Cells).Select(PlainTextOfCell),
            _ => [string.Empty]
        };

    private static string PlainTextOfCell(TableCellContent cell)
        => string.Concat(cell.Blocks.SelectMany(BlockText));

    private static string PlainText(IEnumerable<DocumentBlock> blocks)
        => string.Concat(blocks.SelectMany(BlockText));

    private static string PlainText(IEnumerable<InlineContent> inlines)
        => string.Concat(inlines.OfType<TextRun>().Select(run => run.Text));

    // ── corpus builders (raw OpenXml — deliberately NOT our exporter) ──────

    private static MemoryStream BuildWordStyleContract()
    {
        var stream = new MemoryStream();
        using (var word = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = word.AddMainDocumentPart();

            var stylesPart = main.AddNewPart<StyleDefinitionsPart>();
            stylesPart.Styles = new W.Styles(
                new W.Style(new W.StyleName { Val = "Normal" }) { Type = W.StyleValues.Paragraph, StyleId = "Normal", Default = true },
                new W.Style(
                    new W.StyleName { Val = "heading 1" },
                    new W.BasedOn { Val = "Normal" },
                    new W.StyleParagraphProperties(new W.OutlineLevel { Val = 0 }),
                    new W.StyleRunProperties(new W.Bold(), new W.FontSize { Val = "32" }))
                { Type = W.StyleValues.Paragraph, StyleId = "Heading1" });

            main.Document = new W.Document(new W.Body(
                new W.Paragraph(
                    new W.ParagraphProperties(new W.ParagraphStyleId { Val = "Heading1" }),
                    new W.Run(new W.Text("Kupní smlouva"))),
                new W.Paragraph(
                    new W.Run(new W.RunProperties(new W.Bold()), new W.Text("Jan Novák, prodávající") { Space = SpaceProcessingModeValues.Preserve }),
                    new W.Run(new W.Text(" a ") { Space = SpaceProcessingModeValues.Preserve }),
                    new W.Run(new W.RunProperties(new W.Italic()), new W.Text("Petr Svoboda, kupující") { Space = SpaceProcessingModeValues.Preserve })),
                new W.Table(
                    new W.TableProperties(new W.TableBorders(
                        new W.TopBorder { Val = W.BorderValues.Single, Size = 4 },
                        new W.BottomBorder { Val = W.BorderValues.Single, Size = 4 })),
                    new W.TableGrid(new W.GridColumn(), new W.GridColumn()),
                    new W.TableRow(
                        new W.TableCell(
                            new W.TableCellProperties(new W.GridSpan { Val = 2 }, new W.Shading { Fill = "DDDDDD" }),
                            new W.Paragraph(new W.Run(new W.Text("Předmět koupě"))))),
                    new W.TableRow(
                        new W.TableCell(new W.Paragraph(new W.Run(new W.Text("Cena")))),
                        new W.TableCell(new W.Paragraph(new W.Run(new W.Text("100 000 Kč")))))),
                new W.SectionProperties(
                    new W.PageSize { Width = 11906, Height = 16838 },
                    new W.PageMargin { Top = 1417, Right = 1417, Bottom = 1417, Left = 1417 })));
            main.Document.Save();
        }

        stream.Position = 0;
        return stream;
    }

    private static MemoryStream BuildLibreOfficeStyleMemo()
    {
        var stream = new MemoryStream();
        using (var word = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = word.AddMainDocumentPart();
            main.Document = new W.Document(new W.Body(
                // LibreOffice fragments identically formatted text into multiple runs and often
                // emits empty rPr or szCs-only run properties.
                new W.Paragraph(
                    new W.ParagraphProperties(new W.Justification { Val = W.JustificationValues.Both }),
                    new W.Run(new W.RunProperties(), new W.Text("Tento odstavec ") { Space = SpaceProcessingModeValues.Preserve }),
                    new W.Run(new W.RunProperties(new W.FontSizeComplexScript { Val = "24" }), new W.Text("je rozdělený ") { Space = SpaceProcessingModeValues.Preserve }),
                    new W.Run(new W.Text("do více runů ") { Space = SpaceProcessingModeValues.Preserve }),
                    new W.Run(new W.Text("se stejným formátem.") { Space = SpaceProcessingModeValues.Preserve })),
                new W.Paragraph(
                    new W.Run(new W.Text("Položka:") { Space = SpaceProcessingModeValues.Preserve }),
                    new W.Run(new W.TabChar()),
                    new W.Run(new W.Text("hodnota po tabulátoru") { Space = SpaceProcessingModeValues.Preserve })),
                new W.SectionProperties(
                    new W.PageSize { Width = 11906, Height = 16838 },
                    new W.PageMargin { Top = 1134, Right = 1134, Bottom = 1134, Left = 1134 })));
            main.Document.Save();
        }

        stream.Position = 0;
        return stream;
    }

    private static MemoryStream BuildCourtFiling()
    {
        var stream = new MemoryStream();
        using (var word = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = word.AddMainDocumentPart();

            var headerPart = main.AddNewPart<HeaderPart>();
            headerPart.Header = new W.Header(
                new W.Paragraph(
                    new W.ParagraphProperties(new W.Justification { Val = W.JustificationValues.Right }),
                    new W.Run(new W.Text("č.l. ______") { Space = SpaceProcessingModeValues.Preserve })));
            var headerId = main.GetIdOfPart(headerPart);

            main.Document = new W.Document(new W.Body(
                new W.Paragraph(new W.Run(new W.Text("Okresnímu soudu v Praze"))),
                new W.Paragraph(new W.Run(new W.Text("Sp. zn.: 12 C 34/2026"))),
                new W.Paragraph(
                    new W.ParagraphProperties(new W.Justification { Val = W.JustificationValues.Both }),
                    new W.Run(new W.Text("I. Skutkový stav: žalobce se domáhá zaplacení částky 250 000 Kč s příslušenstvím.") { Space = SpaceProcessingModeValues.Preserve })),
                new W.Paragraph(
                    new W.ParagraphProperties(new W.Justification { Val = W.JustificationValues.Both }),
                    new W.Run(new W.Text("II. Právní posouzení: nárok vyplývá ze smlouvy o dílo uzavřené dne 1. 3. 2026.") { Space = SpaceProcessingModeValues.Preserve })),
                new W.SectionProperties(
                    new W.HeaderReference { Type = W.HeaderFooterValues.Default, Id = headerId },
                    new W.LineNumberType { CountBy = 1, Restart = W.LineNumberRestartValues.NewPage, Distance = "360" },
                    new W.PageSize { Width = 11906, Height = 16838 },
                    new W.PageMargin { Top = 1417, Right = 1417, Bottom = 1417, Left = 1985 })));
            main.Document.Save();
        }

        stream.Position = 0;
        return stream;
    }
}
