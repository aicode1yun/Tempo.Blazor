using System.Text;
using Microsoft.AspNetCore.Components.Rendering;
using Tempo.Blazor.Components.Wireframe.Models;
using static Tempo.Blazor.Components.Wireframe.WireframeSvg;

namespace Tempo.Blazor.Components.Wireframe;

internal static class TempoNativeRendererDefinitions
{
    private static readonly BuiltInComponentSchemas Schemas = new();

    public static IEnumerable<WireframeComponentDef> GetDefinitions()
    {
        yield return DefFromSchema("TmChart", "bar-chart-2", RenderChart);
        yield return DefFromSchema("TmGauge", "activity", RenderGauge);
        yield return DefFromSchema("TmStockChart", "trending-up", RenderStockChart);
        yield return DefFromSchema("TmKanbanBoard", "columns", RenderKanbanBoard);
        yield return DefFromSchema("TmPivotTable", "table", RenderPivotTable);
        yield return DefFromSchema("TmGantt", "bar-chart-2", RenderGantt);
        yield return DefFromSchema("TmWorkflowDesignerCanvas", "git-branch", RenderWorkflowDesignerCanvas);
        yield return DefFromSchema("TmDiagramEditor", "git-branch", RenderDiagramEditor);
        yield return DefFromSchema("TmSpreadsheet", "grid", RenderSpreadsheet);
        yield return DefFromSchema("TmDocumentEditor", "file-text", RenderDocumentEditor);
        yield return DefFromSchema("TmNotionEditor", "book-open", RenderNotionEditor);
        yield return DefFromSchema("TmChat", "message-circle", RenderChat);
    }

    private static WireframeComponentDef DefFromSchema(
        string type,
        string? icon,
        Action<WireframeElement, RenderTreeBuilder> render)
    {
        var schema = Schemas.GetSchemas().FirstOrDefault(x => x.Type == type)
                     ?? throw new InvalidOperationException($"No schema found for '{type}'");

        return new WireframeComponentDef
        {
            Type = schema.Type,
            Category = schema.Category,
            DisplayName = schema.DisplayName,
            Icon = icon,
            DefaultWidth = schema.DefaultWidth,
            DefaultHeight = schema.DefaultHeight,
            Props = [.. schema.Props],
            IsBuiltIn = true,
            RenderSvg = render,
            SizePresets = schema.SizePresets
        };
    }

    private static void Svg(RenderTreeBuilder builder, string markup)
        => builder.AddMarkupContent(0, markup);

