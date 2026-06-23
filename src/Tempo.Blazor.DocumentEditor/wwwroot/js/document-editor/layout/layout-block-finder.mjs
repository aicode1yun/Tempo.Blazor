// Phase D — layout/layout-block-finder.mjs
// Helpers that locate layout-block records (rendered shape) by id and the matching
// reference line for a given character offset. Used by the anchored-drawing layout
// and selection-restore pipelines.
//
//   - `findLayoutBlockById(layoutBlocks, blockId)` — returns the matching block
//     record by blockId, null when not found / empty input.
//   - `findReferenceLineForOffset(layoutBlock, offset)` — returns the first line
//     whose `[start, end]` range contains the offset, otherwise the first line.

import { asArray, asText } from '../core/helpers.mjs';

export function findLayoutBlockById(layoutBlocks, blockId) {
    const id = asText(blockId);
    if (!id) return null;
    return asArray(layoutBlocks).find(function (block) {
        return block && block.blockId === id;
    }) || null;
}

export function findReferenceLineForOffset(layoutBlock, offset) {
    if (!layoutBlock) return null;
    const target = Math.max(0, Number(offset || 0) || 0);
    const lines = asArray(layoutBlock.lines);
    return lines.find(function (line) {
        const start = Number(line && line.start || 0) || 0;
        const end = Number(line && line.end || 0) || 0;
        return target >= start && target <= end;
    }) || lines[0] || null;
}
