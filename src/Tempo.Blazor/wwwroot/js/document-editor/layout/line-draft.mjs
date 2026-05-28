// Phase D — layout/line-draft.mjs
// `createLineDraft` + `materializeLineDraft` — pure factories used by the line
// breaker. `createLineDraft` builds the mutable scaffold (segments accumulate into
// it as tokens are placed). `materializeLineDraft` freezes the draft into a final
// line shape with computed visual rect, range shifts and segment heights.

import { asArray, sortObject } from '../core/helpers.mjs';

export function createLineDraft(index, ranges, y) {
    const rawList = asArray(ranges).length ? asArray(ranges) : [{ x: 0, y: y, width: 0, height: 0 }];
    const normalizedRanges = rawList.map(function (range, rangeIndex) {
        const x = Number(range && range.x || 0) || 0;
        const rangeY = range && range.y !== null && range.y !== undefined
            ? Number(range.y || 0) || 0
            : y;
        const width = Math.max(0, Number(range && range.width || 0) || 0);
        const height = Math.max(0, Number(range && range.height || 0) || 0);
        return {
            id: range && range.id || ('range-' + rangeIndex),
            index: rangeIndex,
            interval: { x: x, y: rangeY, width: width, height: height },
            x: x,
            y: rangeY,
            width: width,
            height: height,
            pageIndex: (range && (range.pageIndex ?? range.PageIndex)) ?? null,
            region: range && (range.region || range.Region) || null,
            headerFooterId: range && (range.headerFooterId || range.HeaderFooterId) || null,
            tableId: range && (range.tableId || range.TableId) || null,
            cellId: range && (range.cellId || range.CellId) || null,
            usedWidth: 0,
            start: null,
            end: 0,
            segments: [],
            caretStops: [],
        };
    });
    const visualLeft = Math.min.apply(null, normalizedRanges.map(function (range) {
        return Number(range.x || 0);
    }));
    const visualRight = Math.max.apply(null, normalizedRanges.map(function (range) {
        return Number(range.x || 0) + Number(range.width || 0);
    }));
    const lineY = normalizedRanges[0] && normalizedRanges[0].y !== null && normalizedRanges[0].y !== undefined
        ? normalizedRanges[0].y
        : y;
    return {
        id: 'line-' + index,
        index: index,
        ranges: normalizedRanges,
        rangeIndex: 0,
        interval: normalizedRanges[0] && normalizedRanges[0].interval || normalizedRanges[0],
        y: lineY,
        start: null,
        end: 0,
        width: 0,
        visualLeft: visualLeft,
        visualRight: visualRight,
        lineHeight: 18,
        segments: [],
        invalid: false,
        movedAcrossRange: false,
    };
}

export function materializeLineDraft(draft, index, hardBreak, alignment) {
    const height = Math.max(1, draft.lineHeight);
    const start = draft.start === null ? draft.end : draft.start;
    const lineTop = Number(draft.y || 0) || 0;
    const ranges = asArray(draft.ranges).map(function (range, rangeIndex) {
        const rangeStart = range.start === null ? draft.end : range.start;
        const rangeEnd = Math.max(rangeStart, Number(range.end || rangeStart) || rangeStart);
        return sortObject({
            id: range.id || ('range-' + rangeIndex),
            index: rangeIndex,
            x: Number(range.x || 0) || 0,
            y: lineTop,
            width: Math.max(0, Number(range.width || 0) || 0),
            height: height,
            pageIndex: range.pageIndex ?? null,
            region: range.region || null,
            headerFooterId: range.headerFooterId || null,
            tableId: range.tableId || null,
            cellId: range.cellId || null,
            usedWidth: Math.max(0, Number(range.usedWidth || 0) || 0),
            start: rangeStart,
            end: rangeEnd,
            collapsedOffset: rangeStart === rangeEnd ? rangeStart : null,
            empty: !range.segments || range.segments.length === 0,
            segments: asArray(range.segments),
        });
    });
    const visualLeft = ranges.length
        ? Math.min.apply(null, ranges.map(function (range) { return range.x; }))
        : Number(draft.visualLeft || 0) || 0;
    const visualRight = ranges.length
        ? Math.max.apply(null, ranges.map(function (range) { return range.x + range.width; }))
        : visualLeft + Math.max(0, draft.width);
    const lineWidth = ranges.length > 1
        ? Math.max(0, visualRight - visualLeft)
        : Math.max(0, Number(ranges[0] && ranges[0].usedWidth || draft.width || 0) || 0);
    const rangeShifts = {};
    ranges.forEach(function (range) {
        const remaining = Math.max(0, Number(range.width || 0) - Number(range.usedWidth || 0));
        const shift = alignment === 'right'
            ? remaining
            : (alignment === 'center' ? remaining / 2 : 0);
        range.alignmentShift = shift;
        rangeShifts[range.index] = shift;
        if (Math.abs(shift) > 0.0001) {
            asArray(range.segments).forEach(function (segment) {
                if (segment && segment.rect) segment.rect.x += shift;
                if (segment && segment.objectRect) segment.objectRect.x += shift;
            });
        }
    });
    return {
        id: draft.id,
        index: index,
        pageIndex: ranges.length ? ranges[0].pageIndex ?? null : null,
        start: start,
        end: draft.end,
        hardBreak: hardBreak === true,
        rect: {
            x: ranges.length > 1 ? visualLeft : Number(ranges[0] && ranges[0].x || 0),
            y: lineTop,
            width: lineWidth,
            height: height,
        },
        visualRect: {
            x: visualLeft,
            y: lineTop,
            width: Math.max(0, visualRight - visualLeft),
            height: height,
        },
        ranges: ranges,
        textRanges: ranges,
        rangeShifts: rangeShifts,
        availableIntervals: ranges.map(function (range) {
            return sortObject({
                id: range.id,
                x: range.x,
                y: range.y,
                width: range.width,
                height: range.height,
                pageIndex: range.pageIndex ?? null,
                region: range.region || null,
                headerFooterId: range.headerFooterId || null,
                tableId: range.tableId || null,
                cellId: range.cellId || null,
                start: range.start,
                end: range.end,
                collapsedOffset: range.collapsedOffset,
                empty: range.empty,
            });
        }),
        segments: draft.segments.map(function (segment) {
            segment.rect.height = height;
            if (segment.type === 'inlineObject' && segment.objectRect) {
                const objectHeight = Math.max(1,
                    Number(segment.objectRect.height || segment.rect.height || height) || height);
                segment.objectRect.y = segment.rect.y + Math.max(0, height - objectHeight);
                segment.objectRect.height = objectHeight;
            }
            return segment;
        }),
        justify: { enabled: false, extraSpacePerGap: 0, gapCount: 0 },
    };
}
