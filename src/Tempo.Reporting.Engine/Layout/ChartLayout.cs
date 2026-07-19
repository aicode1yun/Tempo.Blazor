#pragma warning disable MA0048

using System.Globalization;
using System.Text;
using Tempo.Reporting.Abstractions.Data;
using Tempo.Reporting.Abstractions.Definitions;
using Tempo.Reporting.Engine.Expressions;
using Tempo.Reporting.Engine.Fonts;
using Tempo.Reporting.Engine.Processing;
using Tempo.Reporting.Engine.Snapshot;

namespace Tempo.Reporting.Engine.Layout;

/// <summary>Lays out engine-drawn report chart elements.</summary>
public static class ReportChartLayouter
{
    private static readonly IReadOnlyList<string> DefaultPalette =
    [
        "#2563eb",
        "#14b8a6",
        "#f59e0b",
        "#ef4444",
        "#8b5cf6",
        "#22c55e",
        "#ec4899",
        "#64748b",
    ];

    private const string FontFamily = "Inter";
    private const string TextColor = "#111827";
    private const string MutedTextColor = "#64748b";
    private const string GridColor = "#e5e7eb";
    private const string AxisColor = "#9ca3af";
    private const string PlotFill = "#ffffff";

    /// <summary>Creates snapshot commands for a chart element.</summary>
    public static IReadOnlyList<ReportSnapshotCommand> ToSnapshotCommands(
        ReportChartElement chart,
        ReportProcessingContext context,
        double x,
        double y,
        string idPrefix,
        ITextMeasurer measurer)
    {
        ArgumentNullException.ThrowIfNull(chart);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(measurer);

        if (!TryResolveDataSet(chart, context, out var dataSet) || chart.Series.Count == 0)
        {
            return CreateEmptyState(chart, x, y, idPrefix, measurer);
        }

        var series = ResolveSeries(chart, dataSet, context);
        return chart.ChartType switch
        {
            ReportChartType.Bar => CreateBarChart(chart, series, x, y, idPrefix, measurer, context.Culture),
            ReportChartType.Line => CreateLineChart(chart, series, x, y, idPrefix, measurer, context.Culture, fillArea: false),
            ReportChartType.Area => CreateLineChart(chart, series, x, y, idPrefix, measurer, context.Culture, fillArea: true),
            ReportChartType.Pie => CreateRadialChart(chart, series, x, y, idPrefix, measurer, context.Culture, donut: false),
            ReportChartType.Donut => CreateRadialChart(chart, series, x, y, idPrefix, measurer, context.Culture, donut: true),
            ReportChartType.StackedColumn => CreateStackedColumnChart(chart, series, x, y, idPrefix, measurer, context.Culture),
            ReportChartType.StackedBar => CreateStackedBarChart(chart, series, x, y, idPrefix, measurer, context.Culture),
            ReportChartType.StackedArea => CreateStackedAreaChart(chart, series, x, y, idPrefix, measurer, context.Culture),
            _ => CreateColumnChart(chart, series, x, y, idPrefix, measurer, context.Culture),
        };
    }

    private static IReadOnlyList<ReportSnapshotCommand> CreateColumnChart(
        ReportChartElement chart,
        IReadOnlyList<ChartSeriesData> series,
        double x,
        double y,
        string idPrefix,
        ITextMeasurer measurer,
        CultureInfo culture)
    {
        var commands = new List<ReportSnapshotCommand>();
        AddTitle(chart, commands, x, y, idPrefix, measurer);
        var frame = CreateCartesianFrame(chart, series, x, y, idPrefix, measurer, culture, commands);
        var categories = DistinctCategories(series);
        if (categories.Count == 0)
        {
            return commands;
        }

        var groupCount = Math.Max(1, series.Count);
        var bandWidth = frame.PlotWidth / categories.Count;
        var barWidth = Math.Max(4, Math.Min(28, frame.PlotWidth / (categories.Count * Math.Max(2.2, 3.4 / groupCount))));
        if (groupCount > 1)
        {
            barWidth = Math.Min(barWidth, Math.Max(4, bandWidth * 0.68 / groupCount));
        }

        for (var seriesIndex = 0; seriesIndex < series.Count; seriesIndex++)
        {
            var current = series[seriesIndex];
            for (var categoryIndex = 0; categoryIndex < categories.Count; categoryIndex++)
            {
                var value = current.ValueFor(categories[categoryIndex]);
                var height = frame.ScaleValue(value);
                var center = frame.CategoryCenter(categoryIndex, categories.Count);
                var offset = groupCount == 1 ? 0 : (seriesIndex - (groupCount - 1) / 2d) * (barWidth + 2);
                var barX = center - barWidth / 2 + offset;
                var barY = frame.PlotBottom - height;
                commands.Add(ReportSnapshotCommand.Rectangle(
                    $"{idPrefix}-chart-{current.Slug}-bar-{categoryIndex:000}",
                    Round(barX),
                    Round(barY),
                    Round(barWidth),
                    Round(height),
                    current.Color));
            }
        }

        return commands;
    }

