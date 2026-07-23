using System.Security.Cryptography;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using Dm = Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentFormats.Docx;
using Tempo.Blazor.DocumentFormats.Notion;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Nm = Tempo.Blazor.NotionEditor.Models;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace Tempo.Blazor.DocumentFormats.Tests;

public sealed class KrDocxNotionRoundTripTests
{
    private const string FixtureSha256 =
        "2086363c3535842cafa882154e1f881111d9fe70f4650d17e6bc653fb350e035";

    [Fact]
    public async Task Fixture_IsByteIdenticalToRecordedKrDocxSource()
    {
        await using var stream = File.OpenRead(FixturePath);

        var digest = Convert.ToHexString(await SHA256.HashDataAsync(stream)).ToLowerInvariant();

        digest.Should().Be(FixtureSha256);
        new FileInfo(FixturePath).Length.Should().Be(18_905);
    }

    [Fact]
    public async Task Import_KrDocx_ReadsBothTablesAndCompleteMergedArea()
    {
        var imported = await ImportFixtureAsync();
        var tables = imported.Document.Blocks
            .Where(block => block.Type == Dm.DocumentBlockType.Table)
            .Select(block => (Dm.TableBlockContent)block.Content)
            .ToList();

        tables.Should().HaveCount(2);

        var threshold = tables[0];
        threshold.Rows.Should().HaveCount(6, "the source contains a header plus five data rows");
        threshold.Rows.Should().OnlyContain(row =>
            row.Cells.Sum(cell => Math.Max(1, cell.ColumnSpan)) == 8);
        var merged = threshold.Rows[2].Cells[1];
        merged.ColumnSpan.Should().Be(7);
        merged.RowSpan.Should().Be(4);
        merged.Merge.IsOrigin.Should().BeTrue();
        threshold.Rows.Skip(3).Should().OnlyContain(row =>
            row.Cells.Single(cell => !cell.Merge.IsOrigin).Merge.OriginCellId == merged.Id);

        var impact = tables[1];
        impact.Rows.Should().HaveCount(6);
        impact.Rows.Should().OnlyContain(row => row.Cells.Count == 2);
    }

    [Fact]
    public async Task DocumentModel_Notion_DocumentModel_PreservesKrRichTableSemantics()
    {
        var imported = await ImportFixtureAsync();

        var notion = DocumentModelToNotionConverter.ConvertDocument(
            imported.Document,
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
        var restorationWarnings = notion.Warnings.ToList();
        var restored = NotionToDocumentModelConverter.ConvertBlocks(
            notion.Blocks,
            restorationWarnings);
        restorationWarnings.Should().NotContain(warning =>
            warning.Code.StartsWith("table_", StringComparison.Ordinal));

        var notionTables = ReadNotionTables(notion.Blocks);
        notionTables.Should().HaveCount(2);
        var threshold = notionTables[0];
        threshold.Table.ColumnCount.Should().Be(8);
        threshold.Rows.Should().HaveCount(6);
        threshold.Rows[2].Cells.Should().ContainSingle(cell =>
            cell.ColumnSpan == 7 && cell.RowSpan == 4);

        var impact = notionTables[1];
        impact.Table.ColumnCount.Should().Be(2);
        impact.Rows.Should().HaveCount(6);

        var fills = notionTables
            .SelectMany(table => table.Rows)
            .SelectMany(row => row.Cells)
            .Select(cell => cell.BackgroundColor)
            .Where(color => color is not null)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var expectedFill in new[]
        {
            "#FDE9D9",
            "#FCD5B4",
            "#FF0000",
            "#FF3300",
            "#FFC000",
            "#FFFF00",
            "#76933C"
        })
        {
            fills.Should().Contain(color =>
                color.Equals(expectedFill, StringComparison.OrdinalIgnoreCase));
        }

        impact.Rows.SelectMany(row => row.Cells)
            .SelectMany(cell => cell.Inlines)
            .Should().Contain(inline => inline.Bold);
        impact.Rows[1].Cells[1].HorizontalAlignment
            .Should().Be(Nm.NotionTableHorizontalAlignment.Center);
        impact.Rows.SelectMany(row => row.Cells)
            .SelectMany(cell => cell.Inlines)
            .Should().Contain(inline =>
                string.Equals(inline.TextColor, "#000000", StringComparison.OrdinalIgnoreCase));

        var restoredTables = restored
            .Where(block => block.Type == Dm.DocumentBlockType.Table)
            .Select(block => (Dm.TableBlockContent)block.Content)
            .ToList();
        restoredTables.Should().HaveCount(2);
        restoredTables[0].Rows[2].Cells[1].ColumnSpan.Should().Be(7);
        restoredTables[0].Rows[2].Cells[1].RowSpan.Should().Be(4);
        restoredTables.SelectMany(table => table.Rows)
            .SelectMany(row => row.Cells)
            .Select(cell => cell.BackgroundColor)
            .Should().Contain(color =>
                color != null &&
                color.Equals("#FF0000", StringComparison.OrdinalIgnoreCase));
        GetText(restoredTables[1].Rows[1].Cells[0]).Should().Be("Very high");
        GetText(restoredTables[1].Rows[5].Cells[0]).Should().Be("Very low");
    }

