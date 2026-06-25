using System.Globalization;
using ClosedXML.Excel;

namespace Tempo.Reporting.Engine.Export;

/// <summary>Writes processed tabular report output as an XLSX workbook.</summary>
public static class ReportXlsxExporter
{
    /// <summary>Exports all tabular sheets as a workbook.</summary>
    public static byte[] Export(
        ReportTabularExportDocument document,
        ReportXlsxExportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        options ??= new ReportXlsxExportOptions();
        using var workbook = new XLWorkbook();
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (document.Sheets.Count == 0)
        {
            workbook.AddWorksheet("Data");
        }

        foreach (var sheet in document.Sheets)
        {
            var worksheet = workbook.AddWorksheet(UniqueSheetName(sheet.Name, usedNames));
            WriteSheet(worksheet, sheet, options.Culture ?? CultureInfo.InvariantCulture);
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void WriteSheet(
        IXLWorksheet worksheet,
        ReportTabularExportSheet sheet,
        CultureInfo culture)
    {
        for (var rowIndex = 0; rowIndex < sheet.Rows.Count; rowIndex++)
        {
            var row = sheet.Rows[rowIndex];
            for (var cellIndex = 0; cellIndex < row.Cells.Count; cellIndex++)
            {
                var exportCell = row.Cells[cellIndex];
                var cell = worksheet.Cell(rowIndex + 1, cellIndex + 1);
                SetValue(cell, exportCell, culture);
                ApplyStyle(cell, exportCell, row);
            }
        }

        if (sheet.Rows.Count > 0)
        {
            worksheet.SheetView.FreezeRows(1);
            worksheet.Columns(1, sheet.Rows.Max(row => row.Cells.Count)).AdjustToContents();
        }
    }

    private static void SetValue(
        IXLCell cell,
        ReportTabularExportCell exportCell,
        CultureInfo culture)
    {
        if (exportCell.Value is null)
        {
            cell.Clear(XLClearOptions.Contents);
            return;
        }

        switch (exportCell.Kind)
        {
            case ReportTabularExportCellKind.Number:
                cell.SetValue(Convert.ToDouble(exportCell.Value, culture));
                break;
            case ReportTabularExportCellKind.Date when exportCell.Value is DateTimeOffset dateTimeOffset:
                cell.SetValue(dateTimeOffset.DateTime);
                break;
            case ReportTabularExportCellKind.Date when exportCell.Value is DateTime dateTime:
                cell.SetValue(dateTime);
                break;
            case ReportTabularExportCellKind.Date when exportCell.Value is DateOnly dateOnly:
                cell.SetValue(dateOnly.ToDateTime(TimeOnly.MinValue));
                break;
            case ReportTabularExportCellKind.Boolean:
                cell.SetValue(Convert.ToBoolean(exportCell.Value, culture));
                break;
            default:
                cell.SetValue(Convert.ToString(exportCell.Value, culture) ?? string.Empty);
                break;
        }
    }

    private static void ApplyStyle(
        IXLCell cell,
        ReportTabularExportCell exportCell,
        ReportTabularExportRow row)
    {
        if (row.IsHeader || exportCell.Bold)
        {
            cell.Style.Font.Bold = true;
        }

        var fill = exportCell.BackgroundColor ?? row.BackgroundColor;
        if (!string.IsNullOrWhiteSpace(fill))
        {
            TryApplyFill(cell, fill);
        }

        if (!string.IsNullOrWhiteSpace(exportCell.NumberFormat))
        {
            if (exportCell.Kind == ReportTabularExportCellKind.Date)
            {
                cell.Style.DateFormat.Format = exportCell.NumberFormat;
            }
            else
            {
                cell.Style.NumberFormat.Format = exportCell.NumberFormat;
            }
        }
    }

    private static void TryApplyFill(IXLCell cell, string color)
    {
        try
        {
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml(color);
        }
        catch (ArgumentException)
        {
            // Invalid definition colors are ignored so export never fails because of a cosmetic value.
        }
    }

    private static string UniqueSheetName(string preferred, HashSet<string> usedNames)
    {
        var baseName = SanitizeSheetName(preferred);
        var name = baseName;
        var index = 2;
        while (!usedNames.Add(name))
        {
            var suffix = $" {index}";
            name = baseName[..Math.Min(baseName.Length, 31 - suffix.Length)] + suffix;
            index++;
        }

        return name;
    }

    private static string SanitizeSheetName(string value)
    {
        var sanitized = new string((string.IsNullOrWhiteSpace(value) ? "Data" : value)
            .Select(character => ":\\/?*[]".Contains(character) ? '-' : character)
            .ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "Data" : sanitized[..Math.Min(31, sanitized.Length)];
    }
}