    private static IReadOnlyList<ReportSnapshotCommand> CreateBarChart(
        ReportChartElement chart,
        IReadOnlyList<ChartSeriesData> series,
        double x,
        double y,
        string idPrefix,
        ITextMeasurer measurer,
        CultureInfo culture)
    {
        var commands = new List<ReportSnapshotCommand>();
        AddTitle(chart, commands, x, y, idPrefix, measurer);
        var frame = CreateCartesianFrame(chart, series, x, y, idPrefix, measurer, culture, commands);
        var categories = DistinctCategories(series);
        if (categories.Count == 0)
        {
            return commands;
        }

        var barHeight = Math.Max(5, Math.Min(22, frame.PlotHeight / (categories.Count * 2.5)));
        for (var seriesIndex = 0; seriesIndex < series.Count; seriesIndex++)
        {
            var current = series[seriesIndex];
            for (var categoryIndex = 0; categoryIndex < categories.Count; categoryIndex++)
            {
                var value = current.ValueFor(categories[categoryIndex]);
                var width = frame.ScaleValue(value, frame.PlotWidth);
                var centerY = frame.PlotTop + (categoryIndex + 0.5) * frame.PlotHeight / categories.Count;
                commands.Add(ReportSnapshotCommand.Rectangle(
                    $"{idPrefix}-chart-{current.Slug}-bar-{categoryIndex:000}",
                    Round(frame.AxisLeft),
                    Round(centerY - barHeight / 2),
                    Round(width),
                    Round(barHeight),
                    current.Color));
            }
        }

        return commands;
    }

    private static IReadOnlyList<ReportSnapshotCommand> CreateLineChart(
        ReportChartElement chart,
        IReadOnlyList<ChartSeriesData> series,
        double x,
        double y,
        string idPrefix,
        ITextMeasurer measurer,
        CultureInfo culture,
        bool fillArea)
    {
        var commands = new List<ReportSnapshotCommand>();
        AddTitle(chart, commands, x, y, idPrefix, measurer);
        var frame = CreateCartesianFrame(chart, series, x, y, idPrefix, measurer, culture, commands);
        var categories = DistinctCategories(series);
        if (categories.Count == 0)
        {
            return commands;
        }

        foreach (var current in series)
        {
            var points = categories
                .Select((category, index) => new ChartPoint(
                    frame.CategoryCenter(index, categories.Count),
                    frame.PlotBottom - frame.ScaleValue(current.ValueFor(category))))
                .ToArray();
            if (points.Length == 0)
            {
                continue;
            }

            var linePath = string.Join(" ", points.Select((point, index) => $"{(index == 0 ? "M" : "L")} {Format(point.X)} {Format(point.Y)}"));
            if (fillArea && points.Length > 1)
            {
                var areaPath = new StringBuilder(linePath);
                areaPath.Append(CultureInfo.InvariantCulture, $" L {Format(points[^1].X)} {Format(frame.PlotBottom)}");
                areaPath.Append(CultureInfo.InvariantCulture, $" L {Format(points[0].X)} {Format(frame.PlotBottom)} Z");
                commands.Add(ReportSnapshotCommand.Path(
                    $"{idPrefix}-chart-{current.Slug}-area",
                    areaPath.ToString(),
                    frame.AxisLeft,
                    frame.PlotTop,
                    frame.PlotWidth,
                    frame.PlotHeight,
                    WithAlpha(current.Color, "33")));
            }

            commands.Add(ReportSnapshotCommand.Path(
                $"{idPrefix}-chart-{current.Slug}-line",
                linePath,
                frame.AxisLeft,
                frame.PlotTop,
                frame.PlotWidth,
                frame.PlotHeight,
                stroke: current.Color,
                strokeWidth: 2));

            for (var index = 0; index < points.Length; index++)
            {
                commands.Add(ReportSnapshotCommand.Path(
                    $"{idPrefix}-chart-{current.Slug}-point-{index:000}",
                    CirclePath(points[index].X, points[index].Y, 2.8),
                    points[index].X - 2.8,
                    points[index].Y - 2.8,
                    5.6,
                    5.6,
                    PlotFill,
                    current.Color,
                    1.5));
            }
        }

        return commands;
    }

