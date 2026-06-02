// Phase R.4.3 — core-engine/selection-overlay.mjs
// Selection geometry (pure, from layout caret stops) + a DOM rect builder. A text
// selection between two logical positions becomes one rectangle per visual line: the
// span from the leftmost to the rightmost selected caret stop on that line.
//
//   selectionRectsForRange(layout, anchor, focus) → [{ pageIndex, rect }]  (document coords)
//       Returns [] for a collapsed selection.
//   createSelectionRectElement({ doc, rect }) → a positioned highlight div (page-local
//       coords supplied by the caller).
//   createCompositionUnderlineElement({ doc, rect }) → an IME pre-edit underline div
//       (R.4.4) reusing the same per-line rects.

import { asArray } from '../core/helpers.mjs';
import { collectCaretStops } from './hit-test.mjs';

const ORDER_STRIDE = 1e7;

function linearPos(orderMap, blockId, offset) {
    return (orderMap.has(blockId) ? orderMap.get(blockId) : 0) * ORDER_STRIDE + (Number(offset || 0) || 0);
}

export function selectionRectsForRange(layout, anchor, focus) {
    if (!anchor || !focus) return [];
    const order = new Map();
    asArray(layout && layout.blocks).forEach(function (block, index) { order.set(block.blockId, index); });
    const a = linearPos(order, anchor.blockId, anchor.offset);
    const f = linearPos(order, focus.blockId, focus.offset);
    const start = Math.min(a, f);
    const end = Math.max(a, f);
    if (start === end) return []; // collapsed → no highlight

    const lines = new Map();
    collectCaretStops(layout).forEach(function (stop) {
        const lp = linearPos(order, stop.blockId, stop.offset);
        if (lp < start || lp > end) return;
        const key = stop.lineId;
        const x = Number(stop.rect.x || 0) || 0;
        const y = Number(stop.rect.y || 0) || 0;
        const h = Number(stop.rect.height || 0) || 0;
        if (!lines.has(key)) {
            lines.set(key, { pageIndex: Number(stop.pageIndex || 0) || 0, y, height: h, minX: x, maxX: x });
        }
        const entry = lines.get(key);
        entry.minX = Math.min(entry.minX, x);
        entry.maxX = Math.max(entry.maxX, x);
        entry.y = Math.min(entry.y, y);
        entry.height = Math.max(entry.height, h);
    });

    const rects = [];
    lines.forEach(function (entry) {
        if (entry.maxX > entry.minX) {
            rects.push({
                pageIndex: entry.pageIndex,
                rect: { x: entry.minX, y: entry.y, width: entry.maxX - entry.minX, height: entry.height },
            });
        }
    });
    return rects;
}

// R.5.20 — high contrast: under forced colours the OS strips overlay backgrounds, so the
// selection / find highlights become invisible. Opt out and paint with system colours.
const OVERLAY_HC_STYLE_ID = 'tm-core-overlay-hc-style';
function ensureOverlayHighContrastStyle(doc) {
    if (!doc || !doc.head || typeof doc.getElementById !== 'function' || doc.getElementById(OVERLAY_HC_STYLE_ID)) return;
    const style = doc.createElement('style');
    style.id = OVERLAY_HC_STYLE_ID;
    style.textContent = '@media (forced-colors:active){'
        + '.tm-core-selection-rect{background:Highlight!important;forced-color-adjust:none}'
        + '.tm-core-find-highlight{background:Mark!important;color:MarkText;forced-color-adjust:none}'
        + '}';
    try { doc.head.appendChild(style); } catch { /* ignore */ }
}

export function createSelectionRectElement(options) {
    const opts = options || {};
    const doc = opts.doc || globalThis.document;
    ensureOverlayHighContrastStyle(doc);
    const rect = opts.rect || {};
    const el = doc.createElement('div');
    el.className = 'tm-core-selection-rect';
    el.setAttribute('data-testid', 'core-engine-selection-rect');
    el.setAttribute('aria-hidden', 'true');
    el.style.position = 'absolute';
    el.style.left = (Number(rect.x || 0) || 0) + 'px';
    el.style.top = (Number(rect.y || 0) || 0) + 'px';
    el.style.width = Math.max(0, Number(rect.width || 0) || 0) + 'px';
    el.style.height = Math.max(1, Number(rect.height || 0) || 16) + 'px';
    el.style.background = 'rgba(37, 99, 235, 0.28)';
    el.style.pointerEvents = 'none';
    el.style.zIndex = '15';
    return el;
}

