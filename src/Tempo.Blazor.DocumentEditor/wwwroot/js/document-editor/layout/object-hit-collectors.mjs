// Phase D — layout/object-hit-collectors.mjs
// `createObjectHitCollectors(deps)` → `{ collectLayoutObjectHits,
//   collectLayoutTextExclusionHits }`.
// Both walk a layout's `objects` plus any image blocks, build hit candidates at a
// point `(x, y)`, and sort by hit priority then z-index (descending).
//
// • collectLayoutObjectHits — tests against the object's visual rects; skips
//   non-selectable items and behind-text layers. Carries blockId/objectId/layer/
//   priority/zIndex/hitRect onto each candidate.
// • collectLayoutTextExclusionHits — tests against the object's text-exclusion rect
//   (via `objectTextExclusionRectForHitTest`); skips behind-text AND in-front-of-text
//   layers (those don't reflow text).

const REQUIRED = [
    'asArray', 'asText', 'drawingLayerForWrapMode', 'hitRectContains',
    'hitRectFromAny', 'objectHitPriority', 'finiteNumber',
    'objectTextExclusionRectForHitTest',
];

export function createObjectHitCollectors(deps) {
    const opts = deps || {};
    for (const key of REQUIRED) {
        if (typeof opts[key] !== 'function') {
            throw new TypeError(`createObjectHitCollectors requires options.${key} (function)`);
        }
    }
    const {
        asArray, asText, drawingLayerForWrapMode, hitRectContains,
        hitRectFromAny, objectHitPriority, finiteNumber,
        objectTextExclusionRectForHitTest,
    } = opts;

    function byPriorityThenZIndex(a, b) {
        return (finiteNumber(b.priority, 0) - finiteNumber(a.priority, 0))
            || (finiteNumber(b.zIndex, 0) - finiteNumber(a.zIndex, 0));
    }

    function walkImageBlocks(layout, pushObject) {
        asArray(layout && layout.objects).forEach(pushObject);
        asArray(layout && layout.blocks).forEach(function (block) {
            if (block && block.type === 'image') {
                pushObject(Object.assign({}, block, {
                    blockId: block.blockId || block.id || '',
                    objectId: block.objectId || block.ObjectId || block.blockId || block.id || '',
                    rect: block.rect || block.Rect,
                    Rect: block.Rect || block.rect,
                }));
            }
        });
    }

    function collectLayoutObjectHits(layout, x, y) {
        const candidates = [];
        function pushObject(item) {
            if (!item || item.Selectable === false || item.selectable === false) return;
            const layer = asText(item.Layer || item.layer
                || drawingLayerForWrapMode(item.WrapMode || item.wrapMode));
            if (layer.toLowerCase() === 'behind-text' || layer.toLowerCase() === 'behindtext') return;
            let visualRects = asArray(item.VisualRects || item.visualRects);
            if (!visualRects.length && (item.Rect || item.rect)) visualRects = [item.Rect || item.rect];
            const rect = visualRects.find(function (candidate) {
                return hitRectContains(candidate, x, y);
            });
            if (!rect) return;
            candidates.push(Object.assign({}, item, {
                blockId: item.blockId || item.BlockId || item.anchorBlockId || item.AnchorBlockId || '',
                objectId: item.objectId || item.ObjectId || item.id || item.Id || '',
                layer,
                priority: objectHitPriority(item),
                zIndex: finiteNumber(item.ZIndex ?? item.zIndex, 0),
                hitRect: hitRectFromAny(rect),
            }));
        }
        walkImageBlocks(layout, pushObject);
        return candidates.sort(byPriorityThenZIndex);
    }

    function collectLayoutTextExclusionHits(layout, x, y) {
        const candidates = [];
        function pushObject(item) {
            if (!item) return;
            const layer = asText(item.Layer || item.layer
                || drawingLayerForWrapMode(item.WrapMode || item.wrapMode)).toLowerCase();
            if (layer === 'behind-text' || layer === 'behindtext'
                || layer === 'in-front-of-text' || layer === 'infrontoftext') return;
            const rect = objectTextExclusionRectForHitTest(
                item,
                layout && (layout.frame || layout.Frame || layout.bodyFrame || layout.BodyFrame));
            if (!rect || !hitRectContains(rect, x, y)) return;
            candidates.push(Object.assign({}, item, {
                priority: objectHitPriority(item),
                zIndex: finiteNumber(item.ZIndex ?? item.zIndex, 0),
                hitRect: rect,
            }));
        }
        walkImageBlocks(layout, pushObject);
        return candidates.sort(byPriorityThenZIndex);
    }

    return Object.freeze({ collectLayoutObjectHits, collectLayoutTextExclusionHits });
}