    private static void RenderChart(WireframeElement el, RenderTreeBuilder b)
    {
        var type = el.Props.GetString("type", "bar");
        var title = el.Props.GetString("title", "Chart Title");
        var dataPoints = el.Props.GetInt("dataPoints", 6);
        var showLegend = el.Props.GetBool("showLegend", false);
        var showGrid = el.Props.GetBool("showGrid", false);
        var horizontal = el.Props.GetBool("horizontal", false);
        var sb = new StringBuilder();
        sb.Append(Rect(0, 0, el.W, el.H, Fill, Border, 6));
        sb.Append(Text(title, el.W / 2, 16, 12, ColorText, "middle", "500"));
        var legendH = showLegend ? 24.0 : 0;
        var chartX = 32.0;
        var chartY = 28.0;
        var chartW = el.W - chartX - 8;
        var chartH = el.H - chartY - 24 - legendH;
        var fills = new[] { FillAccent, FillDark, "#fef9c3", "#dcfce7", "#fee2e2" };

        if (type == "pie" || type == "donut")
        {
            var cx = el.W / 2;
            var cy = chartY + chartH / 2;
            var r = Math.Min(chartW, chartH) / 2 - 8;
            var angles = new[] { 0.0, 72, 144, 216, 288, 360 };
            for (var i = 0; i < Math.Min(dataPoints, 5); i++)
            {
                var a1 = angles[i] * Math.PI / 180;
                var a2 = angles[i + 1] * Math.PI / 180;
                var x1 = cx + r * Math.Cos(a1);
                var y1 = cy + r * Math.Sin(a1);
                var x2 = cx + r * Math.Cos(a2);
                var y2 = cy + r * Math.Sin(a2);
                var large = angles[i + 1] - angles[i] > 180 ? 1 : 0;
                sb.Append($"<path d='M{F(cx)},{F(cy)} L{F(x1)},{F(y1)} A{F(r)},{F(r)} 0 {large} 1 {F(x2)},{F(y2)} Z' fill='{fills[i % fills.Length]}' stroke='white' stroke-width='1'></path>");
            }

            if (type == "donut")
                sb.Append($"<circle cx='{F(cx)}' cy='{F(cy)}' r='{F(r * 0.5)}' fill='white'></circle>");
        }
        else if (type == "line")
        {
            sb.Append(VLine(chartX, chartY, chartY + chartH));
            sb.Append(HLine(chartX, chartX + chartW, chartY + chartH));
            if (showGrid)
            {
                for (var g = 1; g <= 4; g++)
                    sb.Append(HLine(chartX, chartX + chartW, chartY + chartH * g / 5, FillDark));
            }

            var heights = new[] { 0.6, 0.8, 0.4, 0.9, 0.5, 0.7, 0.85 };
            var pts = new List<string>();
            for (var i = 0; i < dataPoints; i++)
            {
                var px = chartX + (i + 0.5) * chartW / dataPoints;
                var py = chartY + chartH - heights[i % heights.Length] * chartH;
                pts.Add($"{F(px)},{F(py)}");
            }

            sb.Append($"<polyline points='{string.Join(" ", pts)}' fill='none' stroke='{Accent}' stroke-width='2'></polyline>");
        }
        else
        {
            sb.Append(VLine(chartX, chartY, chartY + chartH));
            sb.Append(HLine(chartX, chartX + chartW, chartY + chartH));
            if (showGrid)
            {
                for (var g = 1; g <= 4; g++)
                {
                    sb.Append(horizontal
                        ? VLine(chartX + chartW * g / 5, chartY, chartY + chartH, FillDark)
                        : HLine(chartX, chartX + chartW, chartY + chartH * g / 5, FillDark));
                }
            }

            var heights = new[] { 0.6, 0.8, 0.4, 0.9, 0.5, 0.7 };
            if (horizontal)
            {
                var barH = chartH / dataPoints * 0.65;
                var gap = chartH / dataPoints;
                for (var i = 0; i < dataPoints; i++)
                {
                    var bw = heights[i % heights.Length] * chartW;
                    var by = chartY + i * gap + gap * 0.175;
                    sb.Append(Rect(chartX, by, bw, barH, FillAccent, "#93c5fd", 2));
                }
            }
            else
            {
                var barW = chartW / dataPoints * 0.65;
                var gap = chartW / dataPoints;
                for (var i = 0; i < dataPoints; i++)
                {
                    var bh = heights[i % heights.Length] * chartH;
                    var bx = chartX + i * gap + gap * 0.175;
                    sb.Append(Rect(bx, chartY + chartH - bh, barW, bh, FillAccent, "#93c5fd", 2));
                }
            }
        }

        if (showLegend)
        {
            var lx = el.W / 2 - dataPoints * 36 / 2;
            for (var i = 0; i < Math.Min(dataPoints, 5); i++)
            {
                sb.Append(Rect(lx + i * 36, el.H - 20, 10, 10, fills[i % fills.Length], Border, 2));
                sb.Append(Text($"L{i + 1}", lx + i * 36 + 14, el.H - 15, 8, ColorMuted));
            }
        }

        Svg(b, sb.ToString());
    }

