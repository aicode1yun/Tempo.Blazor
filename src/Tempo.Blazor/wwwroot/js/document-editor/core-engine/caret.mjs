// Phase R.4.3 / R.4.5 — core-engine/caret.mjs
// Caret geometry (pure, from layout caret stops) + a blinking DOM caret element.
//
// Pure:
//   blockMaxOffset(layout, blockId) → last caret offset in the block
//   moveCaretByKey(layout, { blockId, offset }, key, opts?) → next { blockId, offset }
//       (ArrowLeft/Right/Up/Down, Home, End — navigates by caret stops). When
//       `opts.text` (the block's plain text) is supplied, ArrowLeft/Right step by whole
//       grapheme clusters (emoji / combining marks), not UTF-16 code units (R.4.5).
//   caretRect(layout, position) → the caret stop rect (document coords) | null
//
// DOM:
//   createCaretElement({ doc }) → { element, place(rect), hide(), destroy() }
//       `place(rect)` positions the caret at page-local coords (caller subtracts the
//       page origin). Blink is a CSS animation injected once into the document head.

import { asArray } from '../core/helpers.mjs';
import { collectCaretStops, caretStopAt, lineCaretStops } from './hit-test.mjs';
import { nextGraphemeBoundary, prevGraphemeBoundary } from '../layout/grapheme.mjs';

export function blockMaxOffset(layout, blockId) {
    let max = 0;
    collectCaretStops(layout).forEach(function (stop) {
        if (stop.blockId === blockId) max = Math.max(max, Number(stop.offset || 0) || 0);
    });
    return max;
}

function blocksOrder(layout) {
    return asArray(layout && layout.blocks).map(function (b) { return b.blockId; });
}

function linesOrdered(layout) {
    const map = new Map();
    collectCaretStops(layout).forEach(function (stop) {
        const key = stop.lineId;
        if (!map.has(key)) {
            map.set(key, { lineId: key, blockId: stop.blockId, pageIndex: Number(stop.pageIndex || 0) || 0, y: Number(stop.rect.y || 0) || 0, stops: [] });
        }
        const entry = map.get(key);
        entry.stops.push(stop);
        entry.y = Math.min(entry.y, Number(stop.rect.y || 0) || 0);
    });
    return Array.from(map.values()).sort(function (a, b) {
        return (a.pageIndex - b.pageIndex) || (a.y - b.y);
    });
}

export function caretRect(layout, position) {
    const stop = caretStopAt(layout, position);
    return stop ? stop.rect : null;
}

