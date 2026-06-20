// Phase D — layout/line-breaker-helpers.mjs
// Pure-function support set for the line breaker so the breaker factory in
// `line-breaker.mjs` can be unit-tested with real helpers instead of stubs.
//
// All functions here are deterministic, framework-free, and DOM-free.

import { asArray, asText, sortObject } from '../core/helpers.mjs';

export function normalizeLineBreakerOptions(options) {
    const opts = options || {};
    return {
        x: Number(opts.x || opts.X || 0) || 0,
        y: Number(opts.y || opts.Y || 0) || 0,
        width: Number(opts.width || opts.Width || 0) || 0,
        lineGap: Number(opts.lineGap || opts.LineGap || 0) || 0,
        minReadableWidth: Math.max(1, Number(opts.minReadableWidth || opts.MinReadableWidth || 48) || 48),
        availableIntervals: asArray(opts.availableIntervals || opts.AvailableIntervals),
        resolveAvailableIntervals: typeof opts.resolveAvailableIntervals === 'function'
            ? opts.resolveAvailableIntervals
            : (typeof opts.ResolveAvailableIntervals === 'function' ? opts.ResolveAvailableIntervals : null),
    };
}

export function normalizeLineRanges(opts, y) {
    const intervals = opts.availableIntervals.length
        ? opts.availableIntervals
        : [{ x: opts.x, y: y, width: opts.width }];
    const ranges = asArray(intervals).map(function (raw, index) {
        raw = raw || {};
        const x = Number(raw.x ?? raw.X ?? opts.x ?? 0) || 0;
        const width = Number(raw.width ?? raw.Width ?? opts.width ?? 0) || 0;
        const rangeY = raw.y !== undefined || raw.Y !== undefined
            ? Number(raw.y ?? raw.Y ?? y ?? opts.y ?? 0) || 0
            : y;
        const height = Number(raw.height ?? raw.Height ?? 0) || 0;
        return {
            id: raw.id || raw.Id || ('range-' + index),
            index: index,
            interval: { x: x, y: rangeY, width: width, height: height },
            x: x,
            y: rangeY,
            width: width,
            height: height,
            pageIndex: raw.pageIndex ?? raw.PageIndex ?? null,
            region: raw.region || raw.Region || null,
            headerFooterId: raw.headerFooterId || raw.HeaderFooterId || null,
            tableId: raw.tableId || raw.TableId || null,
            cellId: raw.cellId || raw.CellId || null,
            usedWidth: 0,
            start: null,
            end: 0,
            segments: [],
            caretStops: [],
        };
    }).filter(function (range) {
        return Number.isFinite(range.x) && Number.isFinite(range.width) && range.width > 0;
    }).sort(function (a, b) {
        return a.x - b.x || a.index - b.index;
    });
    return ranges.length ? ranges : [{
        id: 'range-0', index: 0,
        interval: { x: opts.x, y: y, width: opts.width, height: 0 },
        x: opts.x, y: y, width: opts.width, height: 0,
        pageIndex: null, region: null, headerFooterId: null, tableId: null, cellId: null,
        usedWidth: 0, start: null, end: 0, segments: [], caretStops: [],
    }];
}

export function resolveLineRangesForBreaker(opts, y, lineHeight) {
    if (typeof opts.resolveAvailableIntervals !== 'function') {
        return normalizeLineRanges(opts, y);
    }
    const resolved = opts.resolveAvailableIntervals(
        y, Math.max(1, Number(lineHeight || 18) || 18), opts.minReadableWidth);
    if (!resolved) return normalizeLineRanges(opts, y);
    const moved = resolved.moved === true || resolved.Moved === true;
    const movedToY = Number(resolved.movedToY ?? resolved.MovedToY ?? y) || y;
    const lineY = moved && movedToY > y ? movedToY : y;
    let intervals = asArray((moved && (resolved.movedIntervals || resolved.MovedIntervals))
        || resolved.intervals
        || resolved.availableIntervals
        || resolved.AvailableIntervals
        || resolved);
    if (!intervals.length && moved && asArray(
        resolved.intervals || resolved.availableIntervals || resolved.AvailableIntervals).length) {
        intervals = asArray(resolved.intervals || resolved.availableIntervals || resolved.AvailableIntervals);
    }
    return normalizeLineRanges(Object.assign({}, opts, { availableIntervals: intervals }), lineY);
}

export function isInvalidInterval(interval, minReadableWidth) {
    return !interval
        || !Number.isFinite(interval.x)
        || !Number.isFinite(interval.width)
        || interval.width < minReadableWidth;
}

export function lineRangesAreInvalid(ranges, minReadableWidth) {
    return !asArray(ranges).some(function (range) {
        return !isInvalidInterval(range && range.interval, minReadableWidth);
    });
}

