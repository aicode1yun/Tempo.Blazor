// Phase D — layout/hit-rect.mjs
// `hitRectFromAny(rect)` / `hitRectContains(rect, x, y)` — companion to
// `render/rect-helpers.mjs` but in the layout-coordinate space (camel-case shape
// `{x, y, width, height}` instead of the Pascal-cased event/DOM shape). Used by
// the hit-test pipeline for caret-interval lookups + drawing-object hit detection.

import { finiteNumber } from './caret-math.mjs';

export function hitRectFromAny(rect) {
    const source = rect || {};
    return {
        x: finiteNumber(source.x ?? source.X ?? source.left ?? source.Left, 0),
        y: finiteNumber(source.y ?? source.Y ?? source.top ?? source.Top, 0),
        width: Math.max(0, finiteNumber(source.width ?? source.Width, 0)),
        height: Math.max(0, finiteNumber(source.height ?? source.Height, 0)),
    };
}

export function hitRectContains(rect, x, y) {
    const r = hitRectFromAny(rect);
    return x >= r.x && x <= r.x + r.width && y >= r.y && y <= r.y + r.height;
}