export function moveCaretByKey(layout, position, key, opts) {
    const pos = position || {};
    const blockId = pos.blockId;
    const offset = Number(pos.offset || 0) || 0;
    const cur = caretStopAt(layout, pos);
    // Grapheme-aware horizontal stepping when the block text is supplied (R.4.5): a single
    // ArrowLeft/Right crosses a whole cluster so the caret never lands inside an emoji or
    // between a base letter and its combining mark. Falls back to ±1 code unit otherwise.
    const text = opts && typeof opts.text === 'string' ? opts.text : null;

    if (key === 'ArrowLeft') {
        if (offset > 0) return { blockId, offset: text != null ? prevGraphemeBoundary(text, offset) : offset - 1 };
        const order = blocksOrder(layout); const i = order.indexOf(blockId);
        if (i > 0) { const prev = order[i - 1]; return { blockId: prev, offset: blockMaxOffset(layout, prev) }; }
        return { blockId, offset };
    }
    if (key === 'ArrowRight') {
        const max = blockMaxOffset(layout, blockId);
        if (offset < max) return { blockId, offset: text != null ? nextGraphemeBoundary(text, offset) : offset + 1 };
        const order = blocksOrder(layout); const i = order.indexOf(blockId);
        if (i >= 0 && i < order.length - 1) return { blockId: order[i + 1], offset: 0 };
        return { blockId, offset };
    }
    if (key === 'Home' || key === 'End') {
        const stops = lineCaretStops(layout, cur ? cur.lineId : null);
        if (!stops.length) return { blockId, offset };
        const target = key === 'Home' ? stops[0] : stops[stops.length - 1];
        // Carry the lineId so the rendered caret resolves to THIS visual line: a wrap-boundary offset is
        // shared with the adjacent line's end/start, and without the lineId the lookup picks the wrong one
        // (Home would jump to the previous line's end — B1).
        return { blockId: target.blockId, offset: Number(target.offset || 0) || 0, lineId: target.lineId || null };
    }
    if (key === 'ArrowUp' || key === 'ArrowDown' || key === 'PageUp' || key === 'PageDown') {
        const lines = linesOrdered(layout);
        if (!lines.length) return { blockId, offset };
        const curLineId = cur ? cur.lineId : (lines[0] && lines[0].lineId);
        const idx = lines.findIndex(function (l) { return l.lineId === curLineId; });
        if (idx < 0) return { blockId, offset };
        const up = (key === 'ArrowUp' || key === 'PageUp');
        const paging = (key === 'PageUp' || key === 'PageDown');
        const step = paging ? Math.max(1, Number(opts && opts.pageLines) || 12) : 1;
        let targetIdx = up ? idx - step : idx + step;
        if (targetIdx < 0 || targetIdx >= lines.length) {
            // Arrow at the document edge → no move. PageUp/Down clamps to the first/last line.
            if (!paging) return { blockId, offset };
            targetIdx = Math.max(0, Math.min(lines.length - 1, targetIdx));
            if (targetIdx === idx) {
                const edgeStops = lines[up ? 0 : lines.length - 1].stops;
                const edge = up ? edgeStops[0] : edgeStops[edgeStops.length - 1];
                return edge ? { blockId: edge.blockId, offset: Number(edge.offset || 0) || 0, lineId: edge.lineId || null } : { blockId, offset };
            }
        }
        const x = cur ? (Number(cur.rect.x || 0) || 0) : 0;
        let best = null; let bestDx = Infinity;
        lines[targetIdx].stops.forEach(function (stop) {
            const dx = Math.abs((Number(stop.rect.x || 0) || 0) - x);
            if (dx < bestDx) { bestDx = dx; best = stop; }
        });
        // Carry the target line id so the rendered caret stays on the destination line at a wrap boundary (B1).
        return best ? { blockId: best.blockId, offset: Number(best.offset || 0) || 0, lineId: best.lineId || null } : { blockId, offset };
    }
    return { blockId, offset };
}

const CARET_BLINK_STYLE_ID = 'tm-core-caret-blink-style';

function ensureBlinkStyle(doc) {
    if (!doc || !doc.head || typeof doc.getElementById !== 'function') return;
    if (doc.getElementById(CARET_BLINK_STYLE_ID)) return;
    const style = doc.createElement('style');
    style.id = CARET_BLINK_STYLE_ID;
    style.textContent = '@keyframes tm-core-caret-blink{0%,49%{opacity:1}50%,100%{opacity:0}}'
        + '.tm-core-caret{position:absolute;width:2px;background:#1a1a1a;pointer-events:none;'
        + 'animation:tm-core-caret-blink 1.06s step-end infinite;z-index:20}'
        // R.5.20 — high contrast: use the system text colour so the caret stays visible when
        // the OS forces colours (Windows High Contrast), and opt out of background stripping.
        + '@media (forced-colors:active){.tm-core-caret{background:CanvasText;forced-color-adjust:none}}';
    try { doc.head.appendChild(style); } catch { /* ignore */ }
}

export function createCaretElement(options) {
    const opts = options || {};
    const doc = opts.doc || globalThis.document;
    ensureBlinkStyle(doc);
    const el = doc.createElement('div');
    el.className = 'tm-core-caret';
    el.setAttribute('data-testid', 'core-engine-caret');
    el.setAttribute('aria-hidden', 'true');
    el.style.position = 'absolute';
    el.style.width = '2px';
    el.style.display = 'none';

    function place(rect) {
        if (!rect) { hide(); return; }
        el.style.display = 'block';
        el.style.left = (Number(rect.x || 0) || 0) + 'px';
        el.style.top = (Number(rect.y || 0) || 0) + 'px';
        el.style.height = Math.max(1, Number(rect.height || 0) || 16) + 'px';
    }
    function hide() { el.style.display = 'none'; }
    function destroy() {
        if (el.parentNode && typeof el.parentNode.removeChild === 'function') el.parentNode.removeChild(el);
    }
    return { element: el, place, hide, destroy };
}