    [Fact]
    public async Task FullRoundTrip_ExportsOpenableDocxWithBothKrTables()
    {
        var imported = await ImportFixtureAsync();
        var notion = DocumentModelToNotionConverter.ConvertDocument(
            imported.Document,
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
        var restored = NotionToDocumentModelConverter.ConvertBlocks(notion.Blocks);
        var document = imported.Document;
        document.Blocks = restored;

        var exported = await new DocumentDocxExporter().ExportAsync(document);

        using var stream = new MemoryStream(exported.Content);
        using var package = WordprocessingDocument.Open(stream, false);
        var tables = package.MainDocumentPart!.Document.Body!
            .Descendants<DocumentFormat.OpenXml.Wordprocessing.Table>()
            .ToList();
        tables.Should().HaveCount(2);
        tables[0].Elements<DocumentFormat.OpenXml.Wordprocessing.TableRow>()
            .Should().HaveCount(6);
        tables[1].Elements<DocumentFormat.OpenXml.Wordprocessing.TableRow>()
            .Should().HaveCount(6);
    }

    [Fact]
    public void ConvertDocument_UnsupportedTableDetail_EmitsStructuredSourcePathWarning()
    {
        var document = Dm.DocumentEditorDocument.Empty();
        document.Blocks =
        [
            new Dm.DocumentBlock
            {
                Id = "table-1",
                Type = Dm.DocumentBlockType.Table,
                Content = new Dm.TableBlockContent
                {
                    Layout = new Dm.TableLayoutContent
                    {
                        Width = 640,
                        Borders = new Dm.TableCellBorders
                        {
                            Bottom = "1px solid #123456"
                        }
                    },
                    Rows =
                    [
                        new Dm.TableRowContent
                        {
                            Cells =
                            [
                                new Dm.TableCellContent
                                {
                                    Borders = new Dm.TableCellBorders
                                    {
                                        Top = "3px groove #123456"
                                    },
                                    Blocks =
                                    [
                                        new Dm.DocumentBlock
                                        {
                                            Type = Dm.DocumentBlockType.Paragraph,
                                            Content = new Dm.ParagraphBlockContent
                                            {
                                                Inlines = [new Dm.TextRun { Text = "Warning" }]
                                            }
                                        }
                                    ]
                                }
                            ]
                        }
                    ]
                }
            }
        ];

        var converted = DocumentModelToNotionConverter.ConvertDocument(document, Guid.NewGuid());

        converted.Warnings.Should().ContainSingle(warning =>
            warning.Code == "document.table.compatibility" &&
            warning.SourcePath ==
            "document.blocks[table-1].table.rows[0].cells[0].borders.top");
        converted.Warnings.Should().Contain(warning =>
            warning.Code == "document.table.compatibility" &&
            warning.SourcePath == "document.blocks[table-1].table.layout.width");
        converted.Warnings.Should().Contain(warning =>
            warning.Code == "document.table.compatibility" &&
            warning.SourcePath == "document.blocks[table-1].table.layout.borders.bottom");
    }

    [Fact]
    public async Task Import_UnsupportedDocxCellBorder_EmitsStructuredSourcePathWarning()
    {
        await using var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(
                   stream,
                   WordprocessingDocumentType.Document,
                   true))
        {
            var mainPart = package.AddMainDocumentPart();
            mainPart.Document = new W.Document(
                new W.Body(
                    new W.Table(
                        new W.TableRow(
                            new W.TableCell(
                                new W.TableCellProperties(
                                    new W.TableCellBorders(
                                        new W.TopBorder
                                        {
                                            Val = W.BorderValues.Wave
                                        })),
                                new W.Paragraph(new W.Run(new W.Text("Warning"))))))));
            mainPart.Document.Save();
        }

        stream.Position = 0;
        var imported = await new DocumentDocxImporter().ImportAsync(
            stream,
            new DocumentFormatImportOptions { DocumentId = "unsupported-border" });

        imported.Warnings.Should().ContainSingle(warning =>
            warning.Code == "docx.tableBorderUnsupported" &&
            warning.SourcePath != null &&
            warning.SourcePath.EndsWith(".borders.top", StringComparison.Ordinal));
    }

