// Phase D — layout/blocked-intervals.mjs
// Pure helpers for computing the *blocked* horizontal intervals on a line caused
// by text-exclusion rects/polygons.
//
// `polygonIntervalsAtYGeometry(polygon, y)` — even-odd scan of a polygon at `y`,
//   returning the {x,width} intervals where the polygon covers the line.
// `mergeGeometryIntervals(intervals, minWidth)` — sorts + merges overlapping/touching
//   intervals; drops anything narrower than minWidth.
// `polygonBlockedIntervalsForGeometry(polygon, atY, lineHeight, body, minWidth)` —
//   samples three y values inside the line (plus polygon vertices that fall within
//   the band), clips against the body frame, returns merged intervals.
// `applyWrapSideToBlockedIntervals(intervals, wrapSide, body, minWidth)` — for
//   Left/Right/Largest wrap sides, replaces the per-object intervals with a single
//   half-body block so the body's preferred side becomes the only allowed side.
// `blockedIntervalsForExclusionGeometry(exclusion, atY, lineHeight, body, minWidth)`
//   — public entry: dispatches by `kind === 'fullWidth'` / `wrapMode === 'TopBottom'`,
//   then polygon, then plain rect. Result is wrap-side-respecting.

import { asArray, asText, unique } from '../core/helpers.mjs';
import {
    rectFromGeometry,
    rectRightGeometry,
    rectIntersectsGeometry,
} from '../objects/geometry.mjs';
import { normalizeWrapModeName, normalizeWrapSideName } from '../objects/wrap-modes.mjs';
import { intervalEndGeometry } from '../objects/overlap-geometry.mjs';

export function polygonIntervalsAtYGeometry(polygon, y) {
    const points = asArray(polygon);
    const xs = [];
    for (let i = 0; i < points.length; i++) {
        const a = points[i] || {};
        const b = points[(i + 1) % points.length] || {};
        const ay = Number(a.y ?? a.Y ?? 0) || 0;
        const by = Number(b.y ?? b.Y ?? 0) || 0;
        if (Math.abs(ay - by) < 0.0001) continue;
        const minY = Math.min(ay, by);
        const maxY = Math.max(ay, by);
        if (y < minY || y >= maxY) continue;
        const ax = Number(a.x ?? a.X ?? 0) || 0;
        const bx = Number(b.x ?? b.X ?? 0) || 0;
        xs.push(ax + ((y - ay) * (bx - ax) / (by - ay)));
    }
    xs.sort(function (left, right) { return left - right; });
    const intervals = [];
    for (let index = 0; index + 1 < xs.length; index += 2) {
        if (xs[index + 1] > xs[index] + 0.0001) {
            intervals.push({ x: xs[index], width: xs[index + 1] - xs[index] });
        }
    }
    return intervals;
}

export function mergeGeometryIntervals(intervals, minWidth) {
    const sorted = asArray(intervals)
        .filter(function (interval) {
            return Number(interval.width || 0) >= minWidth - 0.0001;
        })
        .sort(function (a, b) { return Number(a.x || 0) - Number(b.x || 0); });
    const result = [];
    sorted.forEach(function (interval) {
        const last = result[result.length - 1];
        if (last && Number(interval.x || 0) <= intervalEndGeometry(last) + 0.0001) {
            last.width = Math.max(intervalEndGeometry(last), intervalEndGeometry(interval))
                - Number(last.x || 0);
        } else {
            result.push({
                x: Number(interval.x || 0),
                width: Number(interval.width || 0),
            });
        }
    });
    return result;
}

export function polygonBlockedIntervalsForGeometry(polygon, atY, lineHeight, body, minWidth) {
    const top = atY;
    const bottom = atY + lineHeight;
    let samples = [top + 0.0001, top + lineHeight / 2, bottom - 0.0001]
        .map(function (sample) { return Math.max(top, Math.min(bottom, sample)); });
    asArray(polygon).forEach(function (point) {
        const y = Number((point && (point.y ?? point.Y)) || 0) || 0;
        if (y > top + 0.0001 && y < bottom - 0.0001) samples.push(y);
    });
    samples = unique(samples.map(function (sample) {
        return Math.round(sample * 10000) / 10000;
    }));
    const intervals = [];
    samples.forEach(function (sampleY) {
        polygonIntervalsAtYGeometry(polygon, sampleY).forEach(function (interval) {
            const left = Math.max(body.x, Number(interval.x || 0));
            const right = Math.min(rectRightGeometry(body), intervalEndGeometry(interval));
            if (right - left >= minWidth - 0.0001) {
                intervals.push({ x: left, width: right - left });
            }
        });
    });
    return mergeGeometryIntervals(intervals, minWidth);
}

