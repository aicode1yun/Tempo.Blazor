// Phase D — core/inline-marker-ranges.mjs
// `createInlineMarkerRanges({asArray, resolveInlineRunDisplayText,
//   readCommentIdsFromRun, readRevisionIdsFromRun})` →
//   `{collectInlineCommentRanges, collectInlineRevisionRanges}`.
// Both walk every paragraph in body + headers + footers (recursing into table
// cells), tracking each run's char range, and accumulate per-marker-id ranges
// `{startBlockId, endBlockId, startOffset, endOffset}`. Repeated ids in the same
// block widen the existing range; cross-block ids keep their first block.

function buildCollector(asArray, resolveInlineRunDisplayText, readIdsFromRun) {
    return function collect(model) {
        const ranges = {};
        function remember(markerId, blockId, start, end) {
            if (!markerId || !blockId || end <= start) return;
            const existing = ranges[markerId];
            if (!existing) {
                ranges[markerId] = { startBlockId: blockId, endBlockId: blockId, startOffset: start, endOffset: end };
                return;
            }
            if (existing.startBlockId === blockId && existing.endBlockId === blockId) {
                existing.startOffset = Math.min(existing.startOffset, start);
                existing.endOffset = Math.max(existing.endOffset, end);
            }
        }
        function scanBlock(block) {
            if (!block || block.type !== 'paragraph') {
                if (block && block.type === 'table') {
                    asArray(block.content && block.content.rows).forEach(function (row) {
                        asArray(row.cells).forEach(function (cell) {
                            asArray(cell.blocks).forEach(scanBlock);
                        });
                    });
                }
                return;
            }
            let cursor = 0;
            asArray(block.content && block.content.runs).forEach(function (run) {
                const text = resolveInlineRunDisplayText(run);
                const start = cursor;
                const end = cursor + text.length;
                readIdsFromRun(run).forEach(function (markerId) {
                    remember(markerId, block.id, start, end);
                });
                cursor = end;
            });
        }
        asArray(model && model.body && model.body.blocks).forEach(scanBlock);
        asArray(model && model.headers).forEach(function (region) {
            asArray(region.blocks).forEach(scanBlock);
        });
        asArray(model && model.footers).forEach(function (region) {
            asArray(region.blocks).forEach(scanBlock);
        });
        return ranges;
    };
}

export function createInlineMarkerRanges(options) {
    const opts = options || {};
    for (const key of ['asArray', 'resolveInlineRunDisplayText',
        'readCommentIdsFromRun', 'readRevisionIdsFromRun']) {
        if (typeof opts[key] !== 'function') {
            throw new TypeError(`createInlineMarkerRanges requires options.${key} (function)`);
        }
    }
    const { asArray, resolveInlineRunDisplayText, readCommentIdsFromRun, readRevisionIdsFromRun } = opts;
    return Object.freeze({
        collectInlineCommentRanges: buildCollector(asArray, resolveInlineRunDisplayText, readCommentIdsFromRun),
        collectInlineRevisionRanges: buildCollector(asArray, resolveInlineRunDisplayText, readRevisionIdsFromRun),
    });
}