    private static string FixturePath => Path.Combine(
        AppContext.BaseDirectory,
        "TestData",
        "KR.docx");

    private static async Task<DocumentFormatImportResult> ImportFixtureAsync()
    {
        await using var stream = File.OpenRead(FixturePath);
        return await new DocumentDocxImporter().ImportAsync(
            stream,
            new DocumentFormatImportOptions
            {
                DocumentId = "kr-docx-notion-fixture",
                FileName = "KR.docx"
            });
    }

    private static List<(Nm.ITableBlockContent Table, List<Nm.NotionAuthoringTableRow> Rows)>
        ReadNotionTables(IReadOnlyList<IPageBlock> blocks)
    {
        return blocks
            .Where(block => block.Type == BlockType.Table)
            .OrderBy(block => block.Order)
            .Select(block =>
            {
                var rows = blocks
                    .Where(row =>
                        row.Type == BlockType.TableRow &&
                        row.ParentBlockId == block.Id)
                    .OrderBy(row => row.Order)
                    .Select(row =>
                    {
                        var content = (Nm.ITableRowBlockContent)row.Content;
                        return new Nm.NotionAuthoringTableRow
                        {
                            Cells = content.RichCells
                                .Where(cell => !cell.IsMergeHidden)
                                .Select(cell => new Nm.NotionAuthoringTableCell
                                {
                                    Html = cell.Html,
                                    Inlines = cell.Inlines,
                                    BackgroundColor = cell.BackgroundColor,
                                    TextColor = cell.TextColor,
                                    HorizontalAlignment = cell.HorizontalAlignment,
                                    VerticalAlignment = cell.VerticalAlignment,
                                    RowSpan = cell.RowSpan,
                                    ColumnSpan = cell.ColSpan,
                                    Width = cell.Width,
                                    Borders = cell.Borders
                                })
                                .ToList()
                        };
                    })
                    .ToList();
                return ((Nm.ITableBlockContent)block.Content, rows);
            })
            .ToList();
    }

    private static string GetText(Dm.TableCellContent cell)
        => string.Concat(
            cell.Blocks
                .OrderBy(block => block.Order)
                .SelectMany(block => block.Content switch
                {
                    Dm.ParagraphBlockContent paragraph => paragraph.Inlines,
                    Dm.HeadingBlockContent heading => heading.Inlines,
                    _ => []
                })
                .OfType<Dm.TextRun>()
                .Select(run => run.Text));
}
