// Phase D — render/wysiwyg-object-visual-rect.mjs
// `createGetWysiwygObjectVisualRectRelativeTo({isWysiwygLayoutElementVisible,
//   getWysiwygRectRelativeTo, unionWysiwygRects})` →
//   `getWysiwygObjectVisualRectRelativeTo(item, bodyRect)` — the union rect (relative
//   to `bodyRect`) of an object layer item's visible image (or the item itself when
//   the image is hidden/absent) plus its caption when present. Used to compute the
//   real painted footprint of a figure for text-exclusion measurement. Returns null
//   when nothing measurable.

export function createGetWysiwygObjectVisualRectRelativeTo(options) {
    const opts = options || {};
    for (const key of ['isWysiwygLayoutElementVisible', 'getWysiwygRectRelativeTo',
        'unionWysiwygRects']) {
        if (typeof opts[key] !== 'function') {
            throw new TypeError(
                `createGetWysiwygObjectVisualRectRelativeTo requires options.${key} (function)`);
        }
    }
    const {
        isWysiwygLayoutElementVisible, getWysiwygRectRelativeTo, unionWysiwygRects,
    } = opts;

    return function getWysiwygObjectVisualRectRelativeTo(item, bodyRect) {
        const rects = [];
        const image = item && item.querySelector ? item.querySelector('img') : null;
        if (image && isWysiwygLayoutElementVisible(image)) {
            rects.push(getWysiwygRectRelativeTo(image.getBoundingClientRect(), bodyRect));
        } else if (item && typeof item.getBoundingClientRect === 'function') {
            rects.push(getWysiwygRectRelativeTo(item.getBoundingClientRect(), bodyRect));
        }
        const caption = item && item.querySelector ? item.querySelector('figcaption') : null;
        if (caption && isWysiwygLayoutElementVisible(caption)) {
            rects.push(getWysiwygRectRelativeTo(caption.getBoundingClientRect(), bodyRect));
        }
        return unionWysiwygRects(rects);
    };
}
