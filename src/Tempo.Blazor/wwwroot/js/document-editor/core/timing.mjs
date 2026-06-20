// Phase D — core/timing.mjs
// `nowMs()` — high-resolution timestamp in ms. Prefers `performance.now()` when
//   available, falls back to `Date.now()`. Useful for measuring elapsed time
//   without coupling to the test harness's clock.
// `elapsedWithSimulated(start, simulated)` — `max(nowMs() - start, simulated)`.
//   When code paths short-circuit early in test environments, `simulated` lets the
//   caller floor the reported elapsed time to a deterministic budget.

export function nowMs() {
    return typeof performance !== 'undefined' && performance && performance.now
        ? performance.now()
        : Date.now();
}

export function elapsedWithSimulated(start, simulated) {
    return Math.max(nowMs() - start, Number(simulated || 0) || 0);
}
