const DEFAULT_PALETTE = Object.freeze(['#2563eb', '#16a34a', '#f59e0b', '#dc2626', '#7c3aed', '#0891b2']);

export function buildDrawingChartLayout(chart, rect) {
    const source = normalizeChart(chart);
    const box = normalizeRect(rect);
    const titleHeight = source.title ? 24 : 10;
    const legendWidth = source.showLegend && source.series.length > 0
        ? Math.min(132, Math.max(82, box.width * 0.28))
        : 0;
    const categoryLabelHeight = isCartesianChart(source.type) ? 18 : 0;
    const axisLabelWidth = isCartesianChart(source.type) ? 28 : 0;
    const plot = {
        x: round(box.x + axisLabelWidth + 10),
        y: round(box.y + titleHeight),
        width: round(Math.max(24, box.width - axisLabelWidth - legendWidth - 22)),
        height: round(Math.max(24, box.height - titleHeight - categoryLabelHeight - 18)),
    };
    const legendRect = legendWidth > 0 ? {
        x: round(plot.x + plot.width + 12),
        y: round(plot.y),
        width: round(legendWidth - 14),
        height: round(Math.max(20, Math.min(plot.height, source.series.length * 18 + 6))),
    } : null;
    const values = source.series.flatMap(series => series.values);
    const max = Math.max(1, ...values.map(value => Math.abs(Number(value) || 0)));
    const categoryCount = Math.max(1, source.categories.length, ...source.series.map(series => series.values.length));

    return {
        type: source.type,
        title: source.title,
        categories: source.categories,
        series: source.series,
        palette: source.palette,
        rect: box,
        titleRect: source.title ? {
            x: round(box.x + 12),
            y: round(box.y + 6),
            width: round(box.width - 24),
            height: 16,
        } : null,
        plotRect: plot,
        legendRect,
        legendItems: legendRect ? source.series.map((series, index) => ({
            name: series.name || `Series ${index + 1}`,
            color: series.color || source.palette[index % source.palette.length],
            x: legendRect.x,
            y: round(legendRect.y + 4 + index * 18),
            width: legendRect.width,
            height: 14,
        })) : [],
        valueAxis: isCartesianChart(source.type) ? {
            min: 0,
            max,
            ticks: [0, max / 2, max].map(value => round(value)),
        } : null,
        categoryLabels: isCartesianChart(source.type)
            ? Array.from({ length: categoryCount }, (_, index) => ({
                text: source.categories[index] || String(index + 1),
                x: round(plot.x + (index + 0.5) * plot.width / categoryCount),
                y: round(plot.y + plot.height + 13),
            }))
            : [],
        seriesLayouts: buildSeriesLayouts(source, plot, max, categoryCount),
    };
}

function buildSeriesLayouts(chart, plot, max, categoryCount) {
    if (chart.type === 'pie' || chart.type === 'donut') {
        return buildPieLayout(chart, plot);
    }

    if (chart.type === 'line' || chart.type === 'area' || chart.type === 'scatter') {
        return chart.series.map((series, seriesIndex) => ({
            type: chart.type,
            name: series.name,
            color: series.color || chart.palette[seriesIndex % chart.palette.length],
            points: series.values.map((value, index) => ({
                x: round(plot.x + (series.values.length <= 1 ? plot.width / 2 : index * plot.width / (series.values.length - 1))),
                y: round(plot.y + plot.height - Math.abs(Number(value) || 0) / max * (plot.height - 8)),
                value,
                category: chart.categories[index] || String(index + 1),
            })),
            baselineY: round(plot.y + plot.height),
        }));
    }

    const seriesCount = Math.max(1, chart.series.length);
    const groupWidth = plot.width / categoryCount;
    const barWidth = Math.max(3, groupWidth / (seriesCount + 1));
    return chart.series.map((series, seriesIndex) => ({
        type: 'bar',
        name: series.name,
        color: series.color || chart.palette[seriesIndex % chart.palette.length],
        bars: series.values.map((value, categoryIndex) => {
            const height = Math.max(1, Math.abs(Number(value) || 0) / max * (plot.height - 8));
            return {
                x: round(plot.x + categoryIndex * groupWidth + 4 + seriesIndex * barWidth),
                y: round(plot.y + plot.height - height),
                width: round(Math.max(2, barWidth - 2)),
                height: round(height),
                value,
                category: chart.categories[categoryIndex] || String(categoryIndex + 1),
            };
        }),
    }));
}

function buildPieLayout(chart, plot) {
    const firstSeries = chart.series[0] || { values: [] };
    const values = firstSeries.values.map(value => Math.max(0, Number(value) || 0));
    const total = values.reduce((sum, value) => sum + value, 0) || 1;
    const radius = Math.max(6, Math.min(plot.width, plot.height) / 2 - 4);
    const center = {
        x: round(plot.x + plot.width / 2),
        y: round(plot.y + plot.height / 2),
    };
    let cursor = -Math.PI / 2;
    return [{
        type: chart.type,
        name: firstSeries.name,
        center,
        radius: round(radius),
        innerRadius: chart.type === 'donut' ? round(radius * 0.54) : 0,
        slices: values.map((value, index) => {
            const sweep = value / total * Math.PI * 2;
            const slice = {
                category: chart.categories[index] || String(index + 1),
                value,
                color: firstSeries.color || chart.palette[index % chart.palette.length],
                startAngle: cursor,
                endAngle: cursor + sweep,
            };
            cursor += sweep;
            return slice;
        }),
    }];
}

function normalizeChart(chart) {
    const source = chart && typeof chart === 'object' ? chart : {};
    const rawType = String(source.type ?? source.Type ?? 'bar').replace(/[\s_-]/g, '').toLowerCase();
    const type = rawType === 'column' ? 'bar'
        : ['bar', 'line', 'pie', 'donut', 'area', 'scatter'].includes(rawType) ? rawType : 'bar';
    const categories = Array.isArray(source.categories ?? source.Categories)
        ? (source.categories ?? source.Categories).map(item => String(item))
        : [];
    const palette = Array.isArray(source.palette ?? source.Palette) && (source.palette ?? source.Palette).length > 0
        ? (source.palette ?? source.Palette).map(item => String(item))
        : [...DEFAULT_PALETTE];
    const seriesSource = Array.isArray(source.series ?? source.Series) ? (source.series ?? source.Series) : [];
    const series = seriesSource.map((item, index) => ({
        name: String(item?.name ?? item?.Name ?? `Series ${index + 1}`),
        values: Array.isArray(item?.values ?? item?.Values) ? (item.values ?? item.Values).map(value => Number(value) || 0) : [],
        color: item?.color ?? item?.Color ?? null,
    }));
    return {
        type,
        title: source.title ?? source.Title ?? null,
        categories,
        series,
        showLegend: (source.showLegend ?? source.ShowLegend ?? true) !== false,
        palette,
    };
}

function isCartesianChart(type) {
    return type === 'bar' || type === 'line' || type === 'area' || type === 'scatter';
}

function normalizeRect(rect) {
    return {
        x: Number(rect?.x || 0) || 0,
        y: Number(rect?.y || 0) || 0,
        width: Math.max(1, Number(rect?.width || 0) || 1),
        height: Math.max(1, Number(rect?.height || 0) || 1),
    };
}

function round(value) {
    return Math.round((Number(value) || 0) * 1000) / 1000;
}
