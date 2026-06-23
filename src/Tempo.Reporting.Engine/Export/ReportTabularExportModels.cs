#pragma warning disable MA0048

using System.Globalization;

namespace Tempo.Reporting.Engine.Export;

/// <summary>Tabular cell value kind used by CSV and spreadsheet exports.</summary>
public enum ReportTabularExportCellKind
{
    /// <summary>Empty value.</summary>
    Empty,

    /// <summary>String value.</summary>
    String,

    /// <summary>Numeric value.</summary>
    Number,

    /// <summary>Date or date-time value.</summary>
    Date,

    /// <summary>Boolean value.</summary>
    Boolean,

    /// <summary>Opaque object value.</summary>
    Object,
}

/// <summary>Culture-aware CSV export options.</summary>
public sealed record ReportCsvExportOptions
{
    /// <summary>Culture used for scalar formatting.</summary>
    public CultureInfo Culture { get; init; } = CultureInfo.InvariantCulture;

    /// <summary>Column delimiter. Defaults to comma.</summary>
    public char Delimiter { get; init; } = ',';

    /// <summary>Whether the UTF-8 BOM is prepended.</summary>
    public bool IncludeBom { get; init; }
}

/// <summary>Spreadsheet export options.</summary>
public sealed record ReportXlsxExportOptions
{
    /// <summary>Culture used for worksheet value formatting fallbacks.</summary>
    public CultureInfo Culture { get; init; } = CultureInfo.InvariantCulture;
}

/// <summary>Structured tabular export document.</summary>
public sealed record ReportTabularExportDocument
{
    /// <summary>Creates a tabular export document.</summary>
    public ReportTabularExportDocument(IReadOnlyList<ReportTabularExportSheet> sheets)
    {
        Sheets = sheets.ToArray();
    }

    /// <summary>Sheets available for export. CSV uses the first sheet.</summary>
    public IReadOnlyList<ReportTabularExportSheet> Sheets { get; }
}

/// <summary>One exportable table or data set.</summary>
public sealed record ReportTabularExportSheet
{
    /// <summary>Creates an export sheet.</summary>
    public ReportTabularExportSheet(string name, IReadOnlyList<ReportTabularExportRow> rows)
    {
        Name = name;
        Rows = rows.ToArray();
    }

    /// <summary>Worksheet or CSV table name.</summary>
    public string Name { get; }

    /// <summary>Rows written in order.</summary>
    public IReadOnlyList<ReportTabularExportRow> Rows { get; }
}

/// <summary>One export row.</summary>
public sealed record ReportTabularExportRow
{
    /// <summary>Creates an export row.</summary>
    public ReportTabularExportRow(
        IReadOnlyList<ReportTabularExportCell> cells,
        bool isHeader = false,
        string? backgroundColor = null)
    {
        Cells = cells.ToArray();
        IsHeader = isHeader;
        BackgroundColor = backgroundColor;
    }

    /// <summary>Cells written in order.</summary>
    public IReadOnlyList<ReportTabularExportCell> Cells { get; }

    /// <summary>Whether the row represents a table header.</summary>
    public bool IsHeader { get; }

    /// <summary>Optional row background color.</summary>
    public string? BackgroundColor { get; }
}

/// <summary>One export cell with optional spreadsheet styling.</summary>
public sealed record ReportTabularExportCell
{
    /// <summary>Creates an export cell.</summary>
    public ReportTabularExportCell(
        object? value,
        ReportTabularExportCellKind kind,
        string? numberFormat = null,
        bool bold = false,
        string? backgroundColor = null)
    {
        Value = value;
        Kind = kind;
        NumberFormat = numberFormat;
        Bold = bold;
        BackgroundColor = backgroundColor;
    }

    /// <summary>Raw typed cell value.</summary>
    public object? Value { get; init; }

    /// <summary>Cell value kind.</summary>
    public ReportTabularExportCellKind Kind { get; init; }

    /// <summary>Optional spreadsheet number or date format.</summary>
    public string? NumberFormat { get; init; }

    /// <summary>Whether the cell uses bold text.</summary>
    public bool Bold { get; init; }

    /// <summary>Optional cell background color.</summary>
    public string? BackgroundColor { get; init; }
}

#pragma warning restore MA0048