export function applyWrapSideToBlockedIntervals(intervals, wrapSide, body, minWidth) {
    const side = normalizeWrapSideName(wrapSide);
    const list = asArray(intervals)
        .filter(function (interval) {
            return Number(interval.width || 0) >= minWidth - 0.0001;
        })
        .sort(function (a, b) { return Number(a.x || 0) - Number(b.x || 0); });
    if (side === 'BothSides' || list.length === 0) return list;

    const bodyLeft = Number((body && body.x) || 0);
    const bodyRight = rectRightGeometry(body);
    let left = Math.min.apply(null, list.map(function (interval) {
        return Number(interval.x || 0);
    }));
    let right = Math.max.apply(null, list.map(function (interval) {
        return intervalEndGeometry(interval);
    }));
    left = Math.max(bodyLeft, left);
    right = Math.min(bodyRight, right);
    if (right - left < minWidth - 0.0001) return [];

    let resolvedSide = side;
    if (side === 'Largest') {
        const leftSpace = Math.max(0, left - bodyLeft);
        const rightSpace = Math.max(0, bodyRight - right);
        resolvedSide = leftSpace >= rightSpace ? 'Left' : 'Right';
    }

    return resolvedSide === 'Left'
        ? [{ x: left, width: Math.max(0, bodyRight - left) }]
        : [{ x: bodyLeft, width: Math.max(0, right - bodyLeft) }];
}

export function blockedIntervalsForExclusionGeometry(exclusion, atY, lineHeight, body, minWidth) {
    const rect = rectFromGeometry(exclusion && (exclusion.rect || exclusion.Rect));
    const lineRect = { x: body.x, y: atY, width: body.width, height: lineHeight };
    const mode = normalizeWrapModeName(
        exclusion && (exclusion.wrapMode || exclusion.WrapMode));
    const sourceRect = mode === 'Tight'
        ? rectFromGeometry(exclusion && (exclusion.sourceRect || exclusion.SourceRect))
        : null;
    const intersectsRect = rectIntersectsGeometry(lineRect, rect);
    const intersectsSourceRect = !!(sourceRect
        && sourceRect.width > 0
        && sourceRect.height > 0
        && rectIntersectsGeometry(lineRect, sourceRect));
    if (!intersectsRect && !intersectsSourceRect) return [];
    const kind = asText(exclusion && (exclusion.kind || exclusion.Kind));
    if (mode === 'TopBottom' || kind === 'fullWidth') {
        return [{ x: body.x, width: body.width }];
    }

    let intervals = [];
    const polygon = asArray(exclusion && (exclusion.polygon || exclusion.Polygon));
    if (polygon.length >= 3) {
        intervals = polygonBlockedIntervalsForGeometry(polygon, atY, lineHeight, body, minWidth);
    } else {
        const left = Math.max(rect.x, body.x);
        const right = Math.min(rectRightGeometry(rect), rectRightGeometry(body));
        intervals = right - left >= minWidth - 0.0001
            ? [{ x: left, width: right - left }]
            : [];
    }
    if (mode === 'Tight' && intersectsSourceRect) {
        const sourceLeft = Math.max(body.x, sourceRect.x);
        const sourceRight = Math.min(rectRightGeometry(body), rectRightGeometry(sourceRect));
        if (sourceRight - sourceLeft > 0.0001) {
            intervals.push({ x: sourceLeft, width: sourceRight - sourceLeft });
        }
    }

    return applyWrapSideToBlockedIntervals(
        intervals, exclusion && (exclusion.wrapSide || exclusion.WrapSide), body, minWidth);
}
