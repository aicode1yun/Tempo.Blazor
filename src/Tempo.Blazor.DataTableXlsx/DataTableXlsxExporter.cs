using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Tempo.Blazor.Models;

namespace Tempo.Blazor.Export;

/// <summary>
/// Typed XLSX exporter for <c>TmDataTable</c>, backed by DocumentFormat.OpenXml.
/// </summary>
public sealed class DataTableXlsxExporter : IDataTableXlsxExporter, IDataTableExporter
{
    private const uint DateTimeStyleIndex = 1U;
    private const uint DateTimeNumberFormatId = 164U;

    /// <inheritdoc />
    public string Format => "xlsx";

    /// <inheritdoc />
    public string ContentType => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <inheritdoc />
    public string FileExtension => "xlsx";

    /// <inheritdoc />
    public byte[] Export(DataTableExportData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        using var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();
            AddStyles(workbookPart);

            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            var sheetData = new SheetData();
            worksheetPart.Worksheet = new Worksheet(sheetData);

            workbookPart.Workbook.AppendChild(new Sheets()).Append(new Sheet
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = 1U,
                Name = SheetName(data.Name)
            });

            sheetData.Append(BuildRow(data.Headers.Cast<object?>()));
            var rowCount = Math.Max(data.Values.Count, data.Rows.Count);
            for (var rowIndex = 0; rowIndex < rowCount; rowIndex++)
            {
                var rawRow = rowIndex < data.Values.Count ? data.Values[rowIndex] : [];
                var displayRow = rowIndex < data.Rows.Count ? data.Rows[rowIndex] : [];
                var cells = new object?[data.Headers.Count];
                for (var columnIndex = 0; columnIndex < cells.Length; columnIndex++)
                {
                    cells[columnIndex] = columnIndex < rawRow.Count
                        ? rawRow[columnIndex]
                        : columnIndex < displayRow.Count ? displayRow[columnIndex] : null;
                }

                sheetData.Append(BuildRow(cells));
            }

            workbookPart.Workbook.Save();
        }

        return stream.ToArray();
    }

    private static Row BuildRow(IEnumerable<object?> values)
    {
        var row = new Row();
        foreach (var value in values)
        {
            row.Append(CellFor(value));
        }

        return row;
    }

    private static Cell CellFor(object? value) => value switch
    {
        null => TextCell(string.Empty),
        string text => TextCell(text),
        DateTime dateTime when dateTime.Year >= 100 => NumberCell(dateTime.ToOADate(), DateTimeStyleIndex),
        DateTime dateTime => TextCell(dateTime.ToString("O", CultureInfo.InvariantCulture)),
        DateTimeOffset dateTimeOffset => TextCell(dateTimeOffset.ToString("O", CultureInfo.InvariantCulture)),
        byte or sbyte or short or ushort or int or uint or long or ulong or decimal =>
            NumberCell(Convert.ToString(value, CultureInfo.InvariantCulture)!),
        float number when float.IsFinite(number) => NumberCell(number),
        double number when double.IsFinite(number) => NumberCell(number),
        bool boolean => new Cell
        {
            DataType = CellValues.Boolean,
            CellValue = new CellValue(boolean ? "1" : "0")
        },
        IFormattable formattable => TextCell(formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty),
        _ => TextCell(value.ToString() ?? string.Empty)
    };

    private static Cell NumberCell(object value, uint? styleIndex = null) => new()
    {
        DataType = CellValues.Number,
        CellValue = new CellValue(Convert.ToString(value, CultureInfo.InvariantCulture)),
        StyleIndex = styleIndex
    };

    private static Cell TextCell(string text) => new()
    {
        DataType = CellValues.InlineString,
        InlineString = new InlineString(new Text(text) { Space = SpaceProcessingModeValues.Preserve })
    };

    private static void AddStyles(WorkbookPart workbookPart)
    {
        var stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
        stylesPart.Stylesheet = new Stylesheet(
            new NumberingFormats(
                new NumberingFormat { NumberFormatId = DateTimeNumberFormatId, FormatCode = "yyyy-mm-dd hh:mm:ss" })
            { Count = 1U },
            new Fonts(new Font()) { Count = 1U },
            new Fills(
                new Fill(new PatternFill { PatternType = PatternValues.None }),
                new Fill(new PatternFill { PatternType = PatternValues.Gray125 }))
            { Count = 2U },
            new Borders(new Border()) { Count = 1U },
            new CellStyleFormats(new CellFormat()) { Count = 1U },
            new CellFormats(
                new CellFormat(),
                new CellFormat { NumberFormatId = DateTimeNumberFormatId, ApplyNumberFormat = true })
            { Count = 2U },
            new CellStyles(new CellStyle { Name = "Normal", FormatId = 0U, BuiltinId = 0U }) { Count = 1U });
        stylesPart.Stylesheet.Save();
    }

    internal static string SheetName(string? name)
    {
        var candidate = string.IsNullOrWhiteSpace(name) ? "Export" : name.Trim();
        candidate = new string(candidate
            .Select(character => character < ' ' || character is '\\' or '/' or '?' or '*' or '[' or ']' or ':'
                ? '_'
                : character)
            .ToArray())
            .Trim('\'');
        if (string.IsNullOrWhiteSpace(candidate))
        {
            candidate = "Export";
        }

        return candidate.Length > 31 ? candidate[..31] : candidate;
    }
}
