using Microsoft.AspNetCore.Components;
using System.Globalization;

namespace Tempo.Blazor.Components.Wireframe;

/// <summary>SVG-based ruler that shares the canvas viewBox for accurate pan/zoom.</summary>
public partial class TmWireframeRuler : ComponentBase
{
    /// <summary>"Horizontal" or "Vertical".</summary>
    [Parameter] public string Orientation { get; set; } = "Horizontal";

    /// <summary>Pixel size of the ruler (height for H, width for V).</summary>
    [Parameter] public int Size { get; set; } = 20;

    /// <summary>Canvas viewBox X.</summary>
    [Parameter] public double ViewBoxX { get; set; }

    /// <summary>Canvas viewBox Y.</summary>
    [Parameter] public double ViewBoxY { get; set; }

    /// <summary>Canvas viewBox W.</summary>
    [Parameter] public double ViewBoxW { get; set; } = 1200;

    /// <summary>Canvas viewBox H.</summary>
    [Parameter] public double ViewBoxH { get; set; } = 800;

    /// <summary>Current zoom scale.</summary>
    [Parameter] public double Scale { get; set; } = 1.0;

    /// <summary>Cursor position in SVG space (null = hidden).</summary>
    [Parameter] public double? IndicatorPos { get; set; }

    [Parameter] public string? Class { get; set; }

    private bool _isHorizontal => Orientation.Equals("Horizontal", StringComparison.OrdinalIgnoreCase);

    private string _viewBox => _isHorizontal
        ? $"{F(ViewBoxX)} 0 {F(ViewBoxW)} {Size}"
        : $"0 {F(ViewBoxY)} {Size} {F(ViewBoxH)}";

    private string _bgX => _isHorizontal ? F(ViewBoxX) : "0";
    private string _bgY => _isHorizontal ? "0" : F(ViewBoxY);
    private string _bgW => _isHorizontal ? F(ViewBoxW) : Size.ToString(CultureInfo.InvariantCulture);
    private string _bgH => _isHorizontal ? Size.ToString(CultureInfo.InvariantCulture) : F(ViewBoxH);

    private List<Tick> _ticks = [];

    protected override void OnParametersSet()
    {
        _ticks = BuildTicks();
    }

    private List<Tick> BuildTicks()
    {
        var ticks = new List<Tick>();
        var start = _isHorizontal ? ViewBoxX : ViewBoxY;
        var length = _isHorizontal ? ViewBoxW : ViewBoxH;
        var end = start + length;

        // Choose step size based on zoom scale
        var (majorStep, minorStep) = Scale switch
        {
            < 0.3 => (200, 50),
            < 0.6 => (100, 25),
            < 1.2 => (50, 10),
            < 2.5 => (25, 5),
            _ => (10, 2),
        };

        // Round start down to nearest major step
        var firstMajor = Math.Floor(start / majorStep) * majorStep;

        for (var pos = firstMajor; pos <= end; pos += minorStep)
        {
            if (pos < start) continue;

            var isMajor = Math.Abs(pos % majorStep) < 0.001 || Math.Abs((pos % majorStep) - majorStep) < 0.001;
            var isMid = !isMajor && Math.Abs(pos % (majorStep / 2)) < 0.001;
            var tickLen = isMajor ? Size : isMid ? Size * 0.6 : Size * 0.35;
            var showLabel = isMajor && pos >= start && pos <= end;

            ticks.Add(new Tick(pos, tickLen, showLabel, isMajor ? $"{(int)pos}" : ""));
        }

        return ticks;
    }

    private static string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    internal sealed record Tick(double Pos, double Len, bool ShowLabel, string Label);
}