    private static IReadOnlyList<ReportSnapshotCommand> CreateRadialChart(
        ReportChartElement chart,
        IReadOnlyList<ChartSeriesData> series,
        double x,
        double y,
        string idPrefix,
        ITextMeasurer measurer,
        CultureInfo culture,
        bool donut)
    {
        var commands = new List<ReportSnapshotCommand>();
        AddTitle(chart, commands, x, y, idPrefix, measurer);
        var current = series.FirstOrDefault();
        if (current is null || current.Points.Count == 0)
        {
            return commands;
        }

        var top = y + (string.IsNullOrWhiteSpace(chart.Title) ? 12 : 28);
        var chartHeight = Math.Max(40, chart.Height - (top - y) - 18);
        var legendWidth = chart.ShowLegend ? Math.Min(84, Math.Max(58, chart.Width * 0.35)) : 0;
        var diameter = Math.Max(24, Math.Min(chart.Width - legendWidth - 20, chartHeight));
        var radius = diameter / 2;
        var centerX = x + 14 + radius;
        var centerY = top + chartHeight / 2;
        var innerRadius = donut ? radius * 0.52 : 0;
        var total = current.Points.Sum(point => Math.Max(0, point.Value));
        if (total <= 0.0001)
        {
            return commands;
        }

        var cursor = -Math.PI / 2;
        for (var index = 0; index < current.Points.Count; index++)
        {
            var point = current.Points[index];
            var angle = Math.Max(0, point.Value) / total * Math.PI * 2;
            var end = index == current.Points.Count - 1 ? Math.PI * 1.5 : cursor + angle;
            var color = ResolveColor(chart, index, point.SeriesColor);
            commands.Add(ReportSnapshotCommand.Path(
                $"{idPrefix}-chart-{current.Slug}-slice-{index:000}",
                donut
                    ? DonutSlicePath(centerX, centerY, radius, innerRadius, cursor, end)
                    : PieSlicePath(centerX, centerY, radius, cursor, end),
                centerX - radius,
                centerY - radius,
                diameter,
                diameter,
                color,
                PlotFill,
                1));
            cursor = end;
        }

        if (donut)
        {
            commands.Add(ReportSnapshotCommand.Path(
                $"{idPrefix}-chart-donut-hole",
                CirclePath(centerX, centerY, innerRadius),
                centerX - innerRadius,
                centerY - innerRadius,
                innerRadius * 2,
                innerRadius * 2,
                PlotFill));
        }

        if (chart.ShowLegend)
        {
            AddRadialLegend(chart, current, commands, x + chart.Width - legendWidth + 4, top + 6, idPrefix, measurer, culture);
        }

        return commands;
    }

    // ── Stacked charts (Fáze 18 / C5) ──
    // Each category's series segments accumulate on a shared baseline instead of sitting
    // side by side. The value axis is scaled to the largest STACK total (sum of the positive
    // series values in a category), so a full stack fills the plot. Non-positive values
    // contribute nothing to the stack (see StackedMaximum / ScaleValue clamping).

    private static IReadOnlyList<ReportSnapshotCommand> CreateStackedColumnChart(
        ReportChartElement chart,
        IReadOnlyList<ChartSeriesData> series,
        double x,
        double y,
        string idPrefix,
        ITextMeasurer measurer,
        CultureInfo culture)
    {
        var commands = new List<ReportSnapshotCommand>();
        AddTitle(chart, commands, x, y, idPrefix, measurer);
        var frame = CreateCartesianFrame(chart, series, x, y, idPrefix, measurer, culture, commands, stacked: true);
        var categories = DistinctCategories(series);
        if (categories.Count == 0)
        {
            return commands;
        }

        var bandWidth = frame.PlotWidth / categories.Count;
        var barWidth = Math.Max(6, Math.Min(34, bandWidth * 0.6));
        for (var categoryIndex = 0; categoryIndex < categories.Count; categoryIndex++)
        {
            var center = frame.CategoryCenter(categoryIndex, categories.Count);
            var barX = center - barWidth / 2;
            var cumulative = 0d;
            for (var seriesIndex = 0; seriesIndex < series.Count; seriesIndex++)
            {
                var current = series[seriesIndex];
                var height = frame.ScaleValue(current.ValueFor(categories[categoryIndex]));
                if (height <= 0)
                {
                    continue;
                }

                var barY = frame.PlotBottom - cumulative - height;
                commands.Add(ReportSnapshotCommand.Rectangle(
                    $"{idPrefix}-chart-{current.Slug}-bar-{categoryIndex:000}",
                    Round(barX),
                    Round(barY),
                    Round(barWidth),
                    Round(height),
                    current.Color));
                cumulative += height;
            }
        }

        return commands;
    }

