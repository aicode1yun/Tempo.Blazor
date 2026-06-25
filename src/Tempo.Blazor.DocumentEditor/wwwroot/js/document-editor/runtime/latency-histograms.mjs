// Phase D — runtime/latency-histograms.mjs
// Per-instance latency budgets + histogram state used by the strict-mode performance
// pipeline. All functions here are pure (no DOM, no global state); the histogram
// summariser delegates p50/p95 to `core/stats.mjs`.
//
//   - `createDefaultLatencyBudgets()` — canonical ms budgets per metric name
//   - `createLatencyHistogramState()` — empty histogram bucket map
//   - `ensureLatencyHistogramState(stats)` — initialises histograms / lastLatencyDetails
//     / latencyBudgets on a stats object, returns the histogram bucket map
//   - `latencyBudgetForName(stats, name)` — resolves the budget for a metric name
//   - `createLatencyHistogramSummary(samples, budgetMs)` — Count/LastMs/MaxMs/P50/P95
//     + WithinBudget verdict

import { asArray, sortObject } from '../core/helpers.mjs';
import { median, percentileNearestRank } from '../core/stats.mjs';

export function createDefaultLatencyBudgets() {
    return {
        KeydownVisibleTextMs: 150,
        SpaceVisibleTextMs: 150,
        EnterVisibleTextMs: 220,
        ToolbarCommandVisibleStyleMs: 250,
        SelectionChangeToolbarStateMs: 200,
    };
}

export function createLatencyHistogramState() {
    return {
        KeydownVisibleText: [],
        SpaceVisibleText: [],
        EnterVisibleText: [],
        ToolbarCommandVisibleStyle: [],
        SelectionChangeToolbarState: [],
    };
}

export function ensureLatencyHistogramState(stats) {
    if (!stats.latencyHistograms || typeof stats.latencyHistograms !== 'object') {
        stats.latencyHistograms = createLatencyHistogramState();
    }
    Object.keys(createLatencyHistogramState()).forEach(function (key) {
        if (!Array.isArray(stats.latencyHistograms[key])) stats.latencyHistograms[key] = [];
    });
    if (!stats.lastLatencyDetails || typeof stats.lastLatencyDetails !== 'object') {
        stats.lastLatencyDetails = {};
    }
    if (!stats.latencyBudgets || typeof stats.latencyBudgets !== 'object') {
        stats.latencyBudgets = createDefaultLatencyBudgets();
    }
    return stats.latencyHistograms;
}

export function latencyBudgetForName(stats, name) {
    const budgets = stats && stats.latencyBudgets || createDefaultLatencyBudgets();
    switch (name) {
        case 'SpaceVisibleText':
            return Number(budgets.SpaceVisibleTextMs || 0) || 150;
        case 'EnterVisibleText':
            return Number(budgets.EnterVisibleTextMs || 0) || 220;
        case 'ToolbarCommandVisibleStyle':
            return Number(budgets.ToolbarCommandVisibleStyleMs || 0) || 250;
        case 'SelectionChangeToolbarState':
            return Number(budgets.SelectionChangeToolbarStateMs || 0) || 200;
        case 'KeydownVisibleText':
        default:
            return Number(budgets.KeydownVisibleTextMs || 0) || 150;
    }
}

export function createLatencyHistogramSummary(samples, budgetMs) {
    const values = asArray(samples).map(Number).filter(function (value) {
        return Number.isFinite(value);
    });
    const p95 = percentileNearestRank(values, 0.95);
    const budget = Number(budgetMs || 0) || 0;
    return sortObject({
        Count: values.length,
        LastMs: values.length ? values[values.length - 1] : 0,
        MaxMs: values.length ? Math.max.apply(Math, values) : 0,
        P50Ms: median(values),
        P95Ms: p95,
        BudgetMs: budget,
        WithinBudget: values.length === 0 || p95 <= (budget || Number.POSITIVE_INFINITY),
    });
}
