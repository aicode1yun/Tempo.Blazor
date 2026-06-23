// Phase R.4.0 — layout/font-metrics.mjs
// Real font metrics via an offscreen canvas `measureText`, with a synthetic fallback
// for headless (Node) environments. This is the foundation of the model-owned layout:
// the headless paragraph engine can now measure real glyph widths + vertical metrics
// WITHOUT touching the document DOM (no reflow), by measuring on a detached canvas.
//
// `createFontMetricsService(options?)` →
//   - `measureRun({text, fontFamily, fontSize, bold?, italic?, fontWeight?, fontStyle?,
//        letterSpacing?, zoom?}) → { width, ascent, descent, lineHeight }`
//   - `measureTextRun(request) → { Text, Width, Height }`  (drop-in for the engine's
//        synthetic `text-measurement` service — Width = run width, Height = lineHeight)
//   - `measureText(text, style) → { width, height }`  (convenience for the line-breaker
//        fallback builder)
//   - `clearCache()`, `getStats()`, `isUsingRealMetrics()`
//
// Options:
//   `createMeasureContext()` — returns a 2D canvas context (with `font` + `measureText`).
//        Default: OffscreenCanvas → document.createElement('canvas') → null (→ synthetic).
//        Inject a stub in Node tests.
//   `cacheLimit` — LRU cap (default 4096).
//
// Pure exports: `normalizeFontMetricStyle`, `fontStringFromStyle`,
//   `syntheticRunMetrics`, `computeFontMetricKey`.

import { asText, clone } from '../core/helpers.mjs';

const KEY_SEP = '';
const DEFAULT_CACHE_LIMIT = 4096;

// Normalise a measurement request into a canonical style record. Accepts both the
// `bold`/`italic` boolean shorthands and explicit `fontWeight`/`fontStyle` strings,
// plus Pascal/camel keys.
export function normalizeFontMetricStyle(request) {
    const source = request || {};
    let fontWeight = asText(source.FontWeight ?? source.fontWeight ?? '');
    if (!fontWeight) fontWeight = (source.bold === true || source.Bold === true) ? '700' : '400';
    let fontStyle = asText(source.FontStyle ?? source.fontStyle ?? '');
    if (!fontStyle) fontStyle = (source.italic === true || source.Italic === true) ? 'italic' : 'normal';
    return {
        text: asText(source.Text ?? source.text ?? ''),
        fontFamily: asText(source.FontFamily ?? source.fontFamily ?? 'Arial'),
        fontSize: Number(source.FontSize ?? source.fontSize ?? 12) || 12,
        fontWeight,
        fontStyle,
        characterScale: Math.max(0.1, Number(source.CharacterScale ?? source.characterScale ?? 1) || 1),
        fontVariantCaps: normalizeFontVariantCaps(source.FontVariantCaps ?? source.fontVariantCaps ?? 'normal'),
        kerning: source.Kerning ?? source.kerning ?? true,
        letterSpacing: Number(source.LetterSpacing ?? source.letterSpacing ?? 0) || 0,
        zoom: Number(source.Zoom ?? source.zoom ?? 1) || 1,
    };
}

// Cache key from the style fingerprint. Pure, stable.
export function computeFontMetricKey(style) {
    return [
        style.text, style.fontFamily, style.fontSize,
        style.fontWeight, style.fontStyle, style.characterScale, style.fontVariantCaps,
        style.kerning, style.letterSpacing, style.zoom,
    ].join(KEY_SEP);
}

// CSS font shorthand for canvas `ctx.font`. e.g. "italic 700 12px Arial".
export function fontStringFromStyle(style) {
    const parts = [];
    if (style.fontStyle && style.fontStyle !== 'normal') parts.push(style.fontStyle);
    const fontVariantCaps = normalizeFontVariantCaps(style.fontVariantCaps);
    if (fontVariantCaps && fontVariantCaps !== 'normal') parts.push(fontVariantCaps);
    if (style.fontWeight && style.fontWeight !== '400' && style.fontWeight !== 'normal') parts.push(style.fontWeight);
    parts.push(style.fontSize + 'px');
    parts.push(style.fontFamily || 'Arial');
    return parts.join(' ');
}

// Synthetic fallback (no canvas). Mirrors the legacy character-based width model so
// headless tests stay deterministic. Returns the rich `{width, ascent, descent,
// lineHeight}` shape.
export function syntheticRunMetrics(style) {
    let width = Array.from(style.text).reduce(
        (total, ch) => total + (/\s/.test(ch) ? style.fontSize * 0.32 : style.fontSize * 0.55),
        0);
    if (/700|bold/i.test(style.fontWeight)) width *= 1.08;
    if (/italic/i.test(style.fontStyle)) width *= 1.04;
    if (style.letterSpacing) {
        width += Math.max(0, Array.from(style.text).length - 1) * style.letterSpacing;
    }
    width *= style.characterScale;
    width *= style.zoom;
    const ascent = style.fontSize * 0.8 * style.zoom;
    const descent = style.fontSize * 0.2 * style.zoom;
    return {
        width: Math.max(0, width),
        ascent,
        descent,
        lineHeight: Math.max(1, Math.ceil(style.fontSize * 1.25 * style.zoom)),
    };
}

