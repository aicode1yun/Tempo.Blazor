using System.Globalization;
using System.Text;
using Tempo.Blazor.Models;

namespace Tempo.Blazor.Components.DataTable;

/// <summary>
/// Dependency-free CSV <see cref="IDataTableExporter"/>. RFC 4180 style quoting; UTF-8 with an
/// optional BOM so Excel opens non-ASCII correctly.
/// </summary>
public sealed class CsvDataTableExporter : IDataTableExporter
{
    private readonly string _delimiter;
    private readonly bool _writeBom;

    /// <param name="delimiter">Field delimiter. Default comma.</param>
    /// <param name="writeBom">Whether to prepend a UTF-8 BOM. Default true (Excel friendliness).</param>
    public CsvDataTableExporter(string delimiter = ",", bool writeBom = true)
    {
        _delimiter = string.IsNullOrEmpty(delimiter) ? "," : delimiter;
        _writeBom = writeBom;
    }

    /// <inheritdoc />
    public string Format => "csv";

    /// <inheritdoc />
    public string ContentType => "text/csv";

    /// <inheritdoc />
    public string FileExtension => "csv";

    /// <inheritdoc />
    public byte[] Export(DataTableExportData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(_delimiter, data.Headers.Select(Escape)));
        foreach (var row in data.Rows)
        {
            sb.AppendLine(string.Join(_delimiter, row.Select(Escape)));
        }

        var text = sb.ToString();
        return _writeBom
            ? [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(text)]
            : Encoding.UTF8.GetBytes(text);
    }

    private string Escape(string? value)
    {
        var cell = value ?? string.Empty;
        var needsQuoting = cell.Contains(_delimiter, StringComparison.Ordinal)
            || cell.Contains('"')
            || cell.Contains('\n')
            || cell.Contains('\r');

        if (!needsQuoting)
        {
            return cell;
        }

        return string.Create(CultureInfo.InvariantCulture, $"\"{cell.Replace("\"", "\"\"")}\"");
    }
}