// R.4.6h-2 — a find/replace match highlight. `current` paints the active match in a
// stronger orange; other matches in pale yellow (reuses the per-line selection rects).
export function createFindHighlightElement(options) {
    const opts = options || {};
    const doc = opts.doc || globalThis.document;
    ensureOverlayHighContrastStyle(doc);
    const rect = opts.rect || {};
    const el = doc.createElement('div');
    el.className = 'tm-core-find-highlight' + (opts.current ? ' tm-core-find-highlight--current' : '');
    el.setAttribute('data-testid', opts.current ? 'core-engine-find-current' : 'core-engine-find-match');
    el.setAttribute('aria-hidden', 'true');
    el.style.position = 'absolute';
    el.style.left = (Number(rect.x || 0) || 0) + 'px';
    el.style.top = (Number(rect.y || 0) || 0) + 'px';
    el.style.width = Math.max(0, Number(rect.width || 0) || 0) + 'px';
    el.style.height = Math.max(1, Number(rect.height || 0) || 16) + 'px';
    el.style.background = opts.current ? 'rgba(255, 145, 0, 0.55)' : 'rgba(255, 230, 0, 0.45)';
    el.style.pointerEvents = 'none';
    el.style.zIndex = '14';
    return el;
}

// R.4.4 — the IME composition (pre-edit) underline: a thin solid rule along the bottom
// of the composing text's line rect. Same per-line rects as a selection, drawn as an
// underline instead of a fill so the in-progress text reads as "not yet committed".
export function createCompositionUnderlineElement(options) {
    const opts = options || {};
    const doc = opts.doc || globalThis.document;
    const rect = opts.rect || {};
    const el = doc.createElement('div');
    el.className = 'tm-core-composition-underline';
    el.setAttribute('data-testid', 'core-engine-composition-underline');
    el.setAttribute('aria-hidden', 'true');
    el.style.position = 'absolute';
    el.style.left = (Number(rect.x || 0) || 0) + 'px';
    const height = Math.max(1, Number(rect.height || 0) || 16);
    el.style.top = ((Number(rect.y || 0) || 0) + height - 2) + 'px';
    el.style.width = Math.max(0, Number(rect.width || 0) || 0) + 'px';
    el.style.height = '2px';
    el.style.background = 'rgba(37, 99, 235, 0.9)';
    el.style.pointerEvents = 'none';
    el.style.zIndex = '16';
    return el;
}

// R.5.22 — a remote collaborator's caret: a colored vertical bar with a small name flag.
export function createRemoteCaretElement(options) {
    const opts = options || {};
    const doc = opts.doc || globalThis.document;
    const rect = opts.rect || {};
    const color = opts.color || '#16a34a';
    const el = doc.createElement('div');
    el.className = 'tm-core-remote-caret';
    el.setAttribute('data-testid', 'core-engine-remote-caret');
    el.setAttribute('data-remote-id', String(opts.id == null ? '' : opts.id));
    el.setAttribute('aria-hidden', 'true');
    el.style.position = 'absolute';
    el.style.left = (Number(rect.x || 0) || 0) + 'px';
    el.style.top = (Number(rect.y || 0) || 0) + 'px';
    el.style.width = '2px';
    el.style.height = Math.max(2, Number(rect.height || 0) || 16) + 'px';
    el.style.background = color;
    el.style.pointerEvents = 'none';
    el.style.zIndex = '29';
    if (opts.label) {
        const flag = doc.createElement('div');
        flag.className = 'tm-core-remote-caret-label';
        flag.textContent = String(opts.label);
        flag.style.position = 'absolute';
        flag.style.left = '0';
        flag.style.top = '-14px';
        flag.style.padding = '0 3px';
        flag.style.fontSize = '10px';
        flag.style.lineHeight = '14px';
        flag.style.whiteSpace = 'nowrap';
        flag.style.color = '#fff';
        flag.style.background = color;
        flag.style.borderRadius = '2px';
        el.appendChild(flag);
    }
    return el;
}

// R.5.23c — a red wavy underline under a misspelled word (one per line-rect of the word).
export function createSpellUnderlineElement(options) {
    const opts = options || {};
    const doc = opts.doc || globalThis.document;
    const rect = opts.rect || {};
    const el = doc.createElement('div');
    el.className = 'tm-core-spell-underline';
    el.setAttribute('data-testid', 'core-engine-spell-underline');
    el.setAttribute('aria-hidden', 'true');
    el.style.position = 'absolute';
    el.style.left = (Number(rect.x || 0) || 0) + 'px';
    const height = Math.max(1, Number(rect.height || 0) || 16);
    el.style.top = ((Number(rect.y || 0) || 0) + height - 3) + 'px';
    el.style.width = Math.max(0, Number(rect.width || 0) || 0) + 'px';
    el.style.height = '3px';
    el.style.pointerEvents = 'none';
    el.style.zIndex = '15';
    // A small SVG wavy stroke tiled horizontally (forced-colors keeps it visible).
    el.style.backgroundImage = "url(\"data:image/svg+xml;utf8,<svg xmlns='http://www.w3.org/2000/svg' width='6' height='3'><path d='M0 2 L1.5 0.5 L3 2 L4.5 0.5 L6 2' fill='none' stroke='%23dc2626' stroke-width='1'/></svg>\")";
    el.style.backgroundRepeat = 'repeat-x';
    el.style.backgroundPosition = 'left bottom';
    return el;
}