    private static void RenderGauge(WireframeElement el, RenderTreeBuilder b)
    {
        var value = el.Props.GetDouble("value", 65.0);
        var min = el.Props.GetDouble("min", 0.0);
        var max = el.Props.GetDouble("max", 100.0);
        var label = el.Props.GetString("label", "");
        var sb = new StringBuilder();
        var cx = el.W / 2;
        var cy = el.H * 0.62;
        var r = Math.Min(el.W, el.H * 2) * 0.42;
        var ratio = max > min ? Math.Clamp((value - min) / (max - min), 0, 1) : 0;
        var startA = Math.PI;
        var endA = 2 * Math.PI;
        var trackX1 = cx + r * Math.Cos(startA);
        var trackY1 = cy + r * Math.Sin(startA);
        var trackX2 = cx + r * Math.Cos(endA);
        var trackY2 = cy + r * Math.Sin(endA);
        sb.Append($"<path d='M {F(trackX1)},{F(trackY1)} A {F(r)},{F(r)} 0 0 1 {F(trackX2)},{F(trackY2)}' fill='none' stroke='{FillDark}' stroke-width='8' stroke-linecap='round'></path>");
        var valueA = startA + ratio * Math.PI;
        var valX2 = cx + r * Math.Cos(valueA);
        var valY2 = cy + r * Math.Sin(valueA);
        var large = ratio > 0.5 ? 1 : 0;
        sb.Append($"<path d='M {F(trackX1)},{F(trackY1)} A {F(r)},{F(r)} 0 {large} 1 {F(valX2)},{F(valY2)}' fill='none' stroke='{Accent}' stroke-width='8' stroke-linecap='round'></path>");
        var needleX = cx + (r - 12) * Math.Cos(valueA);
        var needleY = cy + (r - 12) * Math.Sin(valueA);
        sb.Append($"<line x1='{F(cx)}' y1='{F(cy)}' x2='{F(needleX)}' y2='{F(needleY)}' stroke='{ColorText}' stroke-width='2' stroke-linecap='round'></line>");
        sb.Append($"<circle cx='{F(cx)}' cy='{F(cy)}' r='4' fill='{ColorText}'></circle>");
        sb.Append(Text(F(value), cx, cy + 14, 12, ColorText, "middle", "600"));
        if (!string.IsNullOrEmpty(label))
            sb.Append(Text(label, cx, cy + 26, 9, ColorMuted, "middle"));
        Svg(b, sb.ToString());
    }

    private static void RenderStockChart(WireframeElement el, RenderTreeBuilder b)
    {
        var title = el.Props.GetString("title", "ACME");
        var type = el.Props.GetString("type", "candle");
        var period = el.Props.GetString("period", "1M");
        var sb = new StringBuilder();
        sb.Append(Rect(0, 0, el.W, el.H, Fill, Border, 6));
        sb.Append(Text(title, 12, 16, 12, ColorText, "start", "600"));
        sb.Append(Text("+2.4%  ▲", el.W - 12, 16, 10, "#16a34a", "end", "500"));
        var periods = new[] { "1D", "1W", "1M", "3M", "1Y" };
        var btnW = 28.0;
        var btnsX = el.W - periods.Length * (btnW + 2) - 8;
        var hdrH = 30.0;
        sb.Append(Rect(0, hdrH, el.W, el.H - hdrH, Fill, "none", 0));
        for (var pi = 0; pi < periods.Length; pi++)
        {
            var bx = btnsX + pi * (btnW + 2);
            var isActive = periods[pi] == period;
            sb.Append(Rect(bx, hdrH + 4, btnW, 16, isActive ? FillAccent : FillDark, isActive ? "#93c5fd" : "none", 3));
            sb.Append(Text(periods[pi], bx + btnW / 2, hdrH + 12, 8, isActive ? Accent : ColorMuted, "middle"));
        }

        var chartY = hdrH + 24;
        var chartH = el.H - chartY - 20;
        var chartX = 8.0;
        var chartW = el.W - 16;
        sb.Append(VLine(chartX, chartY, chartY + chartH, Border));
        sb.Append(HLine(chartX, chartX + chartW, chartY + chartH, Border));
        for (var gi = 1; gi <= 3; gi++)
            sb.Append(HLine(chartX, chartX + chartW, chartY + chartH * gi / 4, FillDark));

        var candleCount = 16;
        var candleW = (chartW - 4) / candleCount;
        var rng = 7;
        for (var ci = 0; ci < candleCount; ci++)
        {
            rng = (rng * 1103515245 + 12345) & 0x7fffffff;
            var open = 0.2 + (rng & 0xff) / 512.0;
            rng = (rng * 1103515245 + 12345) & 0x7fffffff;
            var close = 0.2 + (rng & 0xff) / 512.0;
            var high = Math.Max(open, close) + 0.08;
            var low = Math.Min(open, close) - 0.08;
            var bx = chartX + 2 + ci * candleW;
            if (type == "candle")
            {
                var bullish = close > open;
                var cFill = bullish ? "#dcfce7" : "#fee2e2";
                var cStroke = bullish ? "#16a34a" : "#dc2626";
                var bodyY = chartY + (1 - Math.Max(open, close)) * chartH;
                var bodyH = Math.Max(Math.Abs(open - close) * chartH, 2);
                sb.Append($"<line x1='{F(bx + candleW / 2)}' y1='{F(chartY + (1 - high) * chartH)}' x2='{F(bx + candleW / 2)}' y2='{F(chartY + (1 - low) * chartH)}' stroke='{cStroke}' stroke-width='1'></line>");
                sb.Append($"<rect x='{F(bx + 1)}' y='{F(bodyY)}' width='{F(candleW - 2)}' height='{F(bodyH)}' fill='{cFill}' stroke='{cStroke}' stroke-width='0.5'></rect>");
            }
            else if (ci > 0)
            {
                rng = (rng * 1103515245 + 12345) & 0x7fffffff;
                var prevClose = 0.2 + (rng & 0xff) / 512.0;
                sb.Append($"<line x1='{F(bx - candleW)}' y1='{F(chartY + (1 - prevClose) * chartH)}' x2='{F(bx)}' y2='{F(chartY + (1 - close) * chartH)}' stroke='{Accent}' stroke-width='1.5'></line>");
            }
        }

        Svg(b, sb.ToString());
    }

