using System.Globalization;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram;

/// <summary>Horizontal or vertical ruler synchronized with the diagram canvas viewport.</summary>
public partial class TmDiagramRuler : ComponentBase
{
    private const double Dpi = 96.0;

    /// <summary>Either <c>horizontal</c> or <c>vertical</c>.</summary>
    [Parameter] public string Orientation { get; set; } = "horizontal";

    /// <summary>Viewport X in document coordinates.</summary>
    [Parameter] public double ViewportX { get; set; }

    /// <summary>Viewport Y in document coordinates.</summary>
    [Parameter] public double ViewportY { get; set; }

    /// <summary>Viewport width in document coordinates.</summary>
    [Parameter] public double ViewportW { get; set; }

    /// <summary>Viewport height in document coordinates.</summary>
    [Parameter] public double ViewportH { get; set; }

    /// <summary>Current zoom level (1.0 = 100%).</summary>
    [Parameter] public double Zoom { get; set; } = 1.0;

    /// <summary>Measurement unit.</summary>
    [Parameter] public MeasurementUnit Unit { get; set; } = MeasurementUnit.Px;

    /// <summary>Page scale factor (1.0 = 1:1).</summary>
    [Parameter] public double Scale { get; set; } = 1.0;

    /// <summary>Cursor position for crosshair.</summary>
    [Parameter] public double CursorX { get; set; }

    /// <summary>Cursor position for crosshair.</summary>
    [Parameter] public double CursorY { get; set; }

    private string SvgWidth => Orientation == "horizontal" ? "100%" : "24";
    private string SvgHeight => Orientation == "horizontal" ? "24" : "100%";

    private string ViewBox => Orientation == "horizontal"
        ? $"{F(ViewportX)} 0 {F(ViewportW)} 24"
        : $"0 {F(ViewportY)} 24 {F(ViewportH)}";

    private static double GetUnitFactor(MeasurementUnit unit)
        => unit switch
        {
            MeasurementUnit.Pt => 72.0 / Dpi,
            MeasurementUnit.In => 1.0 / Dpi,
            MeasurementUnit.Mm => 25.4 / Dpi,
            MeasurementUnit.M => 0.0254 / Dpi,
            _ => 1.0,
        };

    private static string UnitLabel(MeasurementUnit unit)
        => unit switch
        {
            MeasurementUnit.Px => "px",
            MeasurementUnit.Pt => "pt",
            MeasurementUnit.In => "in",
            MeasurementUnit.Mm => "mm",
            MeasurementUnit.M => "m",
            _ => "px",
        };

    private double PxToUnit(double px) => px * GetUnitFactor(Unit) / Scale;

    private double UnitToPx(double u) => u / GetUnitFactor(Unit) * Scale;

    private static double NiceStep(double roughStep)
    {
        var exponent = Math.Floor(Math.Log10(roughStep));
        var fraction = roughStep / Math.Pow(10, exponent);
        double niceFraction;
        if (fraction <= 1.0) niceFraction = 1.0;
        else if (fraction <= 2.0) niceFraction = 2.0;
        else if (fraction <= 5.0) niceFraction = 5.0;
        else niceFraction = 10.0;
        return niceFraction * Math.Pow(10, exponent);
    }

    private IEnumerable<Tick> Ticks
    {
        get
        {
            var isHorizontal = Orientation == "horizontal";
            var startPx = isHorizontal ? ViewportX : ViewportY;
            var endPx = isHorizontal ? ViewportX + ViewportW : ViewportY + ViewportH;

            var factor = GetUnitFactor(Unit);
            // Minimum 40 px between major ticks
            var minMajorPx = 40.0 / Zoom;
            var roughStepUnit = NiceStep(minMajorPx * factor / Scale);
            var stepPx = UnitToPx(roughStepUnit);

            var firstPx = Math.Floor(startPx / stepPx) * stepPx;
            var subCount = 4;
            var subStepPx = stepPx / subCount;

            var firstSubPx = Math.Floor(startPx / subStepPx) * subStepPx;

            for (var pos = firstSubPx; pos <= endPx + subStepPx / 2; pos += subStepPx)
            {
                if (pos < startPx - subStepPx / 2)
                    continue;

                var majorIndex = Math.Round((pos - firstPx) / stepPx, 6);
                var isMajor = Math.Abs(majorIndex - Math.Round(majorIndex)) < 0.001;
                var isHalf = !isMajor && Math.Abs(majorIndex - Math.Round(majorIndex + 0.5) + 0.5) < 0.001;
                var showLabel = isMajor;
                var unitValue = PxToUnit(pos);
                var label = FormatUnitValue(unitValue, roughStepUnit);

                var len = isMajor ? 14 : isHalf ? 10 : 6;

                if (isHorizontal)
                {
                    yield return new Tick
                    {
                        X1 = pos,
                        Y1 = 0,
                        X2 = pos,
                        Y2 = len,
                        TextX = pos + 3,
                        TextY = 16,
                        IsMajor = isMajor,
                        IsHalf = isHalf,
                        ShowLabel = showLabel,
                        Label = label,
                    };
                }
                else
                {
                    yield return new Tick
                    {
                        X1 = 0,
                        Y1 = pos,
                        X2 = len,
                        Y2 = pos,
                        TextX = 4,
                        TextY = pos + 12,
                        IsMajor = isMajor,
                        IsHalf = isHalf,
                        ShowLabel = showLabel,
                        Label = label,
                    };
                }
            }
        }
    }

    private static string FormatUnitValue(double value, double step)
    {
        var decimals = step >= 1.0 ? 0 : Math.Max(0, (int)Math.Ceiling(-Math.Log10(step)));
        var s = value.ToString($"F{decimals}", CultureInfo.InvariantCulture);
        if (s.Contains('.'))
            s = s.TrimEnd('0').TrimEnd('.');
        if (s == "-0") s = "0";
        return s;
    }

    private static string F(double value)
        => value.ToString(CultureInfo.InvariantCulture);

    private sealed class Tick
    {
        public double X1 { get; set; }
        public double Y1 { get; set; }
        public double X2 { get; set; }
        public double Y2 { get; set; }
        public double TextX { get; set; }
        public double TextY { get; set; }
        public bool IsMajor { get; set; }
        public bool IsHalf { get; set; }
        public bool ShowLabel { get; set; }
        public string Label { get; set; } = string.Empty;
    }
}
