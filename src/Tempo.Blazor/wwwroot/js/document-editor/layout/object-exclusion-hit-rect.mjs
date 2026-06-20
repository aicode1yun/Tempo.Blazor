// Phase D — layout/object-exclusion-hit-rect.mjs
// `objectTextExclusionRectForHitTest(item, frame)` — computes the rect that a
// floating object reserves for hit-testing against text. Returns null when the
// object's wrap mode does not create an exclusion or when the object rect is
// degenerate. An explicit wrap/exclusion rect on the item wins when present and
// non-degenerate. Otherwise the rect grows by the per-side distances (defaulting
// to `wrapMargin`); `TopBottom` mode stretches to the full body frame width.
//
// Pure — but needs `normalizeWrapModeName`, `wrapModeCreatesTextExclusion`,
// `hitRectFromAny`, and `finiteNumber` injected via the factory so it stays
// decoupled from the objects/layout module graph.

export function createObjectTextExclusionRectForHitTest(options) {
    const opts = options || {};
    for (const key of ['normalizeWrapModeName', 'wrapModeCreatesTextExclusion',
        'hitRectFromAny', 'finiteNumber']) {
        if (typeof opts[key] !== 'function') {
            throw new TypeError(
                `createObjectTextExclusionRectForHitTest requires options.${key} (function)`);
        }
    }
    const {
        normalizeWrapModeName,
        wrapModeCreatesTextExclusion,
        hitRectFromAny,
        finiteNumber,
    } = opts;

    return function objectTextExclusionRectForHitTest(item, frame) {
        const mode = normalizeWrapModeName(item && (item.WrapMode || item.wrapMode));
        if (!wrapModeCreatesTextExclusion(mode)) return null;
        const explicitRect = item && (item.WrapRect || item.wrapRect
            || item.TextExclusionRect || item.textExclusionRect
            || item.ExclusionRect || item.exclusionRect);
        if (explicitRect) {
            const normalizedExplicit = hitRectFromAny(explicitRect);
            if (normalizedExplicit.width > 0 && normalizedExplicit.height > 0) {
                return normalizedExplicit;
            }
        }

        const objectRect = item && (item.ObjectRect || item.objectRect
            || item.Rect || item.rect);
        const rect = hitRectFromAny(objectRect);
        if (rect.width <= 0 || rect.height <= 0) return null;

        const margin = Math.max(0, finiteNumber(item.WrapMargin ?? item.wrapMargin, 0));
        const distanceLeft = Math.max(0, finiteNumber(item.DistanceLeft ?? item.distanceLeft, margin));
        const distanceRight = Math.max(0, finiteNumber(item.DistanceRight ?? item.distanceRight, margin));
        const distanceTop = Math.max(0, finiteNumber(item.DistanceTop ?? item.distanceTop, margin));
        const distanceBottom = Math.max(0, finiteNumber(item.DistanceBottom ?? item.distanceBottom, margin));
        const frameRect = frame ? hitRectFromAny(frame) : null;

        if (mode === 'TopBottom' && frameRect && frameRect.width > 0) {
            return {
                x: frameRect.x,
                y: rect.y - distanceTop,
                width: frameRect.width,
                height: rect.height + distanceTop + distanceBottom,
            };
        }

        return {
            x: rect.x - distanceLeft,
            y: rect.y - distanceTop,
            width: rect.width + distanceLeft + distanceRight,
            height: rect.height + distanceTop + distanceBottom,
        };
    };
}
