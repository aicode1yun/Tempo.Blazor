export function createPerformanceMetrics(options = {}) {
    const clock = typeof options.now === 'function'
        ? options.now
        : () => Number(globalThis.performance?.now?.() || Date.now()) || 0;
    const maxSamples = Math.max(1, Number(options.maxSamples || 120) || 120);
    const renderDurations = [];
    const typingLatencies = [];
    const scrollFrames = [];
    let firstPaintMs = 0;
    let firstPaintRecorded = false;
    let renderCount = 0;
    const createdAt = clock();

    function recordRender(durationMs, render = {}) {
        const duration = normalizeDuration(durationMs);
        renderCount += 1;
        pushBounded(renderDurations, duration, maxSamples);
        if (!firstPaintRecorded && Number(render?.mountedPageCount || render?.pageCount || 0) > 0) {
            firstPaintRecorded = true;
            firstPaintMs = Math.max(0, clock() - createdAt);
        }

        return snapshot();
    }

    function recordTypingLatency(durationMs) {
        pushBounded(typingLatencies, normalizeDuration(durationMs), maxSamples);
        return latencySnapshot();
    }

    function recordScrollFrame(durationMs = 0) {
        pushBounded(scrollFrames, normalizeDuration(durationMs), maxSamples);
        return scrollSnapshot();
    }

    function snapshot() {
        return {
            firstPaintMs: round(firstPaintMs),
            renderCount,
            renderP50Ms: percentile(renderDurations, 50),
            renderP95Ms: percentile(renderDurations, 95),
            typing: latencySnapshot(),
            scroll: scrollSnapshot(),
        };
    }

    function latencySnapshot() {
        return {
            count: typingLatencies.length,
            p50Ms: percentile(typingLatencies, 50),
            p95Ms: percentile(typingLatencies, 95),
            lastMs: round(typingLatencies[typingLatencies.length - 1] || 0),
        };
    }

    function scrollSnapshot() {
        return {
            count: scrollFrames.length,
            p50Ms: percentile(scrollFrames, 50),
            p95Ms: percentile(scrollFrames, 95),
            lastMs: round(scrollFrames[scrollFrames.length - 1] || 0),
        };
    }

    return {
        recordRender,
        recordTypingLatency,
        recordScrollFrame,
        snapshot,
    };
}

function pushBounded(items, value, maxSamples) {
    items.push(value);
    while (items.length > maxSamples) {
        items.shift();
    }
}

function percentile(values, percentileValue) {
    if (!values.length) {
        return 0;
    }

    const sorted = [...values].sort((left, right) => left - right);
    const index = Math.min(sorted.length - 1, Math.max(0, Math.ceil((percentileValue / 100) * sorted.length) - 1));
    return round(sorted[index]);
}

function normalizeDuration(value) {
    const parsed = Number(value);
    return Number.isFinite(parsed) && parsed >= 0 ? parsed : 0;
}

function round(value) {
    return Math.round((Number(value) || 0) * 100) / 100;
}
