using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using FluentAssertions;
using Tempo.Blazor.DataTableXlsx;
using Tempo.Blazor.Interfaces;

namespace Tempo.Blazor.Tests.Components.ImportExport;

/// <summary>
/// Unit tests for <see cref="XlsxImportFileParser"/> reading OOXML workbooks: header
/// detection, shared and inline strings, numeric/boolean cells, sparse rows with cell
/// gaps, and the no-header positional mode.
/// </summary>
public class XlsxImportFileParserTests
{
    /// <summary>Builds a one-sheet workbook. Each cell is (reference, value, isSharedString).</summary>
    private static MemoryStream BuildWorkbook(params (string Reference, string Value, bool Shared)[][] rows)
    {
        var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();

            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            var sheetData = new SheetData();
            worksheetPart.Worksheet = new Worksheet(sheetData);

            var sharedStrings = new List<string>();
            var rowIndex = 0u;
            foreach (var cells in rows)
            {
                rowIndex++;
                var row = new Row { RowIndex = rowIndex };
                foreach (var (reference, value, shared) in cells)
                {
                    var cell = new Cell { CellReference = reference };
                    if (shared)
                    {
                        var index = sharedStrings.IndexOf(value);
                        if (index < 0)
                        {
                            sharedStrings.Add(value);
                            index = sharedStrings.Count - 1;
                        }

                        cell.DataType = CellValues.SharedString;
                        cell.CellValue = new CellValue(index.ToString());
                    }
                    else
                    {
                        cell.CellValue = new CellValue(value);
                    }

                    row.Append(cell);
                }

                sheetData.Append(row);
            }

            if (sharedStrings.Count > 0)
            {
                var sharedPart = workbookPart.AddNewPart<SharedStringTablePart>();
                sharedPart.SharedStringTable = new SharedStringTable(
                    sharedStrings.Select(s => new SharedStringItem(new Text(s))));
            }

            var sheets = workbookPart.Workbook.AppendChild(new Sheets());
            sheets.Append(new Sheet
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = 1,
                Name = "List1"
            });
        }

        stream.Position = 0;
        return stream;
    }

    private static async Task<ImportParseResult> ParseAsync(MemoryStream stream, ImportParseOptions? options = null)
        => await new XlsxImportFileParser().ParseAsync(stream, options ?? new ImportParseOptions());

    [Fact]
    public async Task Parses_HeaderAndRows_WithSharedStrings()
    {
        using var workbook = BuildWorkbook(
            [("A1", "Name", true), ("B1", "Email", true)],
            [("A2", "Bedřich", true), ("B2", "b@x.com", true)],
            [("A3", "Alice", true), ("B3", "a@x.com", true)]);

        var result = await ParseAsync(workbook);

        result.Columns.Select(c => c.Name).Should().Equal("Name", "Email");
        result.Rows.Should().HaveCount(2);
        result.Rows[0].Should().Equal("Bedřich", "b@x.com");
        result.Rows[1].Should().Equal("Alice", "a@x.com");
    }

    [Fact]
    public async Task NumericAndInlineCells_KeepInvariantRawValues()
    {
        using var workbook = BuildWorkbook(
            [("A1", "Name", true), ("B1", "Age", true)],
            [("A2", "Alice", true), ("B2", "42.5", false)]);

        var result = await ParseAsync(workbook);

        result.Rows[0].Should().Equal("Alice", "42.5");
    }

    [Fact]
    public async Task SparseRow_WithCellGap_AlignsValuesToColumns()
    {
        // Row 2 has A and C but no B: the gap must stay an empty cell, not shift C left.
        using var workbook = BuildWorkbook(
            [("A1", "First", true), ("B1", "Middle", true), ("C1", "Last", true)],
            [("A2", "x", true), ("C2", "z", true)]);

        var result = await ParseAsync(workbook);

        result.Rows[0].Should().Equal("x", "", "z");
    }

    [Fact]
    public async Task NoHeaderMode_NamesColumnsPositionally()
    {
        using var workbook = BuildWorkbook(
            [("A1", "1", false), ("B1", "2", false)]);

        var result = await ParseAsync(workbook, new ImportParseOptions(HasHeaderRow: false));

        result.Columns.Select(c => c.Name).Should().Equal("Column 1", "Column 2");
        result.Rows.Should().HaveCount(1);
        result.Rows[0].Should().Equal("1", "2");
    }

    [Fact]
    public async Task EmptyWorkbook_YieldsNoColumnsAndNoRows()
    {
        using var workbook = BuildWorkbook();

        var result = await ParseAsync(workbook);

        result.Columns.Should().BeEmpty();
        result.Rows.Should().BeEmpty();
    }
}
