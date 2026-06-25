// Phase D — layout/nearest-text-position-line-box.mjs
// `createNormalizeNearestTextPositionLineBox(deps)` → `normalize(model, source, index)`
// — canonicalises a raw line-box hint (from the layout engine, DOM measurement, or
// a serialised drop-target payload) into the shape the nearest-text-position
// resolver expects: clamped start/end, normalised region/anchorRegion, rect,
// referenceRect, and per-interval offsets. Skips non-editable blocks and lines
// with a degenerate rect (returns null).
//
// Deps (all functions): `asArray`, `asText`, `sortObject`, `findBlock`,
// `isEditableTextBlock`, `blockText`, `rectFromGeometry`,
// `normalizeTextExclusionColumnIndex`, `normalizeDropRegionName`,
// `anchorRegionForNearestTextPosition`.

const REQUIRED = [
    'asArray', 'asText', 'sortObject', 'findBlock', 'isEditableTextBlock',
    'blockText', 'rectFromGeometry', 'normalizeTextExclusionColumnIndex',
    'normalizeDropRegionName', 'anchorRegionForNearestTextPosition',
];

export function createNormalizeNearestTextPositionLineBox(deps) {
    const opts = deps || {};
    for (const key of REQUIRED) {
        if (typeof opts[key] !== 'function') {
            throw new TypeError(
                `createNormalizeNearestTextPositionLineBox requires options.${key} (function)`);
        }
    }
    const {
        asArray, asText, sortObject, findBlock, isEditableTextBlock,
        blockText, rectFromGeometry, normalizeTextExclusionColumnIndex,
        normalizeDropRegionName, anchorRegionForNearestTextPosition,
    } = opts;

    return function normalizeNearestTextPositionLineBox(model, source, index) {
        const line = source || {};
        const blockId = asText(line.blockId || line.BlockId || '');
        const block = findBlock(model, blockId);
        if (!isEditableTextBlock(block)) return null;
        const textLength = blockText(block).length;
        const rect = rectFromGeometry(line.rect || line.Rect || line);
        const referenceRect = rectFromGeometry(
            line.referenceRect || line.ReferenceRect
            || line.blockRect || line.BlockRect || rect);
        if (rect.width <= 0 && rect.height <= 0) return null;
        const start = Math.max(0, Math.min(textLength,
            Number(line.start ?? line.Start
                ?? line.startOffset ?? line.StartOffset ?? 0) || 0));
        const end = Math.max(start, Math.min(textLength,
            Number(line.end ?? line.End
                ?? line.endOffset ?? line.EndOffset ?? textLength) || textLength));
        const cellId = asText(line.cellId || line.CellId || '');
        const columnIndex = normalizeTextExclusionColumnIndex(
            line.columnIndex ?? line.ColumnIndex);
        const region = normalizeDropRegionName(line.region || line.Region || 'Body', cellId);
        const pageIndex = line.pageIndex ?? line.PageIndex;
        return sortObject({
            id: asText(line.id || line.Id || line.lineId || line.LineId
                || ('drop-line-' + index)),
            blockId,
            lineId: asText(line.lineId || line.LineId || line.id || line.Id
                || ('drop-line-' + index)),
            pageIndex: pageIndex === null || pageIndex === undefined ? 0 : Number(pageIndex) || 0,
            region,
            anchorRegion: anchorRegionForNearestTextPosition({ region, cellId }),
            headerFooterId: asText(line.headerFooterId || line.HeaderFooterId || ''),
            tableId: asText(line.tableId || line.TableId || ''),
            cellId,
            columnIndex,
            rect,
            referenceRect,
            start,
            end,
            empty: line.empty === true || line.Empty === true
                || textLength === 0 || start === end,
            availableIntervals: asArray(
                line.availableIntervals || line.AvailableIntervals
                || line.ranges || line.Ranges).map(function (interval, intervalIndex) {
                const intervalStart = Math.max(start, Math.min(end,
                    Number((interval && (interval.start ?? interval.Start ?? start)) || start)
                    || start));
                const intervalEnd = Math.max(intervalStart, Math.min(end,
                    Number((interval && (interval.end ?? interval.End ?? intervalStart))
                        || intervalStart)
                    || intervalStart));
                return sortObject({
                    id: asText((interval && (interval.id || interval.Id))
                        || ('drop-interval-' + intervalIndex)),
                    x: Number((interval && (interval.x ?? interval.X ?? rect.x)) || rect.x)
                        || rect.x,
                    y: Number((interval && (interval.y ?? interval.Y ?? rect.y)) || rect.y)
                        || rect.y,
                    width: Math.max(0,
                        Number((interval && (interval.width ?? interval.Width ?? rect.width))
                            || rect.width) || 0),
                    height: Math.max(1,
                        Number((interval && (interval.height ?? interval.Height ?? rect.height))
                            || rect.height) || 1),
                    start: intervalStart,
                    end: intervalEnd,
                    collapsedOffset: interval
                        && (interval.collapsedOffset ?? interval.CollapsedOffset) !== undefined
                        ? Number(interval.collapsedOffset ?? interval.CollapsedOffset)
                            || intervalStart
                        : (intervalStart === intervalEnd ? intervalStart : null),
                    empty: (interval && (interval.empty === true || interval.Empty === true))
                        || intervalStart === intervalEnd,
                });
            }),
        });
    };
}
