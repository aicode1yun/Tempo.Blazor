using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Tempo.Blazor.Models;

namespace Tempo.Blazor.Export;

/// <summary>
/// XLSX <see cref="IDataTableExporter"/> backed by DocumentFormat.OpenXml. Writes the header row
/// followed by data rows into a single worksheet using inline strings.
/// </summary>
public sealed class XlsxDataTableExporter : IDataTableExporter
{
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

            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            var sheetData = new SheetData();
            worksheetPart.Worksheet = new Worksheet(sheetData);

            var sheets = workbookPart.Workbook.AppendChild(new Sheets());
            sheets.Append(new Sheet
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = 1U,
                Name = SheetName(data.Name)
            });

            var headerRow = new Row();
            foreach (var header in data.Headers)
            {
                headerRow.Append(TextCell(header));
            }

            sheetData.Append(headerRow);

            foreach (var row in data.Rows)
            {
                var dataRow = new Row();
                foreach (var cell in row)
                {
                    dataRow.Append(TextCell(cell ?? string.Empty));
                }

                sheetData.Append(dataRow);
            }

            workbookPart.Workbook.Save();
        }

        return stream.ToArray();
    }

    private static Cell TextCell(string text) => new()
    {
        DataType = CellValues.InlineString,
        InlineString = new InlineString(new Text(text))
    };

    private static string SheetName(string? name)
    {
        var candidate = string.IsNullOrWhiteSpace(name) ? "Export" : name.Trim();
        foreach (var invalid in new[] { '\\', '/', '?', '*', '[', ']', ':' })
        {
            candidate = candidate.Replace(invalid, '_');
        }

        return candidate.Length > 31 ? candidate[..31] : candidate;
    }
}
