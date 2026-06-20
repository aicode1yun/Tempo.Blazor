using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Tempo.Blazor.Components.Charts;

public enum SparklineType
{
    Line,
    Bar,
    Area,
    Pie
}

public partial class TmSparkline : ComponentBase
{
    [Parameter] public double[] Data { get; set; } = [];
    [Parameter] public SparklineType Type { get; set; } = SparklineType.Line;
    [Parameter] public string? Height { get; set; } = "40px";
    [Parameter] public string? Width { get; set; } = "100%";
    [Parameter] public string? Class { get; set; }

    private const int SvgW = 200;
    private const int SvgH = 60;
    private const int Pad = 4;

    private string CssClass => $"tm-sparkline{(string.IsNullOrEmpty(Class) ? "" : " " + Class)}";
    private string WrapperStyle => $"width:{Width};height:{Height};";

    private double MaxVal => Data.Length > 0 ? Data.Max() : 0;
    private double MinVal => Data.Length > 0 ? Data.Min() : 0;
    private double Range => MaxVal - MinVal;

    private static string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    private double ScaleX(int index)
    {
        if (Data.Length <= 1) return SvgW / 2.0;
        return Pad + (index * (SvgW - 2 * Pad) / (double)(Data.Length - 1));
    }

    private double ScaleY(double value)
    {
        if (Range <= 0) return SvgH - Pad;
        return Pad + (SvgH - 2 * Pad) - ((value - MinVal) / Range) * (SvgH - 2 * Pad);
    }

    private static readonly string[] Palette =
    [
        "#3b82f6", "#ef4444", "#10b981", "#f59e0b", "#8b5cf6",
        "#ec4899", "#06b6d4", "#84cc16", "#f97316", "#6366f1"
    ];

    private RenderFragment RenderLine => builder =>
    {
        var pts = string.Join(" ", Data.Select((d, i) => $"{F(ScaleX(i))},{F(ScaleY(d))}"));
        builder.OpenElement(0, "polyline");
        builder.AddAttribute(1, "points", pts);
        builder.AddAttribute(2, "class", "tm-sparkline__line");
        builder.AddAttribute(3, "fill", "none");
        builder.AddAttribute(4, "stroke", "#3b82f6");
        builder.AddAttribute(5, "stroke-width", "2");
        builder.AddAttribute(6, "stroke-linecap", "round");
        builder.AddAttribute(7, "stroke-linejoin", "round");
        builder.CloseElement();

        for (int i = 0; i < Data.Length; i++)
        {
            builder.OpenElement(0, "circle");
            builder.AddAttribute(1, "cx", F(ScaleX(i)));
            builder.AddAttribute(2, "cy", F(ScaleY(Data[i])));
            builder.AddAttribute(3, "r", "2");
            builder.AddAttribute(4, "fill", "#3b82f6");
            builder.OpenElement(5, "title");
            builder.AddContent(6, F(Data[i]));
            builder.CloseElement();
            builder.CloseElement();
        }
    };

    private RenderFragment RenderBars => builder =>
    {
        var bw = (SvgW - 2 * Pad) / (double)Math.Max(Data.Length, 1) * 0.8;
        var step = (SvgW - 2 * Pad) / (double)Math.Max(Data.Length, 1);
        for (int i = 0; i < Data.Length; i++)
        {
            var h = ((Data[i] - MinVal) / Math.Max(Range, 1)) * (SvgH - 2 * Pad);
            var x = Pad + i * step + (step - bw) / 2;
            var y = SvgH - Pad - h;
            builder.OpenElement(0, "rect");
            builder.AddAttribute(1, "x", F(x));
            builder.AddAttribute(2, "y", F(y));
            builder.AddAttribute(3, "width", F(bw));
            builder.AddAttribute(4, "height", F(Math.Max(1, h)));
            builder.AddAttribute(5, "class", "tm-sparkline__bar");
            builder.AddAttribute(6, "fill", "#3b82f6");
            builder.AddAttribute(7, "rx", "1");
            builder.OpenElement(8, "title");
            builder.AddContent(9, F(Data[i]));
            builder.CloseElement();
            builder.CloseElement();
        }
    };

    private RenderFragment RenderArea => builder =>
    {
        var pts = string.Join(" ", Data.Select((d, i) => $"{F(ScaleX(i))},{F(ScaleY(d))}"));
        var path = $"M {F(ScaleX(0))},{F(SvgH - Pad)} L {pts} L {F(ScaleX(Data.Length - 1))},{F(SvgH - Pad)} Z";

        builder.OpenElement(0, "path");
        builder.AddAttribute(1, "d", path);
        builder.AddAttribute(2, "class", "tm-sparkline__area");
        builder.AddAttribute(3, "fill", "#3b82f6");
        builder.AddAttribute(4, "opacity", "0.25");
        builder.CloseElement();

        builder.OpenElement(0, "polyline");
        builder.AddAttribute(1, "points", pts);
        builder.AddAttribute(2, "class", "tm-sparkline__line");
        builder.AddAttribute(3, "fill", "none");
        builder.AddAttribute(4, "stroke", "#3b82f6");
        builder.AddAttribute(5, "stroke-width", "2");
        builder.CloseElement();
    };

    private RenderFragment RenderPie => builder =>
    {
        var total = Data.Sum();
        if (total <= 0) return;

        double cx = SvgW / 2.0;
        double cy = SvgH / 2.0;
        double r = Math.Min(SvgW, SvgH) / 2.0 - Pad;
        double startAngle = -Math.PI / 2;

        for (int i = 0; i < Data.Length; i++)
        {
            var angle = (Data[i] / total) * 2 * Math.PI;
            var endAngle = startAngle + angle;

            var x1 = cx + r * Math.Cos(startAngle);
            var y1 = cy + r * Math.Sin(startAngle);
            var x2 = cx + r * Math.Cos(endAngle);
            var y2 = cy + r * Math.Sin(endAngle);
            var largeArc = angle > Math.PI ? 1 : 0;

            var d = $"M {F(cx)},{F(cy)} L {F(x1)},{F(y1)} A {F(r)},{F(r)} 0 {largeArc} 1 {F(x2)},{F(y2)} Z";

            builder.OpenElement(0, "path");
            builder.AddAttribute(1, "d", d);
            builder.AddAttribute(2, "class", "tm-sparkline__slice");
            builder.AddAttribute(3, "fill", Palette[i % Palette.Length]);
            builder.AddAttribute(4, "stroke", "#fff");
            builder.AddAttribute(5, "stroke-width", "1");
            builder.OpenElement(6, "title");
            builder.AddContent(7, F(Data[i]));
            builder.CloseElement();
            builder.CloseElement();

            startAngle = endAngle;
        }
    };
}
