// Phase D — runtime/performance-metrics.mjs
// `createPerformanceMetricsHarness()` accumulates per-instance perf metrics consumed
// by the Blazor side (latency baselines, layout pass counters, image-drag samples,
// memory cleanup state). Pure factory — no DOM, no Blazor interop, no instance Map.

import { asArray, asText, clone } from '../core/helpers.mjs';

export function createPerformanceMetricsHarness() {
    const metrics = {
        Baselines: [],
        TypingLatency: {},
        ImageDragLatency: {},
        SelectionMovementLatency: {},
        LayoutPassCount: 0,
        LayoutDragReflowCount: 0,
        LayoutResizeReflowCount: 0,
        LayoutInvalidatedPages: [],
        LayoutInvalidatedPageCount: 0,
        LastLayoutPassMs: 0,
        MemoryCleanup: null,
    };

    function latencySummary(samples) {
        const values = asArray(samples).map(Number).filter(function (value) {
            return Number.isFinite(value);
        });
        const total = values.reduce(function (sum, value) { return sum + value; }, 0);
        return {
            Count: values.length,
            LastMs: values.length ? values[values.length - 1] : 0,
            MaxMs: values.length ? Math.max.apply(Math, values) : 0,
            AverageMs: values.length ? total / values.length : 0,
        };
    }

    function baseline(name, samples) {
        const item = Object.assign({ Name: asText(name || 'baseline') }, latencySummary(samples));
        metrics.Baselines.push(item);
        return item;
    }

    return {
        recordTypingLatency(scenarioName, samples) {
            const item = baseline('typing-' + scenarioName, samples);
            metrics.TypingLatency[scenarioName] = item;
            return item;
        },
        recordImageDragLatency(samples) {
            metrics.ImageDragLatency = baseline('image-drag', samples);
            return metrics.ImageDragLatency;
        },
        recordSelectionMovementLatency(samples) {
            metrics.SelectionMovementLatency = baseline('selection-movement', samples);
            return metrics.SelectionMovementLatency;
        },
        recordLayoutPass(reason, _beforeSnapshot, afterSnapshot) {
            metrics.LayoutPassCount++;
            if (String(reason || '').indexOf('drag') >= 0) metrics.LayoutDragReflowCount++;
            if (String(reason || '').indexOf('resize') >= 0) metrics.LayoutResizeReflowCount++;
            const pages = asArray(afterSnapshot && afterSnapshot.Pages);
            pages.forEach(function (page) {
                const index = Number(page.PageIndex ?? page.index ?? 0) || 0;
                if (metrics.LayoutInvalidatedPages.indexOf(index) < 0) {
                    metrics.LayoutInvalidatedPages.push(index);
                }
            });
            metrics.LayoutInvalidatedPageCount = metrics.LayoutInvalidatedPages.length;
            metrics.LastLayoutPassMs = 0;
        },
        recordMemoryCleanup(cleanup) {
            metrics.MemoryCleanup = clone(cleanup || {});
            return metrics.MemoryCleanup;
        },
        metrics() { return clone(metrics); },
        snapshot() {
            return {
                HasInstance: true,
                Performance: clone(metrics),
                LayoutPassCount: metrics.LayoutPassCount,
                LayoutInvalidatedPageCount: metrics.LayoutInvalidatedPageCount,
                BaselineCount: metrics.Baselines.length,
            };
        },
        dispose() {},
    };
}
