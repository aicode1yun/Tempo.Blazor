// Phase D — layout/caret-interval.mjs
// `normalizeCaretInterval(line, interval, index)` — turns a raw line interval into
// the canonical caret interval shape used by the hit-test pipeline. Inherits rect/
// blockId/region/page metadata from the parent line when missing on the interval.
//
// The canonical shape includes start/end/collapsedOffset/affinity/virtualCaret/
// objectId fields plus the merged rect, so callers don't need to fall back to the
// line for every read.

import { asText, sortObject } from '../core/helpers.mjs';
import { hitRectFromAny } from './hit-rect.mjs';
import { finiteNumber } from './caret-math.mjs';
import { normalizeTextExclusionColumnIndex } from '../core/normalize-target.mjs';

export function normalizeCaretInterval(line, interval, index) {
    const sourceLine = line || {};
    const sourceInterval = interval || {};
    const lineRect = hitRectFromAny(sourceLine.rect || sourceLine.Rect);
    const intervalRect = hitRectFromAny({
        x: sourceInterval.x ?? sourceInterval.X ?? lineRect.x,
        y: sourceInterval.y ?? sourceInterval.Y ?? lineRect.y,
        width: sourceInterval.width ?? sourceInterval.Width ?? lineRect.width,
        height: sourceInterval.height ?? sourceInterval.Height ?? lineRect.height,
    });
    const lineStart = Math.max(0, finiteNumber(
        sourceLine.start ?? sourceLine.Start
        ?? sourceLine.StartOffset ?? sourceLine.startOffset,
        0));
    const lineEnd = Math.max(lineStart, finiteNumber(
        sourceLine.end ?? sourceLine.End
        ?? sourceLine.EndOffset ?? sourceLine.endOffset,
        lineStart));
    const hasExplicitStart = sourceInterval.start !== undefined
        || sourceInterval.Start !== undefined
        || sourceInterval.StartOffset !== undefined
        || sourceInterval.startOffset !== undefined;
    const hasExplicitEnd = sourceInterval.end !== undefined
        || sourceInterval.End !== undefined
        || sourceInterval.EndOffset !== undefined
        || sourceInterval.endOffset !== undefined;
    const start = Math.max(0, finiteNumber(
        sourceInterval.start ?? sourceInterval.Start
        ?? sourceInterval.StartOffset ?? sourceInterval.startOffset,
        index === 0 ? lineStart : lineEnd));
    const end = Math.max(start, finiteNumber(
        sourceInterval.end ?? sourceInterval.End
        ?? sourceInterval.EndOffset ?? sourceInterval.endOffset,
        hasExplicitStart && !hasExplicitEnd
            ? start
            : (index === 0 ? lineEnd : start)));
    const hasCollapsed = sourceInterval.collapsedOffset !== undefined
        || sourceInterval.CollapsedOffset !== undefined
        || sourceInterval.caretOffset !== undefined
        || sourceInterval.CaretOffset !== undefined;
    const collapsedOffset = hasCollapsed
        ? Math.max(0, finiteNumber(
            sourceInterval.collapsedOffset ?? sourceInterval.CollapsedOffset
            ?? sourceInterval.caretOffset ?? sourceInterval.CaretOffset,
            start))
        : (start === end ? start : null);
    const lineId = asText(
        sourceInterval.lineId || sourceInterval.LineId
        || sourceLine.lineId || sourceLine.LineId
        || sourceLine.id || sourceLine.Id
        || '');
    const affinity = sourceInterval.affinity === 'before'
        || sourceInterval.Affinity === 'before'
        ? 'before'
        : 'after';
    const virtualCaret = sourceInterval.virtualCaret === true
        || sourceInterval.VirtualCaret === true;
    return sortObject({
        id: asText(sourceInterval.id || sourceInterval.Id
            || (lineId ? lineId + '-interval-' + index : 'interval-' + index)),
        blockId: asText(sourceInterval.blockId || sourceInterval.BlockId
            || sourceLine.blockId || sourceLine.BlockId || ''),
        lineId: lineId,
        pageIndex: finiteNumber(
            sourceInterval.pageIndex ?? sourceInterval.PageIndex
            ?? sourceLine.pageIndex ?? sourceLine.PageIndex,
            0),
        region: sourceInterval.region || sourceInterval.Region
            || sourceLine.region || sourceLine.Region || 'Body',
        headerFooterId: sourceInterval.headerFooterId || sourceInterval.HeaderFooterId
            || sourceLine.headerFooterId || sourceLine.HeaderFooterId || null,
        tableId: sourceInterval.tableId || sourceInterval.TableId
            || sourceLine.tableId || sourceLine.TableId || null,
        cellId: sourceInterval.cellId || sourceInterval.CellId
            || sourceLine.cellId || sourceLine.CellId || null,
        columnIndex: normalizeTextExclusionColumnIndex(
            sourceInterval.columnIndex ?? sourceInterval.ColumnIndex
            ?? sourceLine.columnIndex ?? sourceLine.ColumnIndex),
        x: intervalRect.x,
        y: intervalRect.y,
        width: intervalRect.width,
        height: intervalRect.height,
        rect: intervalRect,
        start: start,
        end: end,
        collapsedOffset: collapsedOffset,
        empty: sourceInterval.empty === true
            || sourceInterval.Empty === true
            || start === end,
        affinity: affinity,
        virtualCaret: virtualCaret,
        objectId: sourceInterval.objectId || sourceInterval.ObjectId || null,
    });
}
