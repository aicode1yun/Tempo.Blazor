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
        const rectRelativeTo = (rect, origin) => ({
            x: Number((rect && (rect.left ?? rect.x)) || 0) - Number((origin && (origin.left ?? origin.x)) || 0),
            y: Number((rect && (rect.top ?? rect.y)) || 0) - Number((origin && (origin.top ?? origin.y)) || 0),
            width: Math.max(0, Number((rect && rect.width) || 0) || 0),
            height: Math.max(0, Number((rect && rect.height) || 0) || 0),
        });
        const visibleChildRect = (item, selector) => {
            const child = item && typeof item.querySelector === 'function'
                ? item.querySelector(selector)
                : null;
            if (!child || !isWysiwygLayoutElementVisible(child)
                || typeof child.getBoundingClientRect !== 'function') {
                return null;
            }

            return rectRelativeTo(child.getBoundingClientRect(), bodyRect);
        };
        const readWrapContourPoints = item => {
            const raw = item.getAttribute('data-wrap-contour-points') || '';
            if (!raw) return [];
            try {
                return JSON.parse(raw);
            } catch (_) {
                return [];
            }
        };
        Array.from(body.querySelectorAll(
            '.tm-wysiwyg-page__layer--object .tm-wysiwyg-object-layer-item[data-object-id]'))
            .forEach(function (item) {
                if (!isWysiwygLayoutElementVisible(item)) return;
                if ((item.getAttribute('data-object-layer-kind') || '') === 'inline') return;
                const mode = normalizeWrapModeName(item.getAttribute('data-wrap-mode') || 'Inline');
                if (!wrapModeCreatesTextExclusion(mode)) return;
                const objectRect = visibleChildRect(item, 'img')
                    || getWysiwygObjectVisualRectRelativeTo(item, bodyRect);
                if (!objectRect || objectRect.width <= 0 || objectRect.height <= 0) return;
                const anchorBlockId = item.getAttribute('data-anchor-block-id')
                    || item.getAttribute('data-model-block-id')
                    || item.getAttribute('data-block-id') || '';
                const objectId = item.getAttribute('data-object-id') || '';
                const wrapSide = normalizeWrapSideName(item.getAttribute('data-wrap-side') || 'BothSides');
                const distanceLeft = Number(item.getAttribute('data-distance-left') || 0) || 0;
                const distanceRight = Number(item.getAttribute('data-distance-right') || 0) || 0;
                const distanceTop = Number(item.getAttribute('data-distance-top') || 0) || 0;
                const distanceBottom = Number(item.getAttribute('data-distance-bottom') || 0) || 0;
                const object = {
                    objectId,
                    blockId: anchorBlockId,
                    anchorBlockId,
                    pageIndex: 0,
                    region: 'Body',
                    wrapMode: mode,
                    wrapSide,
                    rect: objectRect,
                    width: objectRect.width,
                    height: objectRect.height,
                    wrapContourPoints: readWrapContourPoints(item),
                    distanceLeft,
                    distanceRight,
                    distanceTop,
                    distanceBottom,
                    allowOverlap: false,
                };
                const exclusion = createTextExclusion(object, frame);
                if (!exclusion) return;
                exclusion.anchorBlockId = anchorBlockId;
                exclusion.objectElement = item;
                result.push(exclusion);
                const captionRect = visibleChildRect(item, 'figcaption');
                if (captionRect && captionRect.width > 0 && captionRect.height > 0) {
                    const captionExclusion = createTextExclusion({
                        objectId: objectId ? `${objectId}:caption` : `${anchorBlockId}:caption`,
                        blockId: anchorBlockId,
                        anchorBlockId,
                        pageIndex: 0,
                        region: 'Body',
                        wrapMode: 'Square',
                        wrapSide,
                        rect: captionRect,
                        width: captionRect.width,
                        height: captionRect.height,
                        distanceLeft,
                        distanceRight,
                        distanceTop: 0,
                        distanceBottom,
                        allowOverlap: false,
                    }, frame);
                    if (captionExclusion) {
                        captionExclusion.anchorBlockId = anchorBlockId;
                        captionExclusion.objectElement = item;
                        captionExclusion.captionElement = item.querySelector('figcaption');
                        result.push(captionExclusion);
                    }
                }
            });
        return result;
    };
}
