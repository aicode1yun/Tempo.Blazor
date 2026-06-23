// Phase R.4.3 — core-engine/hit-test.mjs
// Pointer → model position, computed from the LAYOUT SNAPSHOT (caret stops), never
// from the DOM. The paragraph engine emits an exact caret stop per text offset with a
// precise rect, so hit-testing is "find the nearest line by y, then the nearest caret
// stop by x" — no per-character interpolation needed.
//
// All coordinates are LAYOUT (document) coordinates. The caller converts client/screen
// coordinates to layout coordinates (see render-host clientToLayout).
//
//   collectCaretStops(layout) → flat [{ blockId, offset, lineId, pageIndex, rect, affinity }]
//   hitTestPoint(layout, x, y) → { blockId, offset, lineId, pageIndex } | null
//   caretStopAt(layout, { blockId, offset, affinity? }) → caret stop | null
//   lineCaretStops(layout, lineId) → caret stops on a line, sorted by x

import { asArray } from '../core/helpers.mjs';

export function collectCaretStops(layout) {
    const stops = [];
    asArray(layout && layout.blocks).forEach(function (block) {
        asArray(block.caretStops).forEach(function (stop) {
            if (stop && stop.rect) stops.push(stop);
        });
    });
    return stops;
}

// Vertical distance from `y` to a caret stop's line band [rect.y, rect.y+height].
// 0 when inside the band.
function verticalDistance(stop, y) {
    const top = Number(stop.rect.y || 0) || 0;
    const bottom = top + (Number(stop.rect.height || 0) || 0);
    if (y < top) return top - y;
    if (y > bottom) return y - bottom;
    return 0;
}

export function hitTestPoint(layout, x, y) {
    const stops = collectCaretStops(layout);
    if (!stops.length) return null;
    // 1. Nearest line vertically.
    let minDy = Infinity;
    stops.forEach(function (stop) {
        const dy = verticalDistance(stop, y);
        if (dy < minDy) minDy = dy;
    });
    // 2. Among stops on (approximately) that line, the nearest by x.
    let best = null;
    let bestDx = Infinity;
    stops.forEach(function (stop) {
        if (Math.abs(verticalDistance(stop, y) - minDy) > 0.5) return;
        const dx = Math.abs((Number(stop.rect.x || 0) || 0) - x);
        if (dx < bestDx) { bestDx = dx; best = stop; }
    });
    if (!best) return null;
    return {
        blockId: best.blockId,
        offset: Number(best.offset || 0) || 0,
        lineId: best.lineId || null,
        pageIndex: Number(best.pageIndex || 0) || 0,
    };
}

export function lineCaretStops(layout, lineId) {
    return collectCaretStops(layout)
        .filter(function (stop) { return stop.lineId === lineId; })
        .sort(function (a, b) { return (a.rect.x || 0) - (b.rect.x || 0); });
}

export function caretStopAt(layout, position) {
    const pos = position || {};
    const blockId = pos.blockId;
    const offset = Number(pos.offset || 0) || 0;
    const affinity = pos.affinity || 'after';
    const lineId = pos.lineId || null;
    const matches = collectCaretStops(layout).filter(function (stop) {
        return stop.blockId === blockId && (Number(stop.offset || 0) || 0) === offset;
    });
    if (!matches.length) return null;
    if (matches.length === 1) return matches[0];
    // At a soft-wrap boundary the same offset is the END of line N AND the START of line N+1, both with
    // affinity 'after' — only the lineId distinguishes them. Honour it first so Home/clicks land on the
    // requested visual line instead of the previous line's end (B1).
    if (lineId) {
        const byLine = matches.find(function (stop) { return stop.lineId === lineId; });
        if (byLine) return byLine;
    }
    // Run-boundary duplicate (before/after affinity): pick by affinity, else first.
    const byAffinity = matches.find(function (stop) { return stop.affinity === affinity; });
    return byAffinity || matches[0];
}