export function coalesceNonBreakingTokens(tokens) {
    const result = [];
    const list = asArray(tokens);
    for (let i = 0; i < list.length; i++) {
        const token = list[i];
        if (!token || token.type === 'newline' || token.type === 'space' || token.type === 'tab') {
            result.push(token);
            continue;
        }
        if (list[i + 1] && list[i + 1].type === 'nbsp') {
            const group = [token];
            let j = i + 1;
            while (j < list.length) {
                group.push(list[j]);
                if (list[j].type !== 'nbsp' && !(list[j + 1] && list[j + 1].type === 'nbsp')) break;
                j++;
            }
            const text = group.map(function (item) { return asText(item && item.text); }).join('');
            const first = group[0] || token;
            const last = group[group.length - 1] || token;
            result.push(sortObject(Object.assign({}, first, {
                type: 'nbspSequence',
                text: text,
                start: first.start,
                end: last.end,
                length: Math.max(0, Number(last.end || 0) - Number(first.start || 0)),
                unbreakable: true,
            })));
            i = j;
            continue;
        }
        result.push(token);
    }
    return result;
}

export function splitTokenIntoFittingPieces(token, text, style, service, availableWidth) {
    const pieces = [];
    const source = Array.from(asText(text));
    let cursor = token.start;
    let buffer = '';
    let bufferStart = cursor;
    for (let i = 0; i < source.length; i++) {
        const next = buffer + source[i];
        const nextWidth = service.measureText(next, style).width;
        if (buffer && nextWidth > availableWidth) {
            pieces.push({
                text: buffer,
                start: bufferStart,
                end: cursor,
                width: service.measureText(buffer, style).width,
            });
            buffer = source[i];
            bufferStart = cursor;
        } else {
            buffer = next;
        }
        cursor += source[i].length;
    }
    if (buffer) {
        pieces.push({
            text: buffer,
            start: bufferStart,
            end: cursor,
            width: service.measureText(buffer, style).width,
        });
    }
    return pieces.length ? pieces : [{
        text: asText(text), start: token.start, end: token.end,
        width: Math.min(availableWidth, service.measureText(text, style).width),
    }];
}

export function applyJustifyMetadata(lines, alignment) {
    const justify = alignment === 'justify' || alignment === 'justified' || alignment === 'block';
    lines.forEach(function (line, index) {
        const isLast = index === lines.length - 1;
        let totalGaps = 0;
        let anyEnabled = false;
        let maxExtraSpacePerGap = 0;
        const rangeJustify = asArray(line.ranges && line.ranges.length
            ? line.ranges
            : line.availableIntervals).map(function (range, rangeIndex) {
                let rangeSegments = asArray(range && range.segments).filter(function (segment) {
                    return Number(segment.rangeIndex || 0) === Number(range.index ?? rangeIndex);
                });
                if (!rangeSegments.length && line.segments && (!line.ranges || line.ranges.length <= 1)) {
                    rangeSegments = asArray(line.segments);
                }
                const gaps = rangeSegments.filter(function (segment) { return segment.type === 'space'; }).length;
                totalGaps += gaps;
                let usedWidth = Number(range && (range.usedWidth ?? range.textWidth) || 0) || 0;
                if (!usedWidth) {
                    usedWidth = rangeSegments.reduce(function (sum, segment) {
                        return sum + Math.max(0,
                            Number(segment && segment.rect && segment.rect.width || 0) || 0);
                    }, 0);
                }
                const remaining = Math.max(0, Number(range && range.width || 0) - usedWidth);
                const enabled = justify && !isLast && !line.hardBreak && gaps > 0 && remaining > 0;
                if (enabled) anyEnabled = true;
                if (enabled) maxExtraSpacePerGap = Math.max(maxExtraSpacePerGap, remaining / gaps);
                return {
                    enabled: enabled,
                    extraSpacePerGap: enabled ? remaining / gaps : 0,
                    gapCount: gaps,
                    rangeIndex: (range && (range.index ?? rangeIndex)) ?? rangeIndex,
                    remainingWidth: remaining,
                };
            });
        line.justify = {
            enabled: anyEnabled,
            extraSpacePerGap: anyEnabled ? maxExtraSpacePerGap : 0,
            gapCount: totalGaps,
            ranges: rangeJustify,
        };
        asArray(line.ranges).forEach(function (range, rangeIndex) {
            range.justify = rangeJustify[rangeIndex] || {
                enabled: false, extraSpacePerGap: 0, gapCount: 0,
                rangeIndex: rangeIndex, remainingWidth: 0,
            };
        });
    });
}