    private static void RenderKanbanBoard(WireframeElement el, RenderTreeBuilder b)
    {
        var cols = el.Props.GetStringList("columns");
        if (cols.Length == 0)
            cols = ["To Do", "In Progress", "Done"];
        var colW = (el.W - (cols.Length - 1) * 8.0) / cols.Length;
        var sb = new StringBuilder();
        for (var i = 0; i < cols.Length; i++)
        {
            var x = i * (colW + 8);
            sb.Append(Rect(x, 0, colW, el.H, FillDark, Border, 6));
            sb.Append(Rect(x + 4, 4, colW - 8, 24, FillDark, "none", 0));
            sb.Append(Text(cols[i], x + colW / 2, 16, 11, ColorText, "middle", "500"));
            for (var c = 0; c < 2; c++)
            {
                var cy = 36 + c * 60.0;
                sb.Append(Rect(x + 4, cy, colW - 8, 52, Fill, Border, 4));
                sb.Append(HLine(x + 4, x + colW - 4, cy + 16));
                sb.Append(Text("Task", x + 12, cy + 8, 10, ColorMuted));
            }
        }

        Svg(b, sb.ToString());
    }

    private static void RenderWorkflowDesignerCanvas(WireframeElement el, RenderTreeBuilder b)
    {
        var title = el.Props.GetString("title", "Workflow");
        var sb = new StringBuilder();
        sb.Append(DashedRect(el.W, el.H, 6));
        sb.Append(Text(title, el.W / 2, 18, 12, ColorMuted, "middle", "500"));
        var nodes = new (double x, double y, string label, string type)[]
        {
            (60, el.H / 2 - 20, "Start", "initial"),
            (el.W / 2 - 60, el.H / 2 - 20, "Process", "intermediate"),
            (el.W - 200, el.H / 2 - 20, "End", "final")
        };
        foreach (var (nx, ny, label, nodeType) in nodes)
        {
            var fill = nodeType == "initial" ? FillAccent : nodeType == "final" ? "#dcfce7" : Fill;
            var rx = nodeType == "initial" ? 20.0 : 8.0;
            sb.Append(Rect(nx, ny, 120, 40, fill, Border, rx));
            sb.Append(Text(label, nx + 60, ny + 20, 11, ColorText, "middle"));
        }

        sb.Append(HLine(180, el.W / 2 - 60, el.H / 2));
        sb.Append(HLine(el.W / 2 + 60, el.W - 200, el.H / 2));
        Svg(b, sb.ToString());
    }

