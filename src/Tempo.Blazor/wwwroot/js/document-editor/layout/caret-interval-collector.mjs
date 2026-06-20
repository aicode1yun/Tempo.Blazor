// Phase D — layout/caret-interval-collector.mjs
// `collectLayoutLineIntervals(layout)` — walks a complete layout tree (body blocks,
// table cell sub-layouts, header/footer regions, plus pre-flattened `lineIntervals`)
// and returns the canonical caret-interval list via `normalizeCaretInterval`.
//
// `findCaretIntervalHit(layout, x, y)` — picks the first interval whose rect
// contains the point; null when no match.

import { asArray } from '../core/helpers.mjs';
import { normalizeCaretInterval } from './caret-interval.mjs';
import { hitRectContains } from './hit-rect.mjs';

export function collectLayoutLineIntervals(layout) {
    const result = [];
    function pushLayoutBlockLines(block, context) {
        const lineContext = context || {};
        asArray(block && block.lines).forEach(function (line) {
            const enrichedLine = Object.assign({}, lineContext, line || {}, {
                blockId: (line && line.blockId)
                    || (block && block.blockId)
                    || lineContext.blockId
                    || '',
            });
            const intervals = asArray(line && line.availableIntervals);
            if (!intervals.length) {
                result.push(normalizeCaretInterval(enrichedLine, null, 0));
                return;
            }
            intervals.forEach(function (interval, index) {
                result.push(normalizeCaretInterval(
                    enrichedLine,
                    Object.assign({}, lineContext, interval || {}),
                    index));
            });
        });
    }
    asArray(layout && (layout.lineIntervals || layout.LineIntervals))
        .forEach(function (interval, index) {
            result.push(normalizeCaretInterval({
                id: interval.lineId || interval.LineId
                    || interval.id || interval.Id || '',
                blockId: interval.blockId || interval.BlockId || '',
                start: interval.start ?? interval.Start
                    ?? interval.StartOffset ?? interval.startOffset,
                end: interval.end ?? interval.End
                    ?? interval.EndOffset ?? interval.endOffset,
                rect: interval.rect || interval.Rect || interval,
            }, interval, index));
        });
    asArray(layout && layout.blocks).forEach(function (block) {
        pushLayoutBlockLines(block, {});
        if (block && block.type === 'table') {
            asArray(block.cells).forEach(function (cell) {
                asArray(cell && cell.blockLayouts).forEach(function (cellBlock) {
                    pushLayoutBlockLines(cellBlock, {
                        region: 'TableCell',
                        headerFooterId: (cell && cell.headerFooterId) || null,
                        tableId: block.blockId || null,
                        cellId: (cell && cell.cellId) || null,
                        columnIndex: cell
                            ? (cell.columnIndex ?? cell.ColumnIndex ?? null)
                            : null,
                    });
                });
            });
        }
    });
    asArray(layout && layout.headerFooterRegions).forEach(function (regionLayout) {
        asArray(regionLayout && regionLayout.blocks).forEach(function (block) {
            pushLayoutBlockLines(block, {
                region: (regionLayout && regionLayout.region) || 'Body',
                headerFooterId: (regionLayout && regionLayout.headerFooterId) || null,
            });
        });
    });
    return result;
}

export function findCaretIntervalHit(layout, x, y) {
    let hit = null;
    collectLayoutLineIntervals(layout).some(function (interval) {
        if (!hitRectContains(interval.rect || interval, x, y)) return false;
        hit = interval;
        return true;
    });
    return hit;
}
