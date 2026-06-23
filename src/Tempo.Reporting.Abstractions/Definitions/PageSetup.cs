#pragma warning disable MA0048

namespace Tempo.Reporting.Abstractions.Definitions;

/// <summary>Report measurement unit for physical layout values.</summary>
public enum ReportMeasurementUnit
{
    /// <summary>CSS/print point, 1/72 inch.</summary>
    Point,
}

/// <summary>Page orientation used during layout.</summary>
public enum ReportPageOrientation
{
    /// <summary>Portrait page orientation.</summary>
    Portrait,

    /// <summary>Landscape page orientation.</summary>
    Landscape,
}

/// <summary>Physical page size.</summary>
public sealed record ReportPageSize(double Width, double Height, ReportMeasurementUnit Unit = ReportMeasurementUnit.Point)
{
    /// <summary>A4 portrait page size in points.</summary>
    public static ReportPageSize A4 { get; } = new(595.28, 841.89);

    /// <summary>US Letter portrait page size in points.</summary>
    public static ReportPageSize Letter { get; } = new(612, 792);
}

/// <summary>Page setup used by the band layout engine.</summary>
public sealed record ReportPageSetup
{
    /// <summary>Physical page size before orientation is applied.</summary>
    public ReportPageSize PageSize { get; init; } = ReportPageSize.A4;

    /// <summary>Page orientation.</summary>
    public ReportPageOrientation Orientation { get; init; } = ReportPageOrientation.Portrait;

    /// <summary>Page margins.</summary>
    public ReportThickness Margins { get; init; } = new(36);
}

/// <summary>Four-sided thickness value.</summary>
public sealed record ReportThickness
{
    /// <summary>Creates an empty thickness.</summary>
    public ReportThickness()
    {
    }

    /// <summary>Creates a uniform thickness.</summary>
    public ReportThickness(double uniform)
        : this(uniform, uniform, uniform, uniform)
    {
    }

    /// <summary>Creates a thickness with individual side values.</summary>
    public ReportThickness(double left, double top, double right, double bottom)
    {
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }

    /// <summary>Left side.</summary>
    public double Left { get; init; }

    /// <summary>Top side.</summary>
    public double Top { get; init; }

    /// <summary>Right side.</summary>
    public double Right { get; init; }

    /// <summary>Bottom side.</summary>
    public double Bottom { get; init; }
}

#pragma warning restore MA0048