    private static void RenderChat(WireframeElement el, RenderTreeBuilder b)
    {
        var msgCount = el.Props.GetInt("messageCount", 4);
        var showInput = el.Props.GetBool("showInput", true);
        var sb = new StringBuilder();
        sb.Append(Rect(0, 0, el.W, el.H, Fill, Border, 6));
        sb.Append(Rect(0, 0, el.W, 44, FillDark, "none", 6));
        sb.Append(HLine(0, el.W, 44));
        sb.Append($"<circle cx='22' cy='22' r='12' fill='{FillAccent}' stroke='#93c5fd'></circle>");
        sb.Append(Text("A", 22, 22, 10, Accent, "middle", "500"));
        sb.Append(Text("Alice", 40, 18, 11, ColorText, "start", "500"));
        sb.Append(Text("Online", 40, 31, 9, "#22c55e"));
        var contentH = el.H - 44 - (showInput ? 48 : 8);
        var bubbleH = Math.Max(contentH / msgCount - 8, 20);
        for (var i = 0; i < msgCount; i++)
        {
            var by = 52 + i * (bubbleH + 8);
            var mine = i % 2 == 1;
            var bw = el.W * 0.65;
            var bx = mine ? el.W - bw - 8 : 8;
            var fill = mine ? FillAccent : FillDark;
            var stroke = mine ? "#93c5fd" : Border;
            sb.Append(Rect(bx, by, bw, bubbleH, fill, stroke, 8));
            if (!mine)
                sb.Append($"<circle cx='8' cy='{F(by + bubbleH / 2)}' r='0'></circle>");
        }

        if (showInput)
        {
            sb.Append(HLine(0, el.W, el.H - 48));
            sb.Append(Rect(8, el.H - 40, el.W - 52, 32, FillDark, Border, 6));
            sb.Append(Text("Message…", 16, el.H - 24, 10, ColorLight));
            sb.Append(Rect(el.W - 40, el.H - 40, 32, 32, FillAccent, "#93c5fd", 6));
            sb.Append(Icon("send", el.W - 24, el.H - 24, 14));
        }

        Svg(b, sb.ToString());
    }

    private static void RenderSpreadsheet(WireframeElement el, RenderTreeBuilder b)
    {
        var rows = el.Props.GetInt("rows", 8);
        var cols = el.Props.GetInt("columns", 6);
        var sheets = el.Props.GetInt("sheetCount", 2);
        var sb = new StringBuilder();
        sb.Append(Rect(0, 0, el.W, el.H, Fill, Border, 4));
        sb.Append(Rect(0, 0, el.W, 28, FillDark, "none", 4));
        sb.Append(HLine(0, el.W, 28));
        sb.Append(Rect(4, 4, 36, 20, Fill, Border, 2));
        sb.Append(Text("A1", 22, 14, 9, ColorText, "middle"));
        sb.Append(VLine(40, 0, 28, Border));
        sb.Append(Text("fx  =SUM(A1:A5)", 48, 14, 9, ColorMuted));
        var headerH = 20.0;
        var rowNumW = 28.0;
        var gridY = 28.0;
        var gridH = el.H - 28 - 24;
        var colW = (el.W - rowNumW) / cols;
        var rowH = (gridH - headerH) / rows;
        sb.Append(Rect(rowNumW, gridY, el.W - rowNumW, headerH, FillDark, "none", 0));
        sb.Append(HLine(0, el.W, gridY + headerH));
        for (var c = 0; c < cols; c++)
        {
            var cx = rowNumW + c * colW;
            sb.Append(VLine(cx, gridY, gridY + gridH));
            sb.Append(Text(((char)('A' + c)).ToString(), cx + colW / 2, gridY + headerH / 2, 9, ColorMuted, "middle"));
        }

        for (var r = 0; r < rows; r++)
        {
            var ry = gridY + headerH + r * rowH;
            sb.Append(HLine(0, el.W, ry + rowH));
            sb.Append(Text((r + 1).ToString(), rowNumW / 2, ry + rowH / 2, 9, ColorMuted, "middle"));
            if (r == 0)
            {
                for (var c = 0; c < cols; c++)
                {
                    var cx = rowNumW + c * colW;
                    sb.Append(Rect(cx + 1, ry + 1, colW - 1, rowH - 1, FillAccent, "none", 0));
                    sb.Append(Text(c == 0 ? "Name" : c == 1 ? "Value" : "—", cx + 4, ry + rowH / 2, 8, Accent));
                }
            }
        }

        sb.Append(VLine(rowNumW, gridY, gridY + gridH));
        sb.Append(HLine(0, el.W, gridY + gridH));
        var tabY = el.H - 24;
        sb.Append(Rect(0, tabY, el.W, 24, FillDark, "none", 0));
        sb.Append(HLine(0, el.W, tabY));
        for (var s = 0; s < sheets; s++)
        {
            sb.Append(Rect(4 + s * 62, tabY + 4, 56, 16, s == 0 ? Fill : FillDark, s == 0 ? Border : "none", 3));
            sb.Append(Text($"Sheet {s + 1}", 32 + s * 62, tabY + 12, 8, s == 0 ? ColorText : ColorMuted, "middle"));
        }

        sb.Append(Text("+", el.W - 16, tabY + 12, 10, ColorMuted, "middle"));
        Svg(b, sb.ToString());
    }

