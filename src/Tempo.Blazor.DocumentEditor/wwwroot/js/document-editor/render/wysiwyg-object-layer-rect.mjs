// Phase D — render/wysiwyg-object-layer-rect.mjs
// `applyWysiwygObjectLayerRect(node, rect)` — writes a `{x,y,width,height}` rect to
//   a layer node's inline style (left/top rounded to 0.01px, width/height clamped
//   to ≥1) plus the `--tm-layout-object-width/height` CSS custom properties.
// `createResolveWysiwygObjectLayerRect({cssEscape})` →
//   `resolveWysiwygObjectLayerRect(body, layerRect, item)` — computes the on-page
//   rect (relative to the object layer) for a floating-object layer item by reading
//   its data-* attributes and the geometry of its anchor element / anchor block.
//   Handles inline objects (follow anchor rect), pre-resolved layout rects, page-
//   fixed objects, and Left/Right/Center horizontal alignment with offsets.

export function applyWysiwygObjectLayerRect(node, rect) {
    if (!node || !node.style || !rect) return;
    const width = Math.max(1, Number(rect.width || 0) || 1);
    const height = Math.max(1, Number(rect.height || 0) || 1);
    node.style.left = Math.round(Number(rect.x || 0) * 100) / 100 + 'px';
    node.style.top = Math.round(Number(rect.y || 0) * 100) / 100 + 'px';
    node.style.width = width + 'px';
    node.style.height = height + 'px';
    node.style.setProperty('--tm-layout-object-width', width + 'px');
    node.style.setProperty('--tm-layout-object-height', height + 'px');
}

export function createResolveWysiwygObjectLayerRect(options) {
    const opts = options || {};
    if (typeof opts.cssEscape !== 'function') {
        throw new TypeError(
            'createResolveWysiwygObjectLayerRect requires options.cssEscape (function)');
    }
    const { cssEscape } = opts;

    return function resolveWysiwygObjectLayerRect(body, layerRect, item) {
        const objectId = item.getAttribute('data-object-id') || '';
        const width = Math.max(1,
            Number(item.getAttribute('data-object-width') || item.style.width || 120) || 120);
        const height = Math.max(1,
            Number(item.getAttribute('data-object-height') || item.style.height || 80) || 80);
        const anchor = objectId
            && body.querySelector('[data-object-anchor-id="' + cssEscape(objectId) + '"]');
        const anchorBlockId = item.getAttribute('data-anchor-block-id')
            || item.getAttribute('data-block-id') || '';
        const block = anchorBlockId
            ? body.querySelector('.tm-wysiwyg-block[data-block-id="' + cssEscape(anchorBlockId) + '"]')
            : null;
        const anchorBlock = anchor && anchor.closest
            ? anchor.closest('.tm-wysiwyg-block[data-block-id]')
            : null;
        const anchorOwnsRequestedBlock = !anchorBlockId
            || !anchorBlock
            || (anchorBlock.getAttribute('data-block-id') || '') === anchorBlockId;
        const kind = item.getAttribute('data-object-layer-kind') || 'anchored';
        if (kind === 'inline' && anchor && typeof anchor.getBoundingClientRect === 'function') {
            const anchorRect = anchor.getBoundingClientRect();
            return {
                x: anchorRect.left - layerRect.left,
                y: anchorRect.top - layerRect.top,
                width: Math.max(width, anchorRect.width || 0),
                height,
            };
        }
        if (kind !== 'inline'
            && item.getAttribute('data-object-position-source') === 'layout-rect') {
            return {
                x: Number(item.getAttribute('data-object-x') || 0) || 0,
                y: Number(item.getAttribute('data-object-y') || 0) || 0,
                width,
                height,
            };
        }

        const fixedOnPage = item.getAttribute('data-fixed-on-page') === 'true';
        const reference = fixedOnPage
            ? layerRect
            : anchor && anchorOwnsRequestedBlock && typeof anchor.getBoundingClientRect === 'function'
                ? anchor.getBoundingClientRect()
                : block && typeof block.getBoundingClientRect === 'function'
                    ? block.getBoundingClientRect()
                    : layerRect;
        const blockRect = fixedOnPage
            ? layerRect
            : block && typeof block.getBoundingClientRect === 'function'
                ? block.getBoundingClientRect()
                : reference;
        const align = String(item.getAttribute('data-horizontal-align') || 'Left').toLowerCase();
        const offsetX = Number(item.getAttribute('data-horizontal-offset') || 0) || 0;
        const offsetY = Number(item.getAttribute('data-vertical-offset') || 0) || 0;
        let left = (blockRect.left || reference.left || layerRect.left) - layerRect.left + offsetX;
        if (align === 'right' || align === 'end') {
            left = (blockRect.right || (blockRect.left + blockRect.width))
                - layerRect.left - width - offsetX;
        } else if (align === 'center' || align === 'middle') {
            left = (blockRect.left || reference.left || layerRect.left)
                - layerRect.left
                + Math.max(0, ((blockRect.width || width) - width) / 2)
                + offsetX;
        }
        return {
            x: Math.max(0, left),
            y: Math.max(0, (reference.top || blockRect.top || layerRect.top)
                - layerRect.top + offsetY),
            width,
            height,
        };
    };
}
