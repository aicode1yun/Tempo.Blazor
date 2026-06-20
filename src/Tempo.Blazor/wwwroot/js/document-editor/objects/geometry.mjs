// Phase D — objects/geometry.mjs
// Pure rectangle geometry helpers + wrap-contour normalization used by the floating
// object layout engine. All pure functions.

import { asArray } from '../core/helpers.mjs';

// Coerce any `{x,y,width,height}` shape (camel or Pascal, left/top aliases) to a clean
// rectangle. Width/height clamped to >= 0.
export function rectFromGeometry(value) {
    const rect = value || {};
    return {
        x: Number(rect.x ?? rect.X ?? rect.left ?? rect.Left ?? 0) || 0,
        y: Number(rect.y ?? rect.Y ?? rect.top ?? rect.Top ?? 0) || 0,
        width: Math.max(0, Number(rect.width ?? rect.Width ?? 0) || 0),
        height: Math.max(0, Number(rect.height ?? rect.Height ?? 0) || 0),
    };
}

export function rectRightGeometry(rect) {
    return Number((rect && rect.x) || 0) + Number((rect && rect.width) || 0);
}

export function rectBottomGeometry(rect) {
    return Number((rect && rect.y) || 0) + Number((rect && rect.height) || 0);
}

// True if rectangles `a` and `b` overlap (any non-zero intersection area).
export function rectIntersectsGeometry(a, b) {
    return Number(a.x || 0) < rectRightGeometry(b)
        && rectRightGeometry(a) > Number(b.x || 0)
        && Number(a.y || 0) < rectBottomGeometry(b)
        && rectBottomGeometry(a) > Number(b.y || 0);
}

// True if `a` and `b` overlap horizontally (ignoring y).
export function rectOverlapsHorizontallyGeometry(a, b) {
    return Number((a && a.x) || 0) < rectRightGeometry(b)
        && rectRightGeometry(a) > Number((b && b.x) || 0);
}

// Intersection of two rectangles, or null when they don't overlap.
export function intersectGeometryRect(a, b) {
    const left = Math.max(Number((a && a.x) || 0), Number((b && b.x) || 0));
    const top = Math.max(Number((a && a.y) || 0), Number((b && b.y) || 0));
    const right = Math.min(rectRightGeometry(a), rectRightGeometry(b));
    const bottom = Math.min(rectBottomGeometry(a), rectBottomGeometry(b));
    if (right <= left || bottom <= top) return null;
    return { x: left, y: top, width: right - left, height: bottom - top };
}

// Bounding box of a list of `{x, y}` points.
export function geometryBoundsOfPoints(points) {
    const list = asArray(points);
    if (!list.length) return { x: 0, y: 0, width: 0, height: 0 };
    const left = Math.min(...list.map(p => Number(p.x ?? p.X ?? 0) || 0));
    const top = Math.min(...list.map(p => Number(p.y ?? p.Y ?? 0) || 0));
    const right = Math.max(...list.map(p => Number(p.x ?? p.X ?? 0) || 0));
    const bottom = Math.max(...list.map(p => Number(p.y ?? p.Y ?? 0) || 0));
    return { x: left, y: top, width: right - left, height: bottom - top };
}

// Pascal-cased wrap contour normaliser used by the wire format (e.g. when sending
// updated points to C# or persisting them to JSON). No minimum-length padding,
// no dedup — exactly mirrors the input length and order.
export function normalizeWrapContourPoints(points) {
    return asArray(points).map(function (point) {
        const x = point ? (point.X ?? point.x ?? 0) : 0;
        const y = point ? (point.Y ?? point.y ?? 0) : 0;
        return {
            X: Math.max(0, Math.min(1, Number(x) || 0)),
            Y: Math.max(0, Math.min(1, Number(y) || 0)),
        };
    });
}

// Clamp + dedup wrap-contour points to the unit square. Returns a default 4-corner
// rectangle when fewer than 3 valid points are provided.
export function normalizeWrapContourPointsForGeometry(points) {
    const normalized = asArray(points).filter(Boolean).map(point => ({
        x: Math.max(0, Math.min(1, Number(point.x ?? point.X ?? 0) || 0)),
        y: Math.max(0, Math.min(1, Number(point.y ?? point.Y ?? 0) || 0)),
    }));
    if (normalized.length >= 3) return normalized;
    return [
        { x: 0, y: 0 },
        { x: 1, y: 0 },
        { x: 1, y: 1 },
        { x: 0, y: 1 },
    ];
}

// Read a distance with explicit > inferred precedence — direct lowerName/upperName
// win, otherwise fall back to `wrapMargin/WrapMargin` (clamped >= 0).
export function readObjectDistance(object, lowerName, upperName) {
    if (object && object[lowerName] !== undefined && object[lowerName] !== null) {
        return Number(object[lowerName]) || 0;
    }
    if (object && object[upperName] !== undefined && object[upperName] !== null) {
        return Number(object[upperName]) || 0;
    }
    return Math.max(0, Number((object && (object.wrapMargin ?? object.WrapMargin)) || 0) || 0);
}

// Build the footprint rect (image rect + caption tail) for layout collision tests.
export function createObjectFootprintRect(object, rect) {
    const captionHeight = (object && object.caption)
        ? Math.max(16, Math.min(48, object.caption.length * 0.6))
        : 0;
    return {
        x: rect.x,
        y: rect.y,
        width: rect.width,
        height: rect.height + captionHeight,
    };
}

// Wrap rect = footprint rect + distance{Left,Right,Top,Bottom} (or wrapMargin fallback).
export function createObjectWrapRect(object, rect) {
    const footprint = createObjectFootprintRect(object, rect);
    const left = readObjectDistance(object, 'distanceLeft', 'DistanceLeft');
    const right = readObjectDistance(object, 'distanceRight', 'DistanceRight');
    const top = readObjectDistance(object, 'distanceTop', 'DistanceTop');
    const bottom = readObjectDistance(object, 'distanceBottom', 'DistanceBottom');
    return {
        x: footprint.x - left,
        y: footprint.y - top,
        width: footprint.width + left + right,
        height: footprint.height + top + bottom,
    };
}

// Project unit-square contour points into a wrap rect, clamped to the body frame.
export function projectWrapContourPointsForGeometry(object, wrapRect, bodyFrame) {
    const source = rectFromGeometry(wrapRect);
    const body = bodyFrame ? rectFromGeometry(bodyFrame) : null;
    return normalizeWrapContourPointsForGeometry(
        object && (object.wrapContourPoints || object.WrapContourPoints)).map(point => {
        const x = source.x + source.width * point.x;
        const y = source.y + source.height * point.y;
        if (!body) return { x, y };
        return {
            x: Math.max(body.x, Math.min(rectRightGeometry(body), x)),
            y: Math.max(body.y, Math.min(rectBottomGeometry(body), y)),
        };
    });
}
