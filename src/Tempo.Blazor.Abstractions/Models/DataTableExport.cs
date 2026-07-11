namespace Tempo.Blazor.Models;

/// <summary>
/// Tabular snapshot of a data table for export: the visible column headers and the fully
/// materialized rows (all pages) for the current filter/sort, as display strings.
/// </summary>
public sealed class DataTableExportData
{
    /// <summary>Visible column headers, left to right.</summary>
    public IReadOnlyList<string> Headers { get; init; } = [];

    /// <summary>Rows, each a list of cell display strings aligned to <see cref="Headers"/>.</summary>
    public IReadOnlyList<IReadOnlyList<string?>> Rows { get; init; } = [];

    /// <summary>Optional sheet/table name used by exporters that support it (e.g. XLSX).</summary>
    public string? Name { get; init; }
}

/// <summary>
/// Serializes a <see cref="DataTableExportData"/> snapshot into a downloadable document
/// (for example CSV or XLSX). Implementations are format-specific and stateless.
/// </summary>
public interface IDataTableExporter
{
    /// <summary>Short format key, e.g. <c>csv</c> or <c>xlsx</c>.</summary>
    string Format { get; }

    /// <summary>MIME content type of the produced document.</summary>
    string ContentType { get; }

    /// <summary>File extension (without the dot), e.g. <c>csv</c> or <c>xlsx</c>.</summary>
    string FileExtension { get; }

    /// <summary>Serializes the export data into document bytes.</summary>
    /// <param name="data">Headers and rows to export.</param>
    byte[] Export(DataTableExportData data);
}