    private static IReadOnlyList<ReportSnapshotCommand> CreateStackedBarChart(
        ReportChartElement chart,
        IReadOnlyList<ChartSeriesData> series,
        double x,
        double y,
        string idPrefix,
        ITextMeasurer measurer,
        CultureInfo culture)
    {
        var commands = new List<ReportSnapshotCommand>();
        AddTitle(chart, commands, x, y, idPrefix, measurer);
        var frame = CreateCartesianFrame(chart, series, x, y, idPrefix, measurer, culture, commands, stacked: true);
        var categories = DistinctCategories(series);
        if (categories.Count == 0)
        {
            return commands;
        }

        var barHeight = Math.Max(6, Math.Min(26, frame.PlotHeight / (categories.Count * 1.6)));
        for (var categoryIndex = 0; categoryIndex < categories.Count; categoryIndex++)
        {
            var centerY = frame.PlotTop + (categoryIndex + 0.5) * frame.PlotHeight / categories.Count;
            var cumulative = 0d;
            for (var seriesIndex = 0; seriesIndex < series.Count; seriesIndex++)
            {
                var current = series[seriesIndex];
                var width = frame.ScaleValue(current.ValueFor(categories[categoryIndex]), frame.PlotWidth);
                if (width <= 0)
                {
                    continue;
                }

                commands.Add(ReportSnapshotCommand.Rectangle(
                    $"{idPrefix}-chart-{current.Slug}-bar-{categoryIndex:000}",
                    Round(frame.AxisLeft + cumulative),
                    Round(centerY - barHeight / 2),
                    Round(width),
                    Round(barHeight),
                    current.Color));
                cumulative += width;
            }
        }

        return commands;
    }

    private static IReadOnlyList<ReportSnapshotCommand> CreateStackedAreaChart(
        ReportChartElement chart,
        IReadOnlyList<ChartSeriesData> series,
        double x,
        double y,
        string idPrefix,
        ITextMeasurer measurer,
        CultureInfo culture)
    {
        var commands = new List<ReportSnapshotCommand>();
        AddTitle(chart, commands, x, y, idPrefix, measurer);
        var frame = CreateCartesianFrame(chart, series, x, y, idPrefix, measurer, culture, commands, stacked: true);
        var categories = DistinctCategories(series);
        if (categories.Count == 0)
        {
            return commands;
        }

        var cumulative = new double[categories.Count];
        foreach (var current in series)
        {
            var top = new ChartPoint[categories.Count];
            var baseline = new ChartPoint[categories.Count];
            for (var index = 0; index < categories.Count; index++)
            {
                var height = frame.ScaleValue(current.ValueFor(categories[index]));
                var baseHeight = cumulative[index];
                var topHeight = baseHeight + height;
                var center = frame.CategoryCenter(index, categories.Count);
                top[index] = new ChartPoint(center, frame.PlotBottom - topHeight);
                baseline[index] = new ChartPoint(center, frame.PlotBottom - baseHeight);
                cumulative[index] = topHeight;
            }

            var areaPath = new StringBuilder();
            for (var index = 0; index < top.Length; index++)
            {
                areaPath.Append(CultureInfo.InvariantCulture, $"{(index == 0 ? "M" : " L")} {Format(top[index].X)} {Format(top[index].Y)}");
            }

            for (var index = baseline.Length - 1; index >= 0; index--)
            {
                areaPath.Append(CultureInfo.InvariantCulture, $" L {Format(baseline[index].X)} {Format(baseline[index].Y)}");
            }

            areaPath.Append(" Z");
            commands.Add(ReportSnapshotCommand.Path(
                $"{idPrefix}-chart-{current.Slug}-area",
                areaPath.ToString(),
                frame.AxisLeft,
                frame.PlotTop,
                frame.PlotWidth,
                frame.PlotHeight,
                WithAlpha(current.Color, "cc")));

            // Top outline drawn as a stroke (not a gap) so stacked segment area ratios stay exact.
            var topLine = string.Join(" ", top.Select((point, index) => $"{(index == 0 ? "M" : "L")} {Format(point.X)} {Format(point.Y)}"));
            commands.Add(ReportSnapshotCommand.Path(
                $"{idPrefix}-chart-{current.Slug}-area-line",
                topLine,
                frame.AxisLeft,
                frame.PlotTop,
                frame.PlotWidth,
                frame.PlotHeight,
                stroke: current.Color,
                strokeWidth: 1.5));
        }

        return commands;
    }

