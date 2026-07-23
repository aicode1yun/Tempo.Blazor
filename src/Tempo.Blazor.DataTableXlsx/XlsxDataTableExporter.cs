using Tempo.Blazor.Models;

namespace Tempo.Blazor.Export;

/// <summary>
/// Backward-compatible name for <see cref="DataTableXlsxExporter"/>. New code should prefer the
/// canonical exporter or register it through <c>AddTempoBlazorDataTableXlsx()</c>.
/// </summary>
public sealed class XlsxDataTableExporter : IDataTableExporter, IDataTableXlsxExporter
{
    private readonly DataTableXlsxExporter _inner = new();

    /// <inheritdoc />
    public string Format => "xlsx";

    /// <inheritdoc />
    public string ContentType => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <inheritdoc />
    public string FileExtension => "xlsx";

    /// <inheritdoc />
    public byte[] Export(DataTableExportData data)
        => _inner.Export(data);
}
