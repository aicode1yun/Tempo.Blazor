// Phase D — runtime/layout-render-metrics.mjs
// `recordLayoutMetric` and `recordRenderMetric` accumulate layout-pass and render-pass
// timings against the strict-performance stats. They mirror the legacy IIFE inline
// helpers but take their stats accessor + timeline recorder via injection so the
// extraction can be unit-tested headlessly.

import { asArray } from '../core/helpers.mjs';

export function recordLayoutMetric(ensureStats, recordTimelineFn, inst, elapsedMs, reason, invalidatedScopes) {
    if (!inst) return null;
    const stats = ensureStats(inst);
    if (!stats) return null;
    const elapsed = Math.max(0, Number(elapsedMs || 0) || 0);
    stats.layoutPassCount = Number(stats.layoutPassCount || 0) + 1;
    stats.layoutPassLastMs = elapsed;
    stats.layoutPassTotalMs = Number(stats.layoutPassTotalMs || 0) + elapsed;
    stats.layoutPassMaxMs = Math.max(Number(stats.layoutPassMaxMs || 0), elapsed);
    stats.layoutLastReason = reason || '';
    stats.layoutInvalidatedPages = asArray(invalidatedScopes);
    stats.layoutInvalidatedPageCount = stats.layoutInvalidatedPages.length;
    if (typeof recordTimelineFn === 'function') {
        recordTimelineFn(inst, 'layout-pass', {
            reason: reason || '',
            elapsedMs: elapsed,
            invalidatedScopes: asArray(invalidatedScopes),
        });
    }
    return stats;
}

export function recordRenderMetric(ensureStats, recordTimelineFn, inst, elapsedMs, reason) {
    if (!inst) return null;
    const stats = ensureStats(inst);
    if (!stats) return null;
    const elapsed = Math.max(0, Number(elapsedMs || 0) || 0);
    stats.renderPassCount = Number(stats.renderPassCount || 0) + 1;
    stats.fullRenderCount = Number(stats.fullRenderCount || 0) + 1;
    stats.renderPassLastMs = elapsed;
    stats.renderPassTotalMs = Number(stats.renderPassTotalMs || 0) + elapsed;
    stats.renderPassMaxMs = Math.max(Number(stats.renderPassMaxMs || 0), elapsed);
    stats.renderLastReason = reason || '';
    if (typeof recordTimelineFn === 'function') {
        recordTimelineFn(inst, 'render-pass', {
            reason: reason || '',
            elapsedMs: elapsed,
        });
    }
    return stats;
}