    private static void RenderGantt(WireframeElement el, RenderTreeBuilder b)
    {
        var taskCount = el.Props.GetInt("taskCount", 5);
        var period = el.Props.GetString("period", "week");
        var sb = new StringBuilder();
        sb.Append(Rect(0, 0, el.W, el.H, Fill, Border, 4));
        var listW = el.W * 0.3;
        var chartW = el.W - listW;
        var headerH = 28.0;
        var rowH = (el.H - headerH) / taskCount;
        sb.Append(Rect(0, 0, el.W, headerH, FillDark, "none", 4));
        sb.Append(HLine(0, el.W, headerH));
        sb.Append(Text("Task", 12, headerH / 2, 10, ColorMuted));
        var ticks = period == "week" ? 7 : period == "month" ? 4 : 12;
        for (var t = 0; t < ticks; t++)
        {
            var tx = listW + t * chartW / ticks;
            sb.Append(VLine(tx, 0, el.H, Border));
            sb.Append(Text($"W{t + 1}", tx + chartW / ticks / 2, headerH / 2, 8, ColorMuted, "middle"));
        }

        sb.Append(VLine(listW, 0, el.H, BorderStrong));
        var barConfigs = new[] { (0.0, 0.4), (0.1, 0.55), (0.3, 0.3), (0.5, 0.4), (0.6, 0.35) };
        for (var t = 0; t < taskCount; t++)
        {
            var ry = headerH + t * rowH;
            if (t > 0)
                sb.Append(HLine(0, el.W, ry));
            sb.Append(Text($"Task {t + 1}", 12, ry + rowH / 2, 10, ColorText));
            var (bStart, bLen) = barConfigs[t % barConfigs.Length];
            var bx = listW + bStart * chartW;
            var bw = bLen * chartW;
            var isDone = t < 2;
            sb.Append(Rect(bx, ry + rowH * 0.2, bw, rowH * 0.6, isDone ? "#dcfce7" : FillAccent, isDone ? "#86efac" : "#93c5fd", 3));
            if (isDone)
                sb.Append(Rect(bx, ry + rowH * 0.2, bw * 0.8, rowH * 0.6, "#22c55e", "none", 3));
        }

        Svg(b, sb.ToString());
    }

