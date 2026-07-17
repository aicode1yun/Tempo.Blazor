using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Tempo.Blazor.Interfaces;

namespace Tempo.Blazor.DataTableXlsx;

/// <summary>
/// XLSX <see cref="IImportFileParser"/> reading the open OOXML format through the
/// DocumentFormat.OpenXml SDK. Reads the first worksheet: shared and inline strings are
/// resolved, numeric/boolean cells keep their raw invariant text, and sparse rows with
/// cell gaps stay aligned to columns by their A1 references. Rows are normalised to a
/// rectangular shape like the CSV parser.
/// </summary>
public sealed class XlsxImportFileParser : IImportFileParser
{
    /// <inheritdoc />
    public Task<ImportParseResult> ParseAsync(Stream stream, ImportParseOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(options);

        // The OpenXml SDK is synchronous; parsing happens in-memory on the provided stream.
        return Task.FromResult(Parse(stream, options));
    }

    private static ImportParseResult Parse(Stream stream, ImportParseOptions options)
    {
        using var document = SpreadsheetDocument.Open(stream, isEditable: false);
        var workbookPart = document.WorkbookPart;
        var worksheetPart = ResolveFirstWorksheet(workbookPart);
        var sheetData = worksheetPart?.Worksheet.GetFirstChild<SheetData>();
        if (workbookPart is null || sheetData is null)
        {
            return new ImportParseResult([], []);
        }

        var sharedStrings = workbookPart.SharedStringTablePart?.SharedStringTable?
            .Elements<SharedStringItem>()
            .Select(item => item.InnerText)
            .ToList();

        var records = new List<List<string>>();
        foreach (var row in sheetData.Elements<Row>())
        {
            var record = new List<string>();
            var nextColumn = 0;
            foreach (var cell in row.Elements<Cell>())
            {
                var columnIndex = ColumnIndexFromReference(cell.CellReference?.Value) ?? nextColumn;
                while (record.Count < columnIndex)
                {
                    record.Add(string.Empty);
                }

                record.Add(CellText(cell, sharedStrings));
                nextColumn = columnIndex + 1;
            }

            records.Add(record);
        }

        // Drop trailing all-empty records (formatting-only rows Excel sometimes keeps).
        while (records.Count > 0 && records[^1].All(string.IsNullOrEmpty))
        {
            records.RemoveAt(records.Count - 1);
        }

        if (records.Count == 0)
        {
            return new ImportParseResult([], []);
        }

        List<string>? header = null;
        var data = records;
        if (options.HasHeaderRow)
        {
            header = records[0];
            data = records.Skip(1).ToList();
        }

        var columnCount = header?.Count ?? 0;
        foreach (var record in data)
        {
            columnCount = Math.Max(columnCount, record.Count);
        }

        if (columnCount == 0)
        {
            return new ImportParseResult([], []);
        }

        var columns = new List<ImportColumn>(columnCount);
        for (var i = 0; i < columnCount; i++)
        {
            var name = header is not null && i < header.Count && !string.IsNullOrWhiteSpace(header[i])
                ? header[i]
                : $"Column {i + 1}";
            columns.Add(new ImportColumn(i, name));
        }

        var rows = new List<IReadOnlyList<string>>(data.Count);
        foreach (var record in data)
        {
            var normalized = new string[columnCount];
            for (var i = 0; i < columnCount; i++)
            {
                normalized[i] = i < record.Count ? record[i] : string.Empty;
            }

            rows.Add(normalized);
        }

        return new ImportParseResult(columns, rows);
    }

    /// <summary>
    /// Returns the FIRST sheet in workbook order (the order shown in Excel), falling back to
    /// any worksheet part — WorksheetParts enumeration order is not the sheet order.
    /// </summary>
    private static WorksheetPart? ResolveFirstWorksheet(WorkbookPart? workbookPart)
    {
        if (workbookPart is null)
        {
            return null;
        }

        var firstSheetId = workbookPart.Workbook.Sheets?.Elements<Sheet>().FirstOrDefault()?.Id?.Value;
        if (firstSheetId is not null && workbookPart.TryGetPartById(firstSheetId, out var part) && part is WorksheetPart worksheetPart)
        {
            return worksheetPart;
        }

        return workbookPart.WorksheetParts.FirstOrDefault();
    }

    private static string CellText(Cell cell, List<string>? sharedStrings)
    {
        var raw = cell.CellValue?.InnerText ?? cell.InlineString?.InnerText ?? string.Empty;

        if (cell.DataType?.Value == CellValues.SharedString
            && sharedStrings is not null
            && int.TryParse(raw, out var index)
            && index >= 0
            && index < sharedStrings.Count)
        {
            return sharedStrings[index];
        }

        if (cell.DataType?.Value == CellValues.InlineString)
        {
            return cell.InlineString?.InnerText ?? raw;
        }

        if (cell.DataType?.Value == CellValues.Boolean)
        {
            return raw == "1" ? "TRUE" : "FALSE";
        }

        return raw;
    }

    /// <summary>Converts the letter part of an A1 cell reference ("C7" → 2), or null when absent.</summary>
    private static int? ColumnIndexFromReference(string? reference)
    {
        if (string.IsNullOrEmpty(reference))
        {
            return null;
        }

        var index = 0;
        var sawLetter = false;
        foreach (var ch in reference)
        {
            if (ch is >= 'A' and <= 'Z')
            {
                index = index * 26 + (ch - 'A' + 1);
                sawLetter = true;
            }
            else if (ch is >= 'a' and <= 'z')
            {
                index = index * 26 + (ch - 'a' + 1);
                sawLetter = true;
            }
            else
            {
                break;
            }
        }

        return sawLetter ? index - 1 : null;
    }
}