    private static double StackedMaximum(IReadOnlyList<ChartSeriesData> series, IReadOnlyList<ChartCategoryKey> categories)
    {
        var max = 0d;
        foreach (var category in categories)
        {
            var sum = 0d;
            foreach (var current in series)
            {
                var value = current.ValueFor(category);
                if (value > 0)
                {
                    sum += value;
                }
            }

            if (sum > max)
            {
                max = sum;
            }
        }

        return max;
    }

    private static CartesianFrame CreateCartesianFrame(
        ReportChartElement chart,
        IReadOnlyList<ChartSeriesData> series,
        double x,
        double y,
        string idPrefix,
        ITextMeasurer measurer,
        CultureInfo culture,
        List<ReportSnapshotCommand> commands,
        bool stacked = false)
    {
        var categories = DistinctCategories(series);
        var maxValue = stacked
            ? StackedMaximum(series, categories)
            : Math.Max(0, series.SelectMany(item => item.Points).Select(point => point.Value).DefaultIfEmpty().Max());
        var axis = NiceAxis(maxValue);
        var top = y + 10;
        var plotHeight = Math.Max(36, chart.Height * 0.5);
        var centerLeft = x + Math.Max(34, Math.Min(50, chart.Width * 0.23));
        var centerRight = x + chart.Width - (chart.ShowLegend ? 62 : 30);
        if (centerRight <= centerLeft)
        {
            centerRight = centerLeft + Math.Max(24, chart.Width * 0.35);
        }

        var axisLeft = centerLeft - 16;
        var axisRight = centerRight + 16;
        var plotTop = top;
        var plotBottom = plotTop + plotHeight;
        var plotWidth = axisRight - axisLeft;
        commands.Add(ReportSnapshotCommand.Rectangle(
            $"{idPrefix}-chart-plot",
            Round(axisLeft),
            Round(plotTop),
            Round(plotWidth),
            Round(plotHeight),
            PlotFill,
            "#f3f4f6",
            0.75));

        if (chart.ShowValueAxis)
        {
            for (var index = 0; index <= axis.TickCount; index++)
            {
                var value = axis.Maximum * index / axis.TickCount;
                var yPos = plotBottom - plotHeight * index / axis.TickCount;
                commands.Add(ReportSnapshotCommand.Line(
                    $"{idPrefix}-chart-grid-{index}",
                    Round(axisLeft),
                    Round(yPos),
                    Round(plotWidth),
                    0,
                    GridColor,
                    0.6));
                commands.Add(Text(
                    $"{idPrefix}-chart-y-label-{index}",
                    FormatValue(value, culture),
                    axisLeft - 6 - Measure(FormatValue(value, culture), measurer, 8).Width,
                    yPos + 3,
                    8,
                    MutedTextColor,
                    measurer));
            }
        }

        commands.Add(ReportSnapshotCommand.Line(
            $"{idPrefix}-chart-axis-x",
            Round(axisLeft),
            Round(plotBottom),
            Round(plotWidth),
            0,
            AxisColor,
            0.8));
        commands.Add(ReportSnapshotCommand.Line(
            $"{idPrefix}-chart-axis-y",
            Round(axisLeft),
            Round(plotTop),
            0,
            Round(plotHeight),
            AxisColor,
            0.8));

        if (chart.ShowCategoryAxis && categories.Count > 0)
        {
            var collision = LabelsCollide(categories, measurer, Math.Max(1, (centerRight - centerLeft) / Math.Max(1, categories.Count - 1)));
            for (var index = 0; index < categories.Count; index++)
            {
                var label = FormatCategory(categories[index], culture);
                var center = categories.Count == 1
                    ? (centerLeft + centerRight) / 2
                    : centerLeft + (centerRight - centerLeft) * index / (categories.Count - 1);
                var measure = Measure(label, measurer, 8);
                commands.Add(Text(
                    $"{idPrefix}-chart-x-label-{index:000}",
                    label,
                    collision ? center - 2 : center - measure.Width / 2,
                    collision ? plotBottom + 18 : plotBottom + 14,
                    8,
                    MutedTextColor,
                    measurer,
                    rotation: collision ? -35 : 0));
            }
        }

        if (!string.IsNullOrWhiteSpace(chart.CategoryAxisTitle))
        {
            var measure = Measure(chart.CategoryAxisTitle, measurer, 8);
            commands.Add(Text(
                $"{idPrefix}-chart-category-title",
                chart.CategoryAxisTitle,
                axisLeft + plotWidth / 2 - measure.Width / 2,
                y + chart.Height - 7,
                8,
                MutedTextColor,
                measurer));
        }

        if (!string.IsNullOrWhiteSpace(chart.ValueAxisTitle))
        {
            commands.Add(Text(
                $"{idPrefix}-chart-value-title",
                chart.ValueAxisTitle,
                x + 6,
                plotTop + plotHeight / 2 + Measure(chart.ValueAxisTitle, measurer, 8).Width / 2,
                8,
                MutedTextColor,
                measurer,
                rotation: -90));
        }

        if (chart.ShowLegend)
        {
            AddSeriesLegend(series, commands, axisRight + 12, plotTop + 8, idPrefix, measurer);
        }

        return new CartesianFrame(axisLeft, axisRight, centerLeft, centerRight, plotTop, plotBottom, plotWidth, plotHeight, axis.Maximum);
    }