    private static void RenderPivotTable(WireframeElement el, RenderTreeBuilder b)
    {
        var rows = el.Props.GetInt("rows", 4);
        var cols = el.Props.GetInt("columns", 4);
        var sb = new StringBuilder();
        sb.Append(Rect(0, 0, el.W, el.H, Fill, Border, 4));
        var rowLabelW = el.W * 0.22;
        var colH = 24.0;
        var rowH = (el.H - colH - 28) / rows;
        var colW = (el.W - rowLabelW) / cols;
        sb.Append(Rect(0, 0, el.W, 28, FillDark, "none", 4));
        sb.Append(HLine(0, el.W, 28));
        sb.Append(Text("Pivot", 10, 14, 10, ColorText, "start", "500"));
        sb.Append(Rect(el.W - 120, 4, 54, 20, FillAccent, "#93c5fd", 3));
        sb.Append(Text("Rows ▾", el.W - 93, 14, 8, Accent, "middle"));
        sb.Append(Rect(el.W - 62, 4, 54, 20, FillDark, Border, 3));
        sb.Append(Text("Cols ▾", el.W - 35, 14, 8, ColorMuted, "middle"));
        sb.Append(Rect(rowLabelW, 28, el.W - rowLabelW, colH, FillDark, "none", 0));
        sb.Append(HLine(0, el.W, 28 + colH));
        for (var c = 0; c < cols; c++)
        {
            var cx = rowLabelW + c * colW;
            sb.Append(VLine(cx, 28, el.H));
            sb.Append(Text($"Col {c + 1}", cx + colW / 2, 28 + colH / 2, 8, ColorMuted, "middle", "500"));
        }

        for (var r = 0; r < rows; r++)
        {
            var ry = 28 + colH + r * rowH;
            sb.Append(HLine(0, el.W, ry + rowH));
            sb.Append(Rect(0, ry, rowLabelW, rowH, FillDark, "none", 0));
            sb.Append(Text($"Row {r + 1}", rowLabelW / 2, ry + rowH / 2, 8, ColorMuted, "middle"));
            for (var c = 0; c < cols; c++)
            {
                var cx = rowLabelW + c * colW;
                var val = (r + 1) * (c + 1) * 12 + r * 7;
                sb.Append(Text(val.ToString(), cx + colW / 2, ry + rowH / 2, 9, ColorText, "middle"));
            }
        }

        sb.Append(VLine(rowLabelW, 28, el.H, BorderStrong));
        Svg(b, sb.ToString());
    }

    private static void RenderDiagramEditor(WireframeElement el, RenderTreeBuilder b)
    {
        var title = el.Props.GetString("title", "Diagram");
        var nodeCount = el.Props.GetInt("nodeCount", 4);
        var sb = new StringBuilder();
        sb.Append(Rect(0, 0, el.W, el.H, Fill, Border, 4));
        var railW = el.W * 0.12;
        sb.Append(Rect(0, 0, railW, el.H, FillDark, "none", 4));
        sb.Append(VLine(railW, 0, el.H, Border));
        sb.Append(Text("⋮", railW / 2, 20, 14, ColorMuted, "middle"));
        for (var s = 0; s < 4; s++)
            sb.Append(Rect(4, 32 + s * 28, railW - 8, 20, Fill, Border, 3));
        sb.Append(Rect(railW, 0, el.W - railW, 32, FillDark, "none", 4));
        sb.Append(HLine(railW, el.W, 32));
        sb.Append(Text(title, railW + 8, 16, 11, ColorText, "start", "500"));
        var nodePositions = new (double x, double y)[]
        {
            (railW + 20, 50),
            ((railW + el.W) / 2 - 50, 48),
            (railW + 20, el.H - 70),
            ((railW + el.W) / 2 - 50, el.H - 70)
        };
        for (var n = 0; n < Math.Min(nodeCount, nodePositions.Length); n++)
        {
            var (nx, ny) = nodePositions[n];
            sb.Append(Rect(nx, ny, 80, 36, n == 0 ? FillAccent : Fill, n == 0 ? "#93c5fd" : Border, 6));
            sb.Append(Text($"Node {n + 1}", nx + 40, ny + 18, 9, n == 0 ? Accent : ColorText, "middle"));
        }

        if (nodeCount >= 2)
        {
            var (x1, y1) = (nodePositions[0].x + 80, nodePositions[0].y + 18);
            var (x2, y2) = (nodePositions[1].x, nodePositions[1].y + 18);
            sb.Append($"<line x1='{F(x1)}' y1='{F(y1)}' x2='{F(x2)}' y2='{F(y2)}' stroke='{Border}' stroke-width='1.5' marker-end='url(#arr)'></line>");
        }

        var panelW = el.W * 0.22;
        sb.Append(Rect(el.W - panelW, 32, panelW, el.H - 32, FillDark, "none", 0));
        sb.Append(VLine(el.W - panelW, 32, el.H, Border));
        sb.Append(Text("Properties", el.W - panelW / 2, 48, 9, ColorMuted, "middle", "500"));
        for (var p = 0; p < 4; p++)
            sb.Append(Rect(el.W - panelW + 4, 58 + p * 24, panelW - 8, 18, Fill, Border, 2));
        Svg(b, sb.ToString());
    }

