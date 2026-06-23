// Phase D — layout/test-text-measurer.mjs
// `createTestTextMeasurer()` factory — synthetic text measurer used by performance
// scenarios and integration tests. Returns a per-instance cache + measure functions.
//
// Mirror of the legacy IIFE's `measureTextRun` / `getTextRunMeasureCacheKey` etc.
// The legacy global cache is replaced by a per-instance Map so tests can hold
// independent measurers without cross-contamination.

import { asText, clone } from '../core/helpers.mjs';

const CACHE_KEY_SEPARATOR = '';

export function testTextMeasureStyle(request) {
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

export function getTextRunMeasureCacheKey(request) {
    const style = testTextMeasureStyle(request);
    return [
        style.text,
        style.fontFamily,
        style.fontSize,
        style.fontWeight,
        style.fontStyle,
        style.letterSpacing,
        style.zoom,
    ].join(CACHE_KEY_SEPARATOR);
}

export function createTestTextMeasurer() {
    const cache = new Map();
    let stats = {
        MeasureCount: 0,
        MeasureCacheHits: 0,
        MeasureCacheSize: 0,
        MeasureInvalidations: 0,
    };

    function measureTextRun(request) {
        const style = testTextMeasureStyle(request);
        const key = getTextRunMeasureCacheKey(request);
        if (cache.has(key)) {
            stats.MeasureCacheHits++;
            return clone(cache.get(key));
        }

        let width = Array.from(style.text).reduce(function (total, ch) {
            return total + (/\s/.test(ch) ? style.fontSize * 0.32 : style.fontSize * 0.55);
        }, 0);
        if (/700|bold/i.test(style.fontWeight)) width *= 1.08;
        if (/italic/i.test(style.fontStyle)) width *= 1.04;
        width += Math.max(0, style.text.length - 1) * style.letterSpacing;
        const result = {
            Text: style.text,
            Width: Math.max(1, width * style.zoom),
            Height: Math.max(1, Math.ceil(style.fontSize * 1.25 * style.zoom)),
        };
        cache.set(key, result);
        stats.MeasureCount++;
        stats.MeasureCacheSize = cache.size;
        return clone(result);
    }

    function clearTextRunMeasureCache() {
        cache.clear();
        stats = {
            MeasureCount: 0,
            MeasureCacheHits: 0,
            MeasureCacheSize: 0,
            MeasureInvalidations: (stats.MeasureInvalidations || 0) + 1,
        };
    }

    function getTextRunMeasureStats() {
        stats.MeasureCacheSize = cache.size;
        return clone(stats);
    }

    return Object.freeze({
        measureTextRun,
        clearTextRunMeasureCache,
        getTextRunMeasureStats,
    });
}