    private static void AddTitle(
        ReportChartElement chart,
        List<ReportSnapshotCommand> commands,
        double x,
        double y,
        string idPrefix,
        ITextMeasurer measurer)
    {
        if (string.IsNullOrWhiteSpace(chart.Title))
        {
            return;
        }

        commands.Add(Text(
            $"{idPrefix}-chart-title",
            chart.Title,
            x + 2,
            y + 9,
            10,
            TextColor,
            measurer,
            fontWeight: "700"));
    }

    private static void AddSeriesLegend(
        IReadOnlyList<ChartSeriesData> series,
        List<ReportSnapshotCommand> commands,
        double x,
        double y,
        string idPrefix,
        ITextMeasurer measurer)
    {
        for (var index = 0; index < series.Count; index++)
        {
            var item = series[index];
            var rowY = y + index * 14;
            commands.Add(ReportSnapshotCommand.Rectangle(
                $"{idPrefix}-chart-legend-swatch-{index:000}",
                Round(x),
                Round(rowY - 7),
                7,
                7,
                item.Color));
            commands.Add(Text(
                $"{idPrefix}-chart-legend-label-{index:000}",
                item.Name,
                x + 11,
                rowY,
                8,
                TextColor,
                measurer));
        }
    }

    private static void AddRadialLegend(
        ReportChartElement chart,
        ChartSeriesData series,
        List<ReportSnapshotCommand> commands,
        double x,
        double y,
        string idPrefix,
        ITextMeasurer measurer,
        CultureInfo culture)
    {
        commands.Add(Text(
            $"{idPrefix}-chart-legend-series",
            series.Name,
            x,
            y,
            8,
            TextColor,
            measurer,
            fontWeight: "700"));

        for (var index = 0; index < series.Points.Count; index++)
        {
            var point = series.Points[index];
            var rowY = y + 14 + index * 13;
            commands.Add(ReportSnapshotCommand.Rectangle(
                $"{idPrefix}-chart-legend-swatch-{index:000}",
                Round(x),
                Round(rowY - 7),
                7,
                7,
                ResolveColor(chart, index, point.SeriesColor)));
            commands.Add(Text(
                $"{idPrefix}-chart-legend-label-{index:000}",
                FormatCategory(point.Category, culture),
                x + 11,
                rowY,
                8,
                TextColor,
                measurer));
        }
    }

    private static IReadOnlyList<ReportSnapshotCommand> CreateEmptyState(
        ReportChartElement chart,
        double x,
        double y,
        string idPrefix,
        ITextMeasurer measurer)
        =>
        [
            ReportSnapshotCommand.Rectangle(idPrefix, x, y, chart.Width, chart.Height, PlotFill, "#e5e7eb", 0.75),
            Text($"{idPrefix}-empty", "No chart data", x + 8, y + Math.Min(chart.Height - 6, 18), 9, MutedTextColor, measurer),
        ];

    private static IReadOnlyList<ChartSeriesData> ResolveSeries(ReportChartElement chart, ProcessedDataSet dataSet, ReportProcessingContext context)
    {
        var series = new List<ChartSeriesData>(chart.Series.Count);
        for (var seriesIndex = 0; seriesIndex < chart.Series.Count; seriesIndex++)
        {
            var definition = chart.Series[seriesIndex];
            var points = new List<ChartDataPoint>();
            foreach (var row in dataSet.Rows)
            {
                var categoryValue = Evaluate(definition.CategoryExpression, row, dataSet.Rows, context, dataSet.Rows);
                var value = Evaluate(definition.ValueExpression, row, dataSet.Rows, context, dataSet.Rows);
                var categoryKey = new ChartCategoryKey(categoryValue.RawValue ?? categoryValue.AsString());
                var existing = points.FirstOrDefault(point => point.Category.Equals(categoryKey));
                if (existing is null)
                {
                    points.Add(new ChartDataPoint(categoryKey, ToDouble(value), definition.Color));
                }
                else
                {
                    existing.Value += ToDouble(value);
                }
            }

            series.Add(new ChartSeriesData(
                string.IsNullOrWhiteSpace(definition.Name) ? $"Series {seriesIndex + 1}" : definition.Name,
                Slugify(definition.Name, seriesIndex),
                ResolveColor(chart, seriesIndex, definition.Color),
                points));
        }

        return series;
    }