    private static void RenderDocumentEditor(WireframeElement el, RenderTreeBuilder b)
    {
        var title = el.Props.GetString("title", "Document");
        var showRuler = el.Props.GetBool("showRuler", true);
        var sb = new StringBuilder();
        sb.Append(Rect(0, 0, el.W, el.H, FillDark, Border, 4));
        sb.Append(Rect(0, 0, el.W, 36, Fill, Border, 4));
        sb.Append(HLine(0, el.W, 36));
        foreach (var (tool, tx) in new[] { ("B", 10.0), ("I", 28.0), ("U", 46.0), ("|", 60.0), ("H1", 68.0), ("H2", 82.0), ("|", 96.0), ("≡", 104.0), ("⋮≡", 116.0) })
        {
            if (tool == "|")
                sb.Append(VLine(tx, 6, 30));
            else
                sb.Append(Text(tool, tx, 18, 10, ColorMuted, "middle", "500"));
        }

        var contentY = 36.0;
        if (showRuler)
        {
            sb.Append(Rect(0, 36, el.W, 16, FillDark, "none", 0));
            sb.Append(HLine(0, el.W, 52));
            for (var r = 0; r < 8; r++)
                sb.Append($"<line x1='{F(r * el.W / 8)}' y1='36' x2='{F(r * el.W / 8)}' y2='44' stroke='{Border}' stroke-width='1'></line>");
            contentY = 52;
        }

        var pageW = el.W * 0.75;
        var pageX = (el.W - pageW) / 2;
        sb.Append(Rect(pageX, contentY + 8, pageW, el.H - contentY - 16, "white", Border, 2));
        sb.Append(Text(title, pageX + 10, contentY + 28, 13, ColorText, "start", "600"));
        for (var r = 0; r < 6; r++)
        {
            var ly = contentY + 44 + r * 14;
            if (ly + 8 > el.H - 16)
                break;
            var lw = r % 4 == 3 ? pageW * 0.55 : pageW * 0.88;
            sb.Append(Rect(pageX + 10, ly, lw, 6, FillDark, "none", 2));
        }

        Svg(b, sb.ToString());
    }

    private static void RenderNotionEditor(WireframeElement el, RenderTreeBuilder b)
    {
        var title = el.Props.GetString("title", "Page Title");
        var showSidebar = el.Props.GetBool("showSidebar", true);
        var sb = new StringBuilder();
        sb.Append(Rect(0, 0, el.W, el.H, Fill, Border, 4));
        var sideW = showSidebar ? el.W * 0.22 : 0;
        if (showSidebar)
        {
            sb.Append(Rect(0, 0, sideW, el.H, FillDark, "none", 4));
            sb.Append(VLine(sideW, 0, el.H, Border));
            for (var i = 0; i < 5; i++)
            {
                var y = 12 + i * 26;
                sb.Append(Rect(6, y, sideW - 12, 20, i == 0 ? FillAccent : Fill, i == 0 ? "#93c5fd" : "none", 3));
                sb.Append(Text($"Page {i + 1}", sideW / 2, y + 10, 9, i == 0 ? Accent : ColorMuted, "middle"));
            }
        }

        var contentX = sideW + 8;
        sb.Append(Text(title, contentX, 28, 15, ColorText, "start", "600"));
        var blocks = new[] { (28.0, 1.0), (8.0, 0.9), (8.0, 0.7), (8.0, 1.2), (8.0, 0.85) };
        var by = 46.0;
        foreach (var (bh, bwRatio) in blocks)
        {
            if (by + bh > el.H - 8)
                break;
            sb.Append(Rect(contentX, by, (el.W - contentX - 12) * bwRatio, bh, FillDark, "none", 2));
            by += bh + 8;
        }

        Svg(b, sb.ToString());
    }
}
