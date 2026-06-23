import assert from 'node:assert/strict';
import test from 'node:test';
import { buildDrawingChartLayout } from '../chart-layout.mjs';

test('chart layout creates deterministic bar geometry with title labels and legend', () => {
    const layout = buildDrawingChartLayout({
        type: 'bar',
        title: 'Revenue',
        categories: ['Q1', 'Q2', 'Q3'],
        series: [
            { name: 'Actual', values: [3, 7, 5], color: '#2563eb' },
            { name: 'Plan', values: [4, 6, 8], color: '#16a34a' },
        ],
    }, { x: 40, y: 50, width: 320, height: 210 });

    assert.equal(layout.type, 'bar');
    assert.equal(layout.titleRect.height, 16);
    assert.equal(layout.seriesLayouts.length, 2);
    assert.equal(layout.seriesLayouts[0].bars.length, 3);
    assert.deepEqual(layout.categoryLabels.map(label => label.text), ['Q1', 'Q2', 'Q3']);
    assert.deepEqual(layout.legendItems.map(item => item.name), ['Actual', 'Plan']);
    assert.ok(layout.plotRect.width > 150);
    assert.ok(layout.plotRect.height > 100);
});

test('chart layout supports line area scatter and pie families', () => {
    for (const type of ['line', 'area', 'scatter']) {
        const layout = buildDrawingChartLayout({
            type,
            categories: ['A', 'B', 'C'],
            series: [{ name: 'Series', values: [1, 3, 2] }],
        }, { x: 0, y: 0, width: 220, height: 140 });
        assert.equal(layout.type, type);
        assert.equal(layout.seriesLayouts[0].points.length, 3);
    }

    const pie = buildDrawingChartLayout({
        type: 'pie',
        categories: ['A', 'B', 'C'],
        series: [{ name: 'Share', values: [2, 3, 5] }],
    }, { x: 0, y: 0, width: 220, height: 140 });
    assert.equal(pie.type, 'pie');
    assert.equal(pie.seriesLayouts[0].slices.length, 3);
    assert.equal(pie.seriesLayouts[0].innerRadius, 0);

    const donut = buildDrawingChartLayout({
        type: 'donut',
        categories: ['A', 'B'],
        series: [{ name: 'Share', values: [2, 3] }],
    }, { x: 0, y: 0, width: 220, height: 140 });
    assert.ok(donut.seriesLayouts[0].innerRadius > 0);
});
