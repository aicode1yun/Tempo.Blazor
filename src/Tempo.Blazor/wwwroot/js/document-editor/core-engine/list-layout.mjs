// Phase R.4.8 (lists) — core-engine/list-layout.mjs
// Post-layout pass (runs after engine.layoutDocument + the bidi pass) that turns text
// blocks tagged as list items into indented items with a hanging marker. The paragraph
// engine has no native indent, so we shift each list block's segments + caret stops right
// by a gutter (+ a per-level step) and attach a block-relative `listMarker` descriptor the
// renderer draws in the freed gutter. Numbering is computed across the whole document so
// items renumber automatically when items are inserted / deleted / re-leveled.
//
//   applyListLayout(layout, model) → mutates layout.blocks[] in place; returns the layout.
//
// Marker coords are BLOCK-RELATIVE (localX/localY) so the renderer's per-page localization
// leaves them correct. Limitation (documented follow-up): segments are shifted, not
// re-wrapped within a narrower box, so a very long line approaches the right margin a touch
// earlier than its indent implies.

import { asArray } from '../core/helpers.mjs';
import { isListBlock, listLevelOf, computeListMarkers } from './list-model.mjs';

export const LIST_MARKER_GUTTER = 28; // px reserved between the marker and the text
export const LIST_LEVEL_STEP = 24;    // px added per nesting level

export function applyListLayout(layout, model) {
    const blocks = layout && asArray(layout.blocks);
    if (!blocks || !blocks.length) return layout;

    const modelBlocks = asArray(model && model.body && model.body.blocks);
    const byId = new Map();
    modelBlocks.forEach(function (b) { if (b && b.id != null) byId.set(b.id, b); });
    const markers = computeListMarkers(modelBlocks);

    blocks.forEach(function (bl) {
        const mb = byId.get(bl.blockId);
        if (!isListBlock(mb)) { if (bl.listMarker) bl.listMarker = null; return; }

        const level = listLevelOf(mb);
        const indent = LIST_MARKER_GUTTER + level * LIST_LEVEL_STEP;

        // Shift the text + caret geometry right to make room for the hanging marker.
        asArray(bl.segments).forEach(function (seg) {
            if (seg && seg.rect) seg.rect.x = (Number(seg.rect.x) || 0) + indent;
        });
        asArray(bl.caretStops).forEach(function (stop) {
            if (stop && stop.rect) stop.rect.x = (Number(stop.rect.x) || 0) + indent;
        });

        // Anchor the marker to the first line (or the first caret stop for an empty item).
        const anchor = (asArray(bl.segments)[0] && asArray(bl.segments)[0].rect)
            || (asArray(bl.caretStops)[0] && asArray(bl.caretStops)[0].rect)
            || bl.rect || { y: 0, height: 0 };
        const blockY = (bl.rect && Number(bl.rect.y)) || 0;
        bl.listMarker = {
            text: markers.get(bl.blockId) || '•',
            localX: level * LIST_LEVEL_STEP,
            localY: (Number(anchor.y) || 0) - blockY,
            height: Number(anchor.height) || (bl.rect && Number(bl.rect.height)) || 0,
            level: level,
        };
    });
    return layout;
}