function defaultCreateMeasureContext() {
    try {
        if (typeof OffscreenCanvas === 'function') {
            const ctx = new OffscreenCanvas(8, 8).getContext('2d');
            if (ctx && typeof ctx.measureText === 'function') return ctx;
        }
    } catch { /* ignore */ }
    try {
        const doc = globalThis.document;
        if (doc && typeof doc.createElement === 'function') {
            const ctx = doc.createElement('canvas').getContext('2d');
            if (ctx && typeof ctx.measureText === 'function') return ctx;
        }
    } catch { /* ignore */ }
    return null;
}

export function createFontMetricsService(options) {
    const opts = options || {};
    const cacheLimit = Number(opts.cacheLimit) > 0 ? Number(opts.cacheLimit) : DEFAULT_CACHE_LIMIT;
    const createContext = typeof opts.createMeasureContext === 'function'
        ? opts.createMeasureContext
        : defaultCreateMeasureContext;

    let ctx = null;
    let ctxResolved = false;
    function context() {
        if (!ctxResolved) {
            try { ctx = createContext(); } catch { ctx = null; }
            ctxResolved = true;
        }
        return ctx;
    }

    // LRU cache (Map keeps insertion order; delete+set moves to MRU).
    const cache = new Map();
    const stats = {
        MeasureCount: 0,
        MeasureCacheHits: 0,
        MeasureCacheSize: 0,
        MeasureInvalidations: 0,
        MeasureEvictions: 0,
        UsingRealMetrics: false,
    };

    function cacheGet(key) {
        if (!cache.has(key)) return undefined;
        const value = cache.get(key);
        cache.delete(key);
        cache.set(key, value);
        return value;
    }

    function cacheSet(key, value) {
        cache.set(key, value);
        if (cache.size > cacheLimit) {
            const oldest = cache.keys().next().value;
            cache.delete(oldest);
            stats.MeasureEvictions += 1;
        }
        stats.MeasureCacheSize = cache.size;
    }

    // Measure on the real canvas; returns rich metrics or null if no context.
    function measureReal(style) {
        const c = context();
        if (!c) return null;
        try {
            c.font = fontStringFromStyle(style);
            if ('fontKerning' in c) {
                c.fontKerning = style.kerning === false || String(style.kerning).toLowerCase() === 'false' ? 'none' : 'normal';
            }
            const m = c.measureText(style.text);
            let width = Number(m.width) || 0;
            // CSS letter-spacing: applied between glyphs (n-1 gaps). measureText gives
            // the natural advance; add letter-spacing explicitly for determinism.
            if (style.letterSpacing) {
                width += Math.max(0, Array.from(style.text).length - 1) * style.letterSpacing;
            }
            width *= style.characterScale;
            width *= style.zoom;
            // Vertical metrics: prefer font-level bounding box (consistent across text),
            // fall back to actual bounding box, then to a synthetic ratio.
            let ascent = Number(m.fontBoundingBoxAscent);
            let descent = Number(m.fontBoundingBoxDescent);
            if (!Number.isFinite(ascent) || ascent <= 0) ascent = Number(m.actualBoundingBoxAscent);
            if (!Number.isFinite(descent) || descent < 0) descent = Number(m.actualBoundingBoxDescent);
            if (!Number.isFinite(ascent) || ascent <= 0) ascent = style.fontSize * 0.8;
            if (!Number.isFinite(descent) || descent < 0) descent = style.fontSize * 0.2;
            ascent *= style.zoom;
            descent *= style.zoom;
            const lineHeight = Math.max(1, Math.ceil((ascent + descent) * 1.15));
            stats.UsingRealMetrics = true;
            return { width: Math.max(0, width), ascent, descent, lineHeight };
        } catch {
            return null;
        }
    }

    function measureRun(request) {
        const style = normalizeFontMetricStyle(request);
        const key = computeFontMetricKey(style);
        const cached = cacheGet(key);
        if (cached !== undefined) {
            stats.MeasureCacheHits += 1;
            return clone(cached);
        }
        const result = measureReal(style) || syntheticRunMetrics(style);
        cacheSet(key, result);
        stats.MeasureCount += 1;
        return clone(result);
    }

    // Drop-in for the engine's synthetic text-measurement service.
    function measureTextRun(request) {
        const style = normalizeFontMetricStyle(request);
        const metrics = measureRun(request);
        return {
            Text: style.text,
            Width: Math.max(1, metrics.width),
            Height: Math.max(1, metrics.lineHeight),
        };
    }

    // Convenience for the line-breaker fallback builder.
    function measureText(text, style) {
        const metrics = measureRun(Object.assign({}, style || {}, { text }));
        return { width: Math.max(1, metrics.width), height: Math.max(1, metrics.lineHeight) };
    }

    function clearCache() {
        cache.clear();
        stats.MeasureCount = 0;
        stats.MeasureCacheHits = 0;
        stats.MeasureCacheSize = 0;
        stats.MeasureEvictions = 0;
        stats.MeasureInvalidations += 1;
    }

    function getStats() {
        stats.MeasureCacheSize = cache.size;
        return clone(stats);
    }

    function isUsingRealMetrics() {
        // Force context resolution so callers can detect availability up-front.
        return context() !== null;
    }

    return Object.freeze({
        measureRun,
        measureTextRun,
        measureText,
        clearCache,
        getStats,
        isUsingRealMetrics,
        computeCacheKey: (request) => computeFontMetricKey(normalizeFontMetricStyle(request)),
        normalizeStyle: normalizeFontMetricStyle,
    });
}

function normalizeFontVariantCaps(value) {
    const normalized = asText(value || 'normal').trim().toLowerCase();
    return normalized === 'small-caps' ? 'small-caps' : 'normal';
}
