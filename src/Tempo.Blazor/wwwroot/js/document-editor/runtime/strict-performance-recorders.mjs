// Phase D — runtime/strict-performance-recorders.mjs
// Stateful recorders for the strict-mode performance pipeline. They mutate the
// `inst.stats` shape produced by `createStrictPerformanceStats()`, delegating to
// the pure helpers in `latency-histograms.mjs` for budget/summary computation.
//
// Each recorder takes an `ensureStrictPerformanceStats(inst)` callback as the first
// argument so the legacy IIFE can keep its inline copy (which also touches Blazor
// interop fields), while these extractions provide an injectable, headless variant
// for tests.

import { asArray, asText, clone, sortObject, unique } from '../core/helpers.mjs';
import {
    ensureLatencyHistogramState,
    latencyBudgetForName,
    createLatencyHistogramSummary,
} from './latency-histograms.mjs';

export const PERFORMANCE_HISTOGRAM_LIMIT = 500;
export const PARTIAL_RENDER_SCOPE_SAMPLES_LIMIT = 100;

// Records an elapsed sample into the named latency histogram (Keydown/Space/Enter/etc.),
// trimming to PERFORMANCE_HISTOGRAM_LIMIT. Stores a `lastLatencyDetails[name]` snapshot
// merged with the supplied detail object + Date.now(). Returns the new histogram summary
// (Count/LastMs/MaxMs/P50/P95/Budget/WithinBudget).
export function recordLatencyHistogram(ensureStats, inst, name, elapsedMs, detail) {
    if (!inst) return null;
    const stats = ensureStats(inst);
    const histograms = ensureLatencyHistogramState(stats);
    const key = histograms[name] ? name : 'KeydownVisibleText';
    const elapsed = Math.max(0, Number(elapsedMs || 0) || 0);
    histograms[key] = histograms[key].concat([elapsed]).slice(-PERFORMANCE_HISTOGRAM_LIMIT);
    stats.lastLatencyDetails[key] = sortObject(Object.assign({}, clone(detail || {}), {
        elapsedMs: elapsed,
        at: Date.now(),
    }));
    return createLatencyHistogramSummary(histograms[key], latencyBudgetForName(stats, key));
}

// Records the scope-id list of the latest partial render along with the operation
// type and detail payload. Caps `partialRenderScopeSamples` to the last 100 entries.
// Returns the dedup'd, sanitised scope id array.
export function recordPartialRenderScope(ensureStats, inst, operationType, scopeIds, detail) {
    if (!inst) return null;
    const stats = ensureStats(inst);
    const scopes = unique(asArray(scopeIds).map(asText).filter(Boolean));
    stats.lastPartialRenderScopeIds = scopes;
    stats.partialRenderScopeSamples = asArray(stats.partialRenderScopeSamples).concat([sortObject({
        operationType: asText(operationType || ''),
        scopeIds: scopes,
        detail: clone(detail || {}),
        at: Date.now(),
    })]).slice(-PARTIAL_RENDER_SCOPE_SAMPLES_LIMIT);
    return scopes;
}
