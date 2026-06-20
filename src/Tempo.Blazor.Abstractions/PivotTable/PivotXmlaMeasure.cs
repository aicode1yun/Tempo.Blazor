namespace Tempo.Blazor.Abstractions.PivotTable;

/// <summary>
/// Describes an OLAP measure exposed by an XMLA data source.
/// </summary>
public sealed class PivotXmlaMeasure
{
    /// <summary>Unique identifier of the measure.</summary>
    public string UniqueName { get; set; } = string.Empty;

    /// <summary>Human-readable caption.</summary>
    public string Caption { get; set; } = string.Empty;

    /// <summary>Data type of the measure (e.g. Integer, Double, Currency).</summary>
    public string DataType { get; set; } = string.Empty;

    /// <summary>Default display format string (e.g. #,##0.00).</summary>
    public string? DefaultFormat { get; set; }
}
