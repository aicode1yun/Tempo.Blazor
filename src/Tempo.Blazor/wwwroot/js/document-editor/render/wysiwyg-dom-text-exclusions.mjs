// Phase D — render/wysiwyg-dom-text-exclusions.mjs
// `createCollectWysiwygDomTextExclusions({isWysiwygLayoutElementVisible,
//   normalizeWrapModeName, wrapModeCreatesTextExclusion,
//   getWysiwygObjectVisualRectRelativeTo, normalizeWrapSideName,
//   createTextExclusion})` →
//   `collectWysiwygDomTextExclusions(body, bodyRect, frame)` — scans the rendered
//   object layer for visible, non-inline floating objects whose wrap mode creates a
//   text exclusion, measures each one's visual rect from the live DOM, and builds a
//   text-exclusion record (carrying `anchorBlockId` + the source `objectElement`).
//   Returns the list of exclusions actually produced by `createTextExclusion`.

const REQUIRED = [
    'isWysiwygLayoutElementVisible', 'normalizeWrapModeName',
    'wrapModeCreatesTextExclusion', 'getWysiwygObjectVisualRectRelativeTo',
    'normalizeWrapSideName', 'createTextExclusion',
];

export function createCollectWysiwygDomTextExclusions(options) {
    const opts = options || {};
    for (const key of REQUIRED) {
        if (typeof opts[key] !== 'function') {
            throw new TypeError(
                `createCollectWysiwygDomTextExclusions requires options.${key} (function)`);
        }
    }
    const {
        isWysiwygLayoutElementVisible, normalizeWrapModeName,
        wrapModeCreatesTextExclusion, getWysiwygObjectVisualRectRelativeTo,
        normalizeWrapSideName, createTextExclusion,
    } = opts;

    return function collectWysiwygDomTextExclusions(body, bodyRect, frame) {
        const result = [];
        Array.from(body.querySelectorAll(
            '.tm-wysiwyg-page__layer--object .tm-wysiwyg-object-layer-item[data-object-id]'))
            .forEach(function (item) {
                if (!isWysiwygLayoutElementVisible(item)) return;
                if ((item.getAttribute('data-object-layer-kind') || '') === 'inline') return;
                const mode = normalizeWrapModeName(item.getAttribute('data-wrap-mode') || 'Inline');
                if (!wrapModeCreatesTextExclusion(mode)) return;
                const objectRect = getWysiwygObjectVisualRectRelativeTo(item, bodyRect);
                if (!objectRect || objectRect.width <= 0 || objectRect.height <= 0) return;
                const anchorBlockId = item.getAttribute('data-anchor-block-id')
                    || item.getAttribute('data-model-block-id')
                    || item.getAttribute('data-block-id') || '';
                const object = {
                    objectId: item.getAttribute('data-object-id') || '',
                    blockId: anchorBlockId,
                    anchorBlockId,
                    pageIndex: 0,
                    region: 'Body',
                    wrapMode: mode,
                    wrapSide: normalizeWrapSideName(item.getAttribute('data-wrap-side') || 'BothSides'),
                    rect: objectRect,
                    width: objectRect.width,
                    height: objectRect.height,
                    distanceLeft: Number(item.getAttribute('data-distance-left') || 0) || 0,
                    distanceRight: Number(item.getAttribute('data-distance-right') || 0) || 0,
                    distanceTop: Number(item.getAttribute('data-distance-top') || 0) || 0,
                    distanceBottom: Number(item.getAttribute('data-distance-bottom') || 0) || 0,
                    allowOverlap: false,
                };
                const exclusion = createTextExclusion(object, frame);
                if (!exclusion) return;
                exclusion.anchorBlockId = anchorBlockId;
                exclusion.objectElement = item;
                result.push(exclusion);
            });
        return result;
    };
}
