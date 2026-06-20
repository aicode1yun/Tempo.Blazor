// Phase D — render/heading-finder.mjs
// `findActiveHeadingBlockIdFromRects` — given the current viewport top and a list of
// heading rects sorted top-to-bottom, returns the id of the most recent heading that
// has already scrolled above the fold. Used by the outline/TOC sticky highlight.

import { asArray } from '../core/helpers.mjs';

export function findActiveHeadingBlockIdFromRects(rects, viewportTop) {
    let current = null;
    asArray(rects).forEach(function (rect) {
        if (Number(rect.top || 0) <= Number(viewportTop || 0)) {
            current = rect.id || rect.Id || current;
        }
    });
    return current;
}
