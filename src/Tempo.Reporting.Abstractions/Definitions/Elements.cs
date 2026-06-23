#pragma warning disable MA0016, MA0048

using System.Text.Json.Serialization;

namespace Tempo.Reporting.Abstractions.Definitions;

/// <summary>Base type for absolutely positioned report elements.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ReportTextBoxElement), "textBox")]
[JsonDerivedType(typeof(ReportImageElement), "image")]
[JsonDerivedType(typeof(ReportShapeElement), "shape")]
[JsonDerivedType(typeof(ReportLineElement), "line")]
[JsonDerivedType(typeof(ReportTableElement), "table")]
[JsonDerivedType(typeof(ReportChartElement), "chart")]
[JsonDerivedType(typeof(ReportSubReportElement), "subReport")]
public abstract record ReportElement
{
    /// <summary>Unique element identifier within the report definition.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Left coordinate in page units.</summary>
    public double X { get; init; }

    /// <summary>Top coordinate in page units.</summary>
    public double Y { get; init; }

    /// <summary>Element width in page units.</summary>
    public double Width { get; init; }

    /// <summary>Element height in page units.</summary>
    public double Height { get; init; }

    /// <summary>Optional named style reference.</summary>
    public string? StyleId { get; init; }

    /// <summary>Optional visibility expression evaluated during processing.</summary>
    public string? VisibleExpression { get; init; }
}

/// <summary>Text box element with static text or expression content.</summary>
public sealed record ReportTextBoxElement : ReportElement
{
    /// <summary>Static text content.</summary>
    public string? Text { get; init; }

    /// <summary>Expression content.</summary>
    public string? Expression { get; init; }

    /// <summary>Inline text style.</summary>
    public ReportTextStyle TextStyle { get; init; } = new();

    /// <summary>Horizontal text alignment.</summary>
    public ReportHorizontalAlignment HorizontalAlignment { get; init; } = ReportHorizontalAlignment.Left;

    /// <summary>Vertical text alignment.</summary>
    public ReportVerticalAlignment VerticalAlignment { get; init; } = ReportVerticalAlignment.Top;

    /// <summary>Text padding.</summary>
    public ReportThickness? Padding { get; init; }

    /// <summary>Text box border.</summary>
    public ReportBorder? Border { get; init; }

    /// <summary>Allows the text box to grow vertically during layout.</summary>
    public bool CanGrow { get; init; }
}

/// <summary>Image source kind.</summary>
public enum ReportImageSourceKind
{
    /// <summary>Image is loaded from a URL controlled by the host.</summary>
    Url,

    /// <summary>Image is embedded in the report definition.</summary>
    Embedded,

    /// <summary>Image source is produced by an expression.</summary>
    Expression,
}

/// <summary>Image sizing behavior.</summary>
public enum ReportImageSizingMode
{
    /// <summary>Stretch image to the element rectangle.</summary>
    Stretch,

    /// <summary>Contain image while preserving aspect ratio.</summary>
    Contain,

    /// <summary>Cover the element rectangle while preserving aspect ratio.</summary>
    Cover,

    /// <summary>Render image at its intrinsic size.</summary>
    ActualSize,
}

/// <summary>Image element.</summary>
public sealed record ReportImageElement : ReportElement
{
    /// <summary>Image source kind.</summary>
    public ReportImageSourceKind SourceKind { get; init; } = ReportImageSourceKind.Url;

    /// <summary>Image source value.</summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>Optional MIME type for embedded images.</summary>
    public string? ContentType { get; init; }

    /// <summary>Image sizing behavior.</summary>
    public ReportImageSizingMode Sizing { get; init; } = ReportImageSizingMode.Contain;
}

/// <summary>Shape kind.</summary>
public enum ReportShapeKind
{
    /// <summary>Rectangle shape.</summary>
    Rectangle,

    /// <summary>Rounded rectangle shape.</summary>
    RoundedRectangle,

    /// <summary>Ellipse shape.</summary>
    Ellipse,
}

/// <summary>Shape element.</summary>
public sealed record ReportShapeElement : ReportElement
{
    /// <summary>Shape kind.</summary>
    public ReportShapeKind Shape { get; init; } = ReportShapeKind.Rectangle;

    /// <summary>Optional fill color.</summary>
    public string? FillColor { get; init; }

