// Phase D — layout/exclusion-intervals.mjs
// Layout-side helpers that materialise the *available* and *blocked* intervals
// for a text line once exclusion geometry has been pre-computed.
//
// `normalizeManagerInterval(interval, atY, lineHeight, extra)` — canonical shape
//   for a manager interval ({x,y,width,height} plus any extra fields the caller
//   wants to carry through). All numeric fields are clamped at safe minimums.
// `mergeBlockedIntervalsForLayout(intervals, body, minWidth, atY, lineHeight)` —
//   clips intervals to the body, sorts left→right, merges adjacent (gap below
//   minWidth) and composes objectId/blockId via `|`-joined unique lists; mixed
//   wrapMode/wrapSide collapse to `'Mixed'`.
// `subtractBlockedIntervalsFromBody(body, atY, lineHeight, blockedIntervals,
//   minWidth)` — starting from the whole body row, subtracts each blocked range
//   using `subtractGeometryInterval`; returns the surviving intervals sorted by
//   x ascending and width descending.

import { asArray, asText, sortObject, unique } from '../core/helpers.mjs';
import { rectRightGeometry } from '../objects/geometry.mjs';
import {
    intervalEndGeometry,
    subtractGeometryInterval,
} from '../objects/overlap-geometry.mjs';

export function normalizeManagerInterval(interval, atY, lineHeight, extra) {
    const rawY = interval ? (interval.y ?? interval.Y) : atY;
    return sortObject(Object.assign({
        x: Number((interval && (interval.x ?? interval.X)) || 0) || 0,
        y: Number(rawY ?? atY) || 0,
        width: Math.max(0,
            Number((interval && (interval.width ?? interval.Width)) || 0) || 0),
        height: Math.max(1,
            Number((interval && (interval.height ?? interval.Height)) || lineHeight)
            || lineHeight),
    }, extra || {}));
}

export function mergeBlockedIntervalsForLayout(intervals, body, minWidth, atY, lineHeight) {
    const bodyLeft = Number((body && body.x) || 0);
    const bodyRight = rectRightGeometry(body);
    const sorted = asArray(intervals)
        .map(function (interval) {
            const left = Math.max(bodyLeft,
                Number((interval && (interval.x ?? interval.X)) || 0) || 0);
            const right = Math.min(bodyRight, intervalEndGeometry(interval));
            if (right <= left) return null;
            return normalizeManagerInterval(
                Object.assign({}, interval, {
                    x: left,
                    y: atY,
                    width: right - left,
                    height: lineHeight,
                }), atY, lineHeight);
        })
        .filter(Boolean)
        .filter(function (interval) { return Number(interval.width || 0) > 0; })
        .sort(function (a, b) { return Number(a.x || 0) - Number(b.x || 0); });
    if (sorted.length <= 1) return sorted;

    const merged = [sorted[0]];
    sorted.slice(1).forEach(function (current) {
        const previous = merged[merged.length - 1];
        const previousEnd = intervalEndGeometry(previous);
        const currentLeft = Number(current.x || 0);
        const gap = currentLeft - previousEnd;
        if (gap <= 0.0001 || gap < minWidth - 0.0001) {
            const right = Math.max(previousEnd, intervalEndGeometry(current));
            previous.width = right - Number(previous.x || 0);
            previous.objectId = unique(
                asText(previous.objectId || '').split('|')
                    .concat(asText(current.objectId || '').split('|'))
                    .filter(Boolean)).join('|');
            previous.blockId = unique(
                asText(previous.blockId || '').split('|')
                    .concat(asText(current.blockId || '').split('|'))
                    .filter(Boolean)).join('|');
            previous.wrapMode = previous.wrapMode === current.wrapMode
                ? previous.wrapMode
                : 'Mixed';
            previous.wrapSide = previous.wrapSide === current.wrapSide
                ? previous.wrapSide
                : 'Mixed';
        } else {
            merged.push(current);
        }
    });
    return merged;
}

export function subtractBlockedIntervalsFromBody(body, atY, lineHeight, blockedIntervals, minWidth) {
    let intervals = [{ x: body.x, y: atY, width: body.width, height: lineHeight }];
    asArray(blockedIntervals).forEach(function (blocked) {
        intervals = subtractGeometryInterval(
            intervals,
            Math.max(body.x, Number(blocked.x || 0)),
            Math.min(rectRightGeometry(body), intervalEndGeometry(blocked)),
            minWidth,
            atY,
            lineHeight);
    });
    return intervals
        .filter(function (interval) {
            return Number(interval.width || 0) >= minWidth - 0.0001;
        })
        .sort(function (a, b) {
            return Number(a.x || 0) - Number(b.x || 0)
                || Number(b.width || 0) - Number(a.width || 0);
        });
}
