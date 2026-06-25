// Phase D — layout/caret-rect.mjs
// `createCaretRectFromLayout({normalizeLogicalPosition})` factory →
//   `caretRectFromLayout(model, layout, position)` — looks up the caret rect for a
//   logical position by matching `layout.caretStops` on blockId + offset. The
//   position is first normalised via the injected `normalizeLogicalPosition` so that
//   region/affinity defaults are applied consistently. Returns a clone of the
//   matched stop's `rect`, or `null` when no caret stop matches.

import { asArray, clone } from '../core/helpers.mjs';

export function createCaretRectFromLayout(options) {
    const opts = options || {};
    if (typeof opts.normalizeLogicalPosition !== 'function') {
        throw new TypeError(
            'createCaretRectFromLayout requires options.normalizeLogicalPosition (function)');
    }
    const { normalizeLogicalPosition } = opts;

    return function caretRectFromLayout(model, layout, position) {
        const pos = normalizeLogicalPosition(model, position);
        const stop = asArray(layout && layout.caretStops).find(function (item) {
            return item.blockId === pos.blockId
                && Number(item.offset) === Number(pos.offset);
        });
        return stop ? clone(stop.rect) : null;
    };
}