    private static ExpressionValue Evaluate(
        string expression,
        ProcessedDataRow row,
        IReadOnlyList<ProcessedDataRow> scopeRows,
        ReportProcessingContext context,
        IReadOnlyList<ProcessedDataRow> reportRows)
        => string.IsNullOrWhiteSpace(expression)
            ? ExpressionValue.Null
            : ReportAggregateEngine.EvaluateForRow(expression, row, scopeRows, context, reportRows);

    private static bool TryResolveDataSet(ReportChartElement chart, ReportProcessingContext context, out ProcessedDataSet dataSet)
    {
        dataSet = new ProcessedDataSet(string.Empty, [], []);
        var dataSetName = chart.DataSetName;
        if (string.IsNullOrWhiteSpace(dataSetName))
        {
            return false;
        }

        if (!context.DataSets.TryGetValue(dataSetName, out var resolved) || resolved is null)
        {
            return false;
        }

        dataSet = resolved;
        return true;
    }

    private static IReadOnlyList<ChartCategoryKey> DistinctCategories(IReadOnlyList<ChartSeriesData> series)
    {
        var categories = new List<ChartCategoryKey>();
        foreach (var point in series.SelectMany(item => item.Points))
        {
            if (!categories.Contains(point.Category))
            {
                categories.Add(point.Category);
            }
        }

        return categories;
    }

    private static bool LabelsCollide(IReadOnlyList<ChartCategoryKey> categories, ITextMeasurer measurer, double slotWidth)
        => categories.Count > 1 &&
            categories.Select(category => Measure(FormatCategory(category, CultureInfo.InvariantCulture), measurer, 8).Width).DefaultIfEmpty().Max() > slotWidth * 0.85;

    private static NiceAxisDefinition NiceAxis(double maxValue)
    {
        var maximum = Math.Max(1, maxValue);
        var rawStep = maximum / 3d;
        var power = Math.Pow(10, Math.Floor(Math.Log10(rawStep)));
        var fraction = rawStep / power;
        var niceFraction = fraction <= 1
            ? 1
            : fraction <= 2
                ? 2
                : fraction <= 4
                    ? 4
                    : fraction <= 5
                        ? 5
                        : 10;
        var step = niceFraction * power;
        var niceMax = Math.Ceiling(maximum / step) * step;
        return new NiceAxisDefinition(niceMax, 3);
    }

    private static string ResolveColor(ReportChartElement chart, int index, string? explicitColor)
    {
        if (!string.IsNullOrWhiteSpace(explicitColor))
        {
            return explicitColor;
        }

        if (chart.ColorPalette.Count > 0)
        {
            return chart.ColorPalette[index % chart.ColorPalette.Count];
        }

        return DefaultPalette[index % DefaultPalette.Count];
    }

    private static ReportSnapshotCommand Text(
        string id,
        string text,
        double x,
        double baseline,
        double fontSize,
        string fill,
        ITextMeasurer measurer,
        string fontWeight = "400",
        double rotation = 0)
    {
        var measurement = Measure(text, measurer, fontSize, fontWeight);
        return ReportSnapshotCommand.TextRun(
            id,
            text,
            Round(x),
            Round(baseline),
            Round(measurement.Width),
            Round(measurement.LineHeight),
            FontFamily,
            fontSize,
            fill,
            fontWeight,
            rotation: rotation);
    }

    private static TextMeasurement Measure(string text, ITextMeasurer measurer, double fontSize, string fontWeight = "400")
        => measurer.MeasureRun(new TextMeasureRequest(text, FontFamily, fontSize, Bold: string.Equals(fontWeight, "700", StringComparison.Ordinal)));

    private static string FormatValue(double value, CultureInfo culture)
        => Math.Abs(value - Math.Round(value)) < 0.0001
            ? Math.Round(value).ToString("N0", culture)
            : value.ToString("N2", culture);

    private static string FormatCategory(ChartCategoryKey key, CultureInfo culture)
        => key.Value switch
        {
            null => string.Empty,
            DateTime date => date.ToString("d", culture),
            DateTimeOffset date => date.ToString("d", culture),
            IFormattable formattable => formattable.ToString(null, culture),
            _ => Convert.ToString(key.Value, culture) ?? string.Empty,
        };

