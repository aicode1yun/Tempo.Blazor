// Phase R.4.5 — core-engine/bidi-line.mjs
// A post-layout bidi pass for the model-owned engine. The line-breaker lays text out in
// logical order, left to right. This pass reorders each line into VISUAL order using the
// resolved Unicode embedding levels (layout/bidi.mjs), so right-to-left text (Arabic /
// Hebrew) and mixed runs display correctly and the caret moves through them in the right
// place.
//
// Strategy (positioned-DOM friendly): we own line layout and segment placement; the
// browser owns intra-segment glyph shaping. So we:
//   1. resolve embedding levels for the line's logical text,
//   2. reorder + repack the segments into visual order, tagging RTL segments
//      `direction: 'rtl'` (the renderer sets `dir`/`unicode-bidi` so the browser shapes
//      Arabic joining + glyph order inside the box),
//   3. recompute every caret stop's x from its segment's NEW visual box, mirrored for RTL
//      segments — so caret geometry stays consistent with what is painted.
//
// Pure-LTR lines are left untouched (fast path → zero effect on existing behaviour).
// Limitations (documented follow-ups): explicit bidi formatting codes and segments that
// are themselves internally mixed-direction are not split here.
//
//   applyBidiToLayout(layout) → mutates layout.blocks[].{segments,caretStops} in place.

import { asArray } from '../core/helpers.mjs';
import { resolveLevels, hasRtl } from '../layout/bidi.mjs';

function num(v) { return Number(v || 0) || 0; }

function reorderSegmentsVisually(levels) {
    // L2 at segment granularity: reverse runs of higher level, high → low odd.
    const n = levels.length;
    const order = new Array(n);
    for (let i = 0; i < n; i++) order[i] = i;
    if (!n) return order;
    let max = 0; let minOdd = Infinity;
    for (let i = 0; i < n; i++) {
        if (levels[i] > max) max = levels[i];
        if ((levels[i] & 1) && levels[i] < minOdd) minOdd = levels[i];
    }
    if (minOdd === Infinity) return order;
    for (let lvl = max; lvl >= minOdd; lvl--) {
        let start = -1;
        for (let i = 0; i <= n; i++) {
            const lev = (i < n) ? levels[order[i]] : -1;
            if (lev >= lvl) { if (start < 0) start = i; }
            else if (start >= 0) {
                let a = start; let b = i - 1;
                while (a < b) { const t = order[a]; order[a] = order[b]; order[b] = t; a++; b--; }
                start = -1;
            }
        }
    }
    return order;
}

function reorderLine(lineSegs, lineStops) {
    // Skip lines containing inline objects / images — bidi for those is a later concern.
    if (lineSegs.some(function (s) { return s.objectId || s.inlineObject || s.type === 'object'; })) return;

    const ordered = lineSegs.slice().sort(function (a, b) { return num(a.start) - num(b.start); });
    if (!ordered.length) return;

    // Reconstruct the line's logical text + a code-unit-offset → bidi-level map.
    let lineText = '';
    const offsetAt = [];
    ordered.forEach(function (seg) {
        const txt = String(seg.text == null ? '' : seg.text);
        for (let k = 0; k < txt.length; k++) { lineText += txt[k]; offsetAt.push(num(seg.start) + k); }
    });
    if (!lineText) return;

    const resolved = resolveLevels(lineText);
    const levels = resolved.levels;
    // Pure LTR line (base LTR + no RTL anywhere) → leave logical layout as-is.
    if ((resolved.baseLevel % 2 === 0) && !hasRtl(levels)) return;

    const levelByOffset = new Map();
    for (let i = 0; i < offsetAt.length; i++) levelByOffset.set(offsetAt[i], levels[i]);

    // Per-segment level (from the segment's first character) → visual order + direction.
    const segLevels = ordered.map(function (seg) {
        const lvl = levelByOffset.get(num(seg.start));
        return (lvl == null) ? resolved.baseLevel : lvl;
    });
    const visualOrder = reorderSegmentsVisually(segLevels);

    // Repack segments left → right in visual order from the line's logical left edge.
    let lineLeft = Infinity;
    ordered.forEach(function (seg) { lineLeft = Math.min(lineLeft, num(seg.rect && seg.rect.x)); });
    if (!isFinite(lineLeft)) lineLeft = 0;

    const placement = new Map(); // seg → { left, width, rtl }
    let cursor = lineLeft;
    visualOrder.forEach(function (idx) {
        const seg = ordered[idx];
        const width = num(seg.rect && seg.rect.width);
        const rtl = (segLevels[idx] & 1) === 1;
        placement.set(seg, { left: cursor, width: width, rtl: rtl });
        if (seg.rect) seg.rect.x = cursor;
        seg.direction = rtl ? 'rtl' : 'ltr';
        cursor += width;
    });

    // Recompute each caret stop's x from its segment's new visual box (mirror for RTL).
    asArray(lineStops).forEach(function (stop) {
        const offset = num(stop.offset);
        const before = stop.affinity === 'before';
        // Choose the owning segment: a stop at a boundary belongs to the segment whose
        // [start,end) contains it; the trailing edge (offset === end) of the last segment
        // is handled by preferring start<=offset<end, then falling back to offset<=end.
        let owner = null;
        for (let i = 0; i < ordered.length; i++) {
            const seg = ordered[i];
            const s = num(seg.start); const e = num(seg.end);
            if (offset >= s && offset < e) { owner = seg; break; }
            if (offset === e) { owner = seg; if (!before) continue; else break; }
        }
        if (!owner) return;
        const place = placement.get(owner);
        if (!place || !stop.rect) return;
        const s = num(owner.start); const e = num(owner.end);
        const len = Math.max(1, e - s);
        const ratio = Math.max(0, Math.min(1, (offset - s) / len));
        stop.rect.x = place.rtl ? (place.left + place.width * (1 - ratio)) : (place.left + place.width * ratio);
    });
}

export function applyBidiToBlock(block) {
    const segs = asArray(block && block.segments);
    const stops = asArray(block && block.caretStops);
    if (!segs.length) return;
    const lineIds = new Set();
    segs.forEach(function (s) { lineIds.add(s.lineId); });
    lineIds.forEach(function (lineId) {
        reorderLine(
            segs.filter(function (s) { return s.lineId === lineId; }),
            stops.filter(function (s) { return s.lineId === lineId; }));
    });
}

export function applyBidiToLayout(layout) {
    asArray(layout && layout.blocks).forEach(function (block) {
        try { applyBidiToBlock(block); } catch { /* never let bidi break rendering */ }
    });
    return layout;
}
