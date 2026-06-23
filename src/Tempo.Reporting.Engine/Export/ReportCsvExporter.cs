using System.Globalization;
using System.Text;

namespace Tempo.Reporting.Engine.Export;

/// <summary>Writes processed tabular report output as CSV.</summary>
public static class ReportCsvExporter
{
    /// <summary>Exports the first tabular sheet as CSV bytes.</summary>
    public static byte[] Export(
        ReportTabularExportDocument document,
        ReportCsvExportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        options ??= new ReportCsvExportOptions();
        var culture = options.Culture ?? CultureInfo.InvariantCulture;
        var builder = new StringBuilder();
        var sheet = document.Sheets.FirstOrDefault();
        if (sheet is not null)
        {
            foreach (var row in sheet.Rows)
            {
                for (var index = 0; index < row.Cells.Count; index++)
                {
                    if (index > 0)
                    {
                        builder.Append(options.Delimiter);
                    }

                    builder.Append(Escape(FormatCell(row.Cells[index], culture), options.Delimiter));
                }

                builder.Append("\r\n");
            }
        }

        var payload = Encoding.UTF8.GetBytes(builder.ToString());
        if (!options.IncludeBom)
        {
            return payload;
        }

        var preamble = Encoding.UTF8.GetPreamble();
        var bytes = new byte[preamble.Length + payload.Length];
        Buffer.BlockCopy(preamble, 0, bytes, 0, preamble.Length);
        Buffer.BlockCopy(payload, 0, bytes, preamble.Length, payload.Length);
        return bytes;
    }

    private static string FormatCell(ReportTabularExportCell cell, CultureInfo culture)
    {
        if (cell.Value is null)
        {
            return string.Empty;
        }

        return cell.Kind switch
        {
            ReportTabularExportCellKind.Date when cell.Value is DateTimeOffset dateTimeOffset =>
                dateTimeOffset.Date.ToString("d", culture),
            ReportTabularExportCellKind.Date when cell.Value is DateTime dateTime =>
                dateTime.ToString("d", culture),
            ReportTabularExportCellKind.Date when cell.Value is DateOnly dateOnly =>
                dateOnly.ToString("d", culture),
            ReportTabularExportCellKind.Number when cell.Value is IFormattable formattable =>
                formattable.ToString(null, culture),
            ReportTabularExportCellKind.Boolean when cell.Value is bool boolean =>
                boolean.ToString(culture),
            _ when cell.Value is IFormattable formattable =>
                formattable.ToString(null, culture),
            _ => Convert.ToString(cell.Value, culture) ?? string.Empty,
        };
    }

    private static string Escape(string value, char delimiter)
    {
        if (value.IndexOfAny([delimiter, '"', '\r', '\n']) < 0)
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
