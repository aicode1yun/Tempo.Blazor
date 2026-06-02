// Phase R.4.5 — layout/grapheme.mjs
// Grapheme-cluster segmentation: the unit a human perceives as one character even when
// it is several UTF-16 code units (astral emoji, ZWJ emoji sequences like 👨‍👩‍👧, base
// letter + combining marks like e + ◌́). The caret must move by graphemes, not by code
// units, or it lands "inside" an emoji / between a letter and its accent.
//
// Built on the platform `Intl.Segmenter` (the engine's own Unicode segmentation — modern
// browsers and Node ≥ 16 ship it). A surrogate- and combining-mark-aware scan is kept as
// a fallback so the module degrades gracefully where Segmenter is missing.
//
//   graphemeBoundaries(text)            → sorted code-unit offsets [0 … text.length]
//   nextGraphemeBoundary(text, offset)  → first boundary strictly after offset
//   prevGraphemeBoundary(text, offset)  → last boundary strictly before offset
//   graphemeCount(text)                 → number of grapheme clusters
//   isGraphemeBoundary(text, offset)    → true when offset sits on a cluster edge

let _segmenter = null;

function graphemeSegmenter() {
    if (_segmenter !== null) return _segmenter;
    try {
        if (typeof Intl !== 'undefined' && typeof Intl.Segmenter === 'function') {
            _segmenter = new Intl.Segmenter(undefined, { granularity: 'grapheme' });
            return _segmenter;
        }
    } catch { /* fall through to the manual scanner */ }
    _segmenter = false;
    return _segmenter;
}

// Combining marks that the fallback scanner attaches to the preceding base character.
// (Intl.Segmenter, when present, handles the full Unicode rules — this is a safety net.)
function isCombiningMark(cp) {
    return (cp >= 0x0300 && cp <= 0x036F)   // combining diacritical marks
        || (cp >= 0x0483 && cp <= 0x0489)   // Cyrillic combining
        || (cp >= 0x0591 && cp <= 0x05BD)   // Hebrew points
        || (cp >= 0x0610 && cp <= 0x061A)   // Arabic marks
        || (cp >= 0x064B && cp <= 0x065F)   // Arabic marks
        || cp === 0x0670                    // Arabic letter superscript alef
        || (cp >= 0x06D6 && cp <= 0x06DC)
        || (cp >= 0x0E31 && cp <= 0x0E3A)   // Thai
        || (cp >= 0x1AB0 && cp <= 0x1AFF)   // combining diacritical marks extended
        || (cp >= 0x1DC0 && cp <= 0x1DFF)   // combining diacritical marks supplement
        || (cp >= 0x20D0 && cp <= 0x20FF)   // combining marks for symbols
        || (cp >= 0xFE20 && cp <= 0xFE2F);  // combining half marks
}

function codeUnitStep(str, i) {
    const code = str.charCodeAt(i);
    // High surrogate followed by a low surrogate → astral code point (2 units).
    if (code >= 0xD800 && code <= 0xDBFF && i + 1 < str.length) {
        const next = str.charCodeAt(i + 1);
        if (next >= 0xDC00 && next <= 0xDFFF) return 2;
    }
    return 1;
}

export function graphemeBoundaries(text) {
    const str = String(text == null ? '' : text);
    if (!str.length) return [0];
    const seg = graphemeSegmenter();
    const bounds = [0];
    if (seg) {
        for (const piece of seg.segment(str)) {
            bounds.push(piece.index + piece.segment.length);
        }
        if (bounds[bounds.length - 1] !== str.length) bounds.push(str.length);
        return bounds;
    }
    // Fallback: code point step + swallow trailing combining marks + ZWJ glue.
    let i = 0;
    while (i < str.length) {
        let next = i + codeUnitStep(str, i);
        // Glue ZWJ sequences (emoji): … ZWJ <next cp> …
        while (next < str.length) {
            const cp = str.codePointAt(next);
            if (cp === 0x200D) { // ZWJ → consume it and the following code point
                next += 1;
                if (next < str.length) next += codeUnitStep(str, next);
                continue;
            }
            if (isCombiningMark(cp) || cp === 0xFE0F /* variation selector-16 */) {
                next += codeUnitStep(str, next);
                continue;
            }
            break;
        }
        bounds.push(next);
        i = next;
    }
    return bounds;
}

export function nextGraphemeBoundary(text, offset) {
    const str = String(text == null ? '' : text);
    const o = Math.max(0, Math.min(str.length, Number(offset || 0) || 0));
    const bounds = graphemeBoundaries(str);
    for (let k = 0; k < bounds.length; k++) {
        if (bounds[k] > o) return bounds[k];
    }
    return str.length;
}

export function prevGraphemeBoundary(text, offset) {
    const str = String(text == null ? '' : text);
    const o = Math.max(0, Math.min(str.length, Number(offset || 0) || 0));
    const bounds = graphemeBoundaries(str);
    let prev = 0;
    for (let k = 0; k < bounds.length; k++) {
        if (bounds[k] < o) prev = bounds[k];
        else break;
    }
    return prev;
}

export function isGraphemeBoundary(text, offset) {
    const o = Number(offset || 0) || 0;
    return graphemeBoundaries(text).indexOf(o) !== -1;
}

export function graphemeCount(text) {
    return Math.max(0, graphemeBoundaries(text).length - 1);
}

// R.5.6 — the word range [start, end) containing (or adjacent to) `offset`. Used by
// double-click word selection. Prefers Intl.Segmenter word granularity; falls back to a
// Unicode letter/number/underscore run scan.
export function wordRangeAt(text, offset) {
    const t = String(text == null ? '' : text);
    const o = Math.max(0, Math.min(t.length, Number(offset) || 0));
    if (!t.length) return { start: 0, end: 0 };
    try {
        if (typeof Intl !== 'undefined' && typeof Intl.Segmenter === 'function') {
            const seg = new Intl.Segmenter(undefined, { granularity: 'word' });
            let fallback = null;
            for (const piece of seg.segment(t)) {
                const start = piece.index;
                const end = start + piece.segment.length;
                if (o >= start && o < end) return { start: start, end: end };
                if (o === end) fallback = { start: start, end: end }; // caret at a word's trailing edge
            }
            if (fallback) return fallback;
        }
    } catch (e) { /* fall through to the manual scan */ }

    const isWord = function (ch) { return ch != null && /[\p{L}\p{N}_]/u.test(ch); };
    let s = o, e = o;
    if (o < t.length && isWord(t[o])) {
        while (s > 0 && isWord(t[s - 1])) s--;
        while (e < t.length && isWord(t[e])) e++;
    } else if (o > 0 && isWord(t[o - 1])) {
        s = o; e = o;
        while (s > 0 && isWord(t[s - 1])) s--;
    } else {
        return { start: o, end: o };
    }
    return { start: s, end: e };
}