    /// <summary>Optional border.</summary>
    public ReportBorder? Border { get; init; }
}

/// <summary>Line element.</summary>
public sealed record ReportLineElement : ReportElement
{
    /// <summary>Line stroke.</summary>
    public ReportBorderLine Stroke { get; init; } = new();
}

/// <summary>Table element structure. Detailed tablix behavior is implemented in a later phase.</summary>
public sealed record ReportTableElement : ReportElement
{
    /// <summary>Data set consumed by the table.</summary>
    public string? DataSetName { get; init; }

    /// <summary>Table columns.</summary>
    public List<ReportTableColumn> Columns { get; init; } = [];

    /// <summary>Whether the header row repeats on continued pages.</summary>
    public bool RepeatHeaderOnNewPage { get; init; }

    /// <summary>Border conflict handling used when adjacent cells share an edge.</summary>
    public ReportTableBorderModel BorderModel { get; init; } = ReportTableBorderModel.Collapse;

    /// <summary>Optional header row.</summary>
    public ReportTableRow? Header { get; init; }

    /// <summary>Optional grouped row definitions.</summary>
    public List<ReportTableGroupDefinition> Groups { get; init; } = [];

    /// <summary>Detail row template.</summary>
    public ReportTableRow Detail { get; init; } = new();

    /// <summary>Optional footer row.</summary>
    public ReportTableRow? Footer { get; init; }

    /// <summary>Optional background applied to every odd visible detail row.</summary>
    public string? ZebraStripeColor { get; init; }
}

/// <summary>Table column width behavior.</summary>
public enum ReportTableColumnWidthMode
{
    /// <summary>Column width is an absolute page-unit value.</summary>
    Fixed,

    /// <summary>Column width is a proportional weight that consumes remaining table width.</summary>
    Proportional,
}

/// <summary>Border conflict behavior for adjacent table cells.</summary>
public enum ReportTableBorderModel
{
    /// <summary>Adjacent borders collapse to a single stroke.</summary>
    Collapse,

    /// <summary>Each cell paints its own border independently.</summary>
    Separate,
}

/// <summary>Table column definition.</summary>
public sealed record ReportTableColumn
{
    /// <summary>Creates an empty table column.</summary>
    public ReportTableColumn()
    {
    }

    /// <summary>Creates a table column.</summary>
    public ReportTableColumn(string header, double width)
    {
        Header = header;
        Width = width;
    }

    /// <summary>Header text.</summary>
    public string Header { get; init; } = string.Empty;

    /// <summary>Column width in page units.</summary>
    public double Width { get; init; }

    /// <summary>Column width behavior. Defaults to fixed width.</summary>
    public ReportTableColumnWidthMode WidthMode { get; init; } = ReportTableColumnWidthMode.Fixed;
}

/// <summary>Table row template.</summary>
public sealed record ReportTableRow
{
    /// <summary>Nominal row height in page units.</summary>
    public double Height { get; init; } = 20;

    /// <summary>Whether the row should stay on one page.</summary>
    public bool KeepTogether { get; init; } = true;

    /// <summary>Optional visibility expression evaluated against the current row.</summary>
    public string? VisibleExpression { get; init; }

    /// <summary>Optional row background color.</summary>
    public string? BackgroundColor { get; init; }

    /// <summary>Optional row background expression. A string result is treated as a color; a true boolean uses the table zebra color.</summary>
    public string? BackgroundExpression { get; init; }

    /// <summary>Cells in the row.</summary>
    public List<ReportTableCell> Cells { get; init; } = [];
}

/// <summary>Table cell template.</summary>
public sealed record ReportTableCell
{
    /// <summary>Cell text or expression.</summary>
    public string? Text { get; init; }

    /// <summary>Expression that produces the cell text.</summary>
    public string? Expression { get; init; }

    /// <summary>Optional spreadsheet number or date format used by tabular exports.</summary>
    public string? NumberFormat { get; init; }

    /// <summary>Optional cell style id.</summary>
    public string? StyleId { get; init; }

    /// <summary>Inline text style for the cell.</summary>
    public ReportTextStyle TextStyle { get; init; } = new();

    /// <summary>Cell padding.</summary>
    public ReportThickness? Padding { get; init; }

    /// <summary>Cell border.</summary>
    public ReportBorder? Border { get; init; }

