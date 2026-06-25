// Phase D — core/stats.mjs
// Pure statistics helpers used across the perf-metrics pipeline.
//
// `median(values)` — sorts numeric input, picks middle element (or average of two
// when even-length), returns 0 for empty / all-non-finite input.
// `percentileNearestRank(values, percentile)` — nearest-rank percentile estimator
// (NIST primary method, no interpolation), returns 0 for empty input.

import { asArray } from './helpers.mjs';

function sortedFiniteSamples(values) {
    return asArray(values)
        .map(Number)
        .filter(function (value) { return Number.isFinite(value); })
        .sort(function (a, b) { return a - b; });
}

export function median(values) {
    const samples = sortedFiniteSamples(values);
    if (!samples.length) return 0;
    const middle = Math.floor(samples.length / 2);
    return samples.length % 2 === 0
        ? (samples[middle - 1] + samples[middle]) / 2
        : samples[middle];
}

export function percentileNearestRank(values, percentile) {
    const samples = sortedFiniteSamples(values);
    if (!samples.length) return 0;
    const rank = Math.max(1, Math.ceil(samples.length * percentile));
    return samples[Math.min(samples.length - 1, rank - 1)];
}
