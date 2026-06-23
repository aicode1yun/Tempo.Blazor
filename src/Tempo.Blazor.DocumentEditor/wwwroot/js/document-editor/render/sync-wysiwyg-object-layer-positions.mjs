// Phase D — render/sync-wysiwyg-object-layer-positions.mjs
// `createSyncWysiwygObjectLayerPositions({resolveWysiwygObjectLayerRect,
//   applyWysiwygObjectLayerRect, cssEscape})` →
//   `syncWysiwygObjectLayerPositions(root)` — after a render, re-positions every
//   object layer item (behind-text / object / in-front-of-text layers) against
//   its live layer rect, then mirrors that rect onto the matching selection /
//   guides overlays so handles track the object. No-op when `root` lacks
//   `querySelectorAll`.

export function createSyncWysiwygObjectLayerPositions(options) {
    const opts = options || {};
    for (const key of [
        'resolveWysiwygObjectLayerRect',
        'applyWysiwygObjectLayerRect',
        'cssEscape',
    ]) {
        if (typeof opts[key] !== 'function') {
            throw new TypeError(
                `createSyncWysiwygObjectLayerPositions requires options.${key} (function)`);
        }
    }
    const {
        resolveWysiwygObjectLayerRect,
        applyWysiwygObjectLayerRect,
        cssEscape,
    } = opts;

    return function syncWysiwygObjectLayerPositions(root) {
        if (!root || typeof root.querySelectorAll !== 'function') return;
        Array.from(root.querySelectorAll('.tm-wysiwyg-page__body--layout'))
            .forEach(function (body) {
                const layers = Array.from(body.querySelectorAll(
                    '.tm-wysiwyg-page__layer--behind-text, '
                    + '.tm-wysiwyg-page__layer--object, '
                    + '.tm-wysiwyg-page__layer--in-front-of-text'));
                layers.forEach(function (layer) {
                    if (!layer || typeof layer.getBoundingClientRect !== 'function') return;
                    const layerRect = layer.getBoundingClientRect();
                    Array.from(layer.querySelectorAll(
                        '.tm-wysiwyg-object-layer-item[data-object-id]'))
                        .forEach(function (item) {
                            const objectId = item.getAttribute('data-object-id') || '';
                            if (!objectId) return;
                            const rect = resolveWysiwygObjectLayerRect(body, layerRect, item);
                            applyWysiwygObjectLayerRect(item, rect);
                            Array.from(body.querySelectorAll(
                                '.tm-wysiwyg-object-selection-overlay[data-object-id="'
                                + cssEscape(objectId) + '"], '
                                + '.tm-wysiwyg-object-guides-overlay[data-object-id="'
                                + cssEscape(objectId) + '"]'))
                                .forEach(function (overlay) {
                                    applyWysiwygObjectLayerRect(overlay, rect);
                                });
                        });
                });
            });
    };
}