    /// <summary>Cell background color.</summary>
    public string? BackgroundColor { get; init; }

    /// <summary>Horizontal text alignment.</summary>
    public ReportHorizontalAlignment HorizontalAlignment { get; init; } = ReportHorizontalAlignment.Left;

    /// <summary>Whether text can grow the row height.</summary>
    public bool CanGrow { get; init; } = true;

    /// <summary>Optional element content hosted by this cell. Text boxes are supported in the F7 layout core.</summary>
    public List<ReportElement> Elements { get; init; } = [];
}

/// <summary>Table grouping definition.</summary>
public sealed record ReportTableGroupDefinition
{
    /// <summary>Group name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Expression that produces the group key.</summary>
    public string Expression { get; init; } = string.Empty;

    /// <summary>Optional group header row.</summary>
    public ReportTableRow? Header { get; init; }

    /// <summary>Optional group footer row.</summary>
    public ReportTableRow? Footer { get; init; }

    /// <summary>Whether a group header should stay with the first child row.</summary>
    public bool KeepWithFirstDetail { get; init; } = true;
}

/// <summary>Chart type.</summary>
public enum ReportChartType
{
    /// <summary>Column chart.</summary>
    Column,

    /// <summary>Horizontal bar chart.</summary>
    Bar,

    /// <summary>Line chart.</summary>
    Line,

    /// <summary>Area chart.</summary>
    Area,

    /// <summary>Pie chart.</summary>
    Pie,

    /// <summary>Donut chart.</summary>
    Donut,
}

/// <summary>Chart element structure rendered by the reporting engine.</summary>
public sealed record ReportChartElement : ReportElement
{
    /// <summary>Chart type.</summary>
    public ReportChartType ChartType { get; init; } = ReportChartType.Column;

    /// <summary>Data set consumed by the chart.</summary>
    public string? DataSetName { get; init; }

    /// <summary>Chart series definitions.</summary>
    public List<ReportChartSeries> Series { get; init; } = [];

    /// <summary>Optional chart title rendered above the plot area.</summary>
    public string? Title { get; init; }

    /// <summary>Category axis title rendered below Cartesian charts.</summary>
    public string? CategoryAxisTitle { get; init; }

    /// <summary>Value axis title rendered near the vertical value axis.</summary>
    public string? ValueAxisTitle { get; init; }

    /// <summary>Whether a legend is rendered. Defaults to true.</summary>
    public bool ShowLegend { get; init; } = true;

    /// <summary>Whether category labels are rendered for Cartesian charts. Defaults to true.</summary>
    public bool ShowCategoryAxis { get; init; } = true;

    /// <summary>Whether value labels and grid are rendered for Cartesian charts. Defaults to true.</summary>
    public bool ShowValueAxis { get; init; } = true;

    /// <summary>Chart color palette. Series colors override palette entries.</summary>
    public List<string> ColorPalette { get; init; } = [];
}

/// <summary>Chart series definition.</summary>
public sealed record ReportChartSeries
{
    /// <summary>Series name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Category expression.</summary>
    public string CategoryExpression { get; init; } = string.Empty;

    /// <summary>Value expression.</summary>
    public string ValueExpression { get; init; } = string.Empty;

    /// <summary>Optional series color. Falls back to the chart palette.</summary>
    public string? Color { get; init; }
}

/// <summary>Sub-report element.</summary>
public sealed record ReportSubReportElement : ReportElement
{
    /// <summary>Referenced report identifier.</summary>
    public string ReportId { get; init; } = string.Empty;

    /// <summary>Parameter mappings passed to the sub-report.</summary>
    public List<ReportSubReportParameterMapping> ParameterMappings { get; init; } = [];
}

/// <summary>Sub-report parameter mapping.</summary>
public sealed record ReportSubReportParameterMapping
{
    /// <summary>Creates an empty mapping.</summary>
    public ReportSubReportParameterMapping()
    {
    }

    /// <summary>Creates a parameter mapping.</summary>
    public ReportSubReportParameterMapping(string parameterName, string expression)
    {
        ParameterName = parameterName;
        Expression = expression;
    }

    /// <summary>Target parameter name.</summary>
    public string ParameterName { get; init; } = string.Empty;

    /// <summary>Source expression.</summary>
    public string Expression { get; init; } = string.Empty;
}

#pragma warning restore MA0016, MA0048
