// Phase D — layout/text-measurement.mjs
// `createTextMeasurementService()` — synthetic text measurement service used by tests
// and the headless layout engine. Provides `measureTextRun(request) → {Text, Width,
// Height}` with a memoising cache keyed by the style fingerprint.
//
// The width model is character-based (no real font metrics, no DOM): wide chars
// (`fontSize * 0.55`), whitespace (`fontSize * 0.32`), bold/italic multipliers,
// letter-spacing offset. Height is `ceil(fontSize * 1.25 * zoom)`. Real browser
// metrics are used at runtime by the production renderer (in the legacy IIFE); this
// module is the headless fallback that Node-based tests run against.
//
// Pure factory — each call to `createTextMeasurementService()` produces an isolated
// cache.

import { asText, clone } from '../core/helpers.mjs';

// Field separator used inside the cache key. The ASCII unit-separator (``) is
// guaranteed never to appear in text or font names.
const KEY_SEP = '';

// Normalise the measurement-request input into a canonical style record.
export function normalizeMeasureStyle(request) {
    const source = request || {};
    return {
        text: asText(source.Text ?? source.text ?? ''),
        fontFamily: asText(source.FontFamily ?? source.fontFamily ?? 'Arial'),
        fontSize: Number(source.FontSize ?? source.fontSize ?? 12) || 12,
        fontWeight: asText(source.FontWeight ?? source.fontWeight ?? '400'),
        fontStyle: asText(source.FontStyle ?? source.fontStyle ?? 'normal'),
        letterSpacing: Number(source.LetterSpacing ?? source.letterSpacing ?? 0) || 0,
        zoom: Number(source.Zoom ?? source.zoom ?? 1) || 1,
    };
}

// Compute the cache key from the style fingerprint. Pure; stable across runs.
export function computeMeasureCacheKey(request) {
    const style = normalizeMeasureStyle(request);
    return [
        style.text,
        style.fontFamily,
        style.fontSize,
        style.fontWeight,
        style.fontStyle,
        style.letterSpacing,
        style.zoom,
    ].join(KEY_SEP);
}

// Pure measurement function (no cache). Returns `{Text, Width, Height}`.
export function measureTextRunPure(request) {
    const style = normalizeMeasureStyle(request);
    let width = Array.from(style.text).reduce(
        (total, ch) => total + (/\s/.test(ch) ? style.fontSize * 0.32 : style.fontSize * 0.55),
        0);
    if (/700|bold/i.test(style.fontWeight)) width *= 1.08;
    if (/italic/i.test(style.fontStyle)) width *= 1.04;
    width += Math.max(0, style.text.length - 1) * style.letterSpacing;
    return {
        Text: style.text,
        Width: Math.max(1, width * style.zoom),
        Height: Math.max(1, Math.ceil(style.fontSize * 1.25 * style.zoom)),
    };
}

// Factory — produces a measurement service with its own cache + stats.
// Returns `{ measureTextRun, clearCache, getStats, computeCacheKey, normalizeStyle }`.
export function createTextMeasurementService() {
    const cache = new Map();
    const stats = {
        MeasureCount: 0,
        MeasureCacheHits: 0,
        MeasureCacheSize: 0,
        MeasureInvalidations: 0,
    };

    function measureTextRun(request) {
        const key = computeMeasureCacheKey(request);
        if (cache.has(key)) {
            stats.MeasureCacheHits += 1;
            return clone(cache.get(key));
        }
        const result = measureTextRunPure(request);
        cache.set(key, result);
        stats.MeasureCount += 1;
        stats.MeasureCacheSize = cache.size;
        return clone(result);
    }

    function clearCache() {
        cache.clear();
        stats.MeasureCount = 0;
        stats.MeasureCacheHits = 0;
        stats.MeasureCacheSize = 0;
        stats.MeasureInvalidations += 1;
    }

    function getStats() {
        stats.MeasureCacheSize = cache.size;
        return clone(stats);
    }

    return Object.freeze({
        measureTextRun,
        clearCache,
        getStats,
        computeCacheKey: computeMeasureCacheKey,
        normalizeStyle: normalizeMeasureStyle,
    });
}
