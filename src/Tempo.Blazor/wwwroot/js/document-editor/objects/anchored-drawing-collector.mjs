// Phase D — objects/anchored-drawing-collector.mjs
// `createAnchoredDrawingRunCollector({normalizeImageObject})` factory →
// `collectAnchoredDrawingRuns(block, context?)` — walks a paragraph block's
// inline runs, returns `{blockId, inlineIndex, run, object}` records for each
// floating (non-inline) drawing run. Skips non-paragraph blocks and inline
// drawings. The returned `object` payload is `normalizeImageObject` with anchor
// metadata stitched from the region context (region/headerFooterId/tableId/cellId
// /columnIndex). When the anchor block is not locked and the run lives elsewhere,
// the anchor falls back to the owning block id.

import { asArray } from '../core/helpers.mjs';
import { sortObject } from '../core/helpers.mjs';
import { isDrawingRunSource } from '../core/inline-runs.mjs';
import { normalizeTextExclusionColumnIndex } from '../core/normalize-target.mjs';
import { readObjectLayoutInCell } from './anchor-region.mjs';

export function createAnchoredDrawingRunCollector(options) {
    const opts = options || {};
    if (typeof opts.normalizeImageObject !== 'function') {
        throw new TypeError(
            'createAnchoredDrawingRunCollector requires options.normalizeImageObject (function)');
    }
    const { normalizeImageObject } = opts;

    return function collectAnchoredDrawingRuns(block, context) {
        if (!block || block.type !== 'paragraph') return [];
        const regionContext = context || {};
        return asArray(block.content && block.content.runs).map(function (run, inlineIndex) {
            if (!run || !(run.kind === 'drawing' || isDrawingRunSource(run))) return null;
            const object = normalizeImageObject(run,
                Object.assign({ blockId: block.id || '', inlineIndex }, regionContext));
            if (!object || object.isInline === true) return null;
            if (!object.anchorBlockId
                || (object.lockAnchor !== true && object.anchorBlockId !== block.id)) {
                object.anchorBlockId = block.id || object.anchorBlockId || '';
            }
            if (regionContext.region
                && (object.anchorRegion === 'Body' || !object.anchorRegion)
                && regionContext.region !== 'Body') {
                object.anchorRegion = regionContext.region;
            } else {
                object.anchorRegion = object.anchorRegion || regionContext.region || 'Body';
            }
            object.anchorHeaderFooterId = object.anchorHeaderFooterId
                || regionContext.headerFooterId || '';
            object.anchorTableId = object.anchorTableId || regionContext.tableId || '';
            object.anchorCellId = object.anchorCellId || regionContext.cellId || '';
            object.anchorColumnIndex = normalizeTextExclusionColumnIndex(
                object.anchorColumnIndex ?? regionContext.columnIndex);
            object.layoutInCell = readObjectLayoutInCell(object);
            object.anchorInlineIndex = object.anchorInlineIndex >= 0
                ? object.anchorInlineIndex
                : inlineIndex;
            return sortObject({ blockId: block.id || '', inlineIndex, run, object });
        }).filter(Boolean);
    };
}
