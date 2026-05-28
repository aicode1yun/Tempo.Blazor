// Phase D — layout/paragraph-layout-options.mjs
// `normalizeParagraphLayoutOptions(options)` — coerces caller-supplied layout
// options into the canonical shape used by the paragraph layout engine:
//   { page, x, y, width, minReadableWidth, lineGap, availableIntervals,
//     resolveAvailableIntervals }
// • `page` falls through `normalizePageBox`.
// • `x` / `y` fall back to the page's x/y when missing or zero-coerced.
// • `width` is clamped to ≥ 1, defaulting to the page width.
// • `minReadableWidth` is clamped to ≥ 1, defaulting to 48.
// • `lineGap` defaults to 0.
// • `availableIntervals` becomes a normalised array (Pascal/Camel accepted).
// • `resolveAvailableIntervals` keeps the caller's function reference if present.

import { asArray } from '../core/helpers.mjs';
import { normalizePageBox } from './page-metrics.mjs';

export function normalizeParagraphLayoutOptions(options) {
    const opts = options || {};
    const page = normalizePageBox(opts);
    return {
        page,
        x: Number(opts.x || opts.X || page.x) || page.x,
        y: Number(opts.y || opts.Y || page.y) || page.y,
        width: Math.max(1, Number(opts.width || opts.Width || page.width) || page.width),
        minReadableWidth: Math.max(1,
            Number(opts.minReadableWidth || opts.MinReadableWidth || 48) || 48),
        lineGap: Number(opts.lineGap || opts.LineGap || 0) || 0,
        availableIntervals: asArray(opts.availableIntervals || opts.AvailableIntervals),
        resolveAvailableIntervals: typeof opts.resolveAvailableIntervals === 'function'
            ? opts.resolveAvailableIntervals
            : (typeof opts.ResolveAvailableIntervals === 'function'
                ? opts.ResolveAvailableIntervals
                : null),
    };
}