    private static double ToDouble(ExpressionValue value)
        => value.Kind == ExpressionValueKind.Null ? 0 : (double)value.AsNumber();

    private static string Slugify(string value, int index)
    {
        var text = string.IsNullOrWhiteSpace(value) ? $"series-{index + 1}" : value;
        var builder = new StringBuilder();
        foreach (var ch in text)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        return builder.Length == 0 ? $"series-{index + 1}" : builder.ToString().Trim('-');
    }

    private static string WithAlpha(string color, string alpha)
        => color.Length == 7 && color[0] == '#' ? color + alpha : color;

    private static string CirclePath(double centerX, double centerY, double radius)
        => string.Create(
            CultureInfo.InvariantCulture,
            $"M {Format(centerX - radius)} {Format(centerY)} a {Format(radius)} {Format(radius)} 0 1 0 {Format(radius * 2)} 0 a {Format(radius)} {Format(radius)} 0 1 0 {Format(-radius * 2)} 0");

    private static string PieSlicePath(double centerX, double centerY, double radius, double startAngle, double endAngle)
    {
        var start = Polar(centerX, centerY, radius, startAngle);
        var end = Polar(centerX, centerY, radius, endAngle);
        var largeArc = endAngle - startAngle > Math.PI ? 1 : 0;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"M {Format(centerX)} {Format(centerY)} L {Format(start.X)} {Format(start.Y)} A {Format(radius)} {Format(radius)} 0 {largeArc} 1 {Format(end.X)} {Format(end.Y)} Z");
    }

    private static string DonutSlicePath(double centerX, double centerY, double radius, double innerRadius, double startAngle, double endAngle)
    {
        var outerStart = Polar(centerX, centerY, radius, startAngle);
        var outerEnd = Polar(centerX, centerY, radius, endAngle);
        var innerStart = Polar(centerX, centerY, innerRadius, startAngle);
        var innerEnd = Polar(centerX, centerY, innerRadius, endAngle);
        var largeArc = endAngle - startAngle > Math.PI ? 1 : 0;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"M {Format(outerStart.X)} {Format(outerStart.Y)} A {Format(radius)} {Format(radius)} 0 {largeArc} 1 {Format(outerEnd.X)} {Format(outerEnd.Y)} L {Format(innerEnd.X)} {Format(innerEnd.Y)} A {Format(innerRadius)} {Format(innerRadius)} 0 {largeArc} 0 {Format(innerStart.X)} {Format(innerStart.Y)} Z");
    }

    private static ChartPoint Polar(double centerX, double centerY, double radius, double angle)
        => new(centerX + radius * Math.Cos(angle), centerY + radius * Math.Sin(angle));

    private static string Format(double value)
        => Round(value).ToString("0.##", CultureInfo.InvariantCulture);

    private static double Round(double value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private sealed record NiceAxisDefinition(double Maximum, int TickCount);

    private sealed record CartesianFrame(
        double AxisLeft,
        double AxisRight,
        double CenterLeft,
        double CenterRight,
        double PlotTop,
        double PlotBottom,
        double PlotWidth,
        double PlotHeight,
        double AxisMaximum)
    {
        public double ScaleValue(double value)
            => ScaleValue(value, PlotHeight);

        public double ScaleValue(double value, double size)
            => AxisMaximum <= 0 ? 0 : Math.Max(0, value) / AxisMaximum * size;

        public double CategoryCenter(int index, int categoryCount)
            => categoryCount <= 1
                ? (CenterLeft + CenterRight) / 2
                : CenterLeft + (CenterRight - CenterLeft) * index / (categoryCount - 1);
    }

    private sealed class ChartSeriesData
    {
        public ChartSeriesData(string name, string slug, string color, IReadOnlyList<ChartDataPoint> points)
        {
            Name = name;
            Slug = slug;
            Color = color;
            Points = points.ToArray();
        }

        public string Name { get; }

        public string Slug { get; }

        public string Color { get; }

        public IReadOnlyList<ChartDataPoint> Points { get; }

        public double ValueFor(ChartCategoryKey category)
            => Points.FirstOrDefault(point => point.Category.Equals(category))?.Value ?? 0;
    }

    private sealed class ChartDataPoint
    {
        public ChartDataPoint(ChartCategoryKey category, double value, string? seriesColor)
        {
            Category = category;
            Value = value;
            SeriesColor = seriesColor;
        }

        public ChartCategoryKey Category { get; }

        public double Value { get; set; }

        public string? SeriesColor { get; }
    }

    private sealed record ChartCategoryKey(object? Value);

    private sealed record ChartPoint(double X, double Y);
}

#pragma warning restore MA0048
