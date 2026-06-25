// Phase D — objects/image-resize-preview.mjs
// `shouldReanchorImageObject(object, options)` — whether dragging an image should
//   re-anchor it to a new paragraph. Page-fixed objects never re-anchor;
//   lock-anchored objects re-anchor only on an explicit drag.
// `createComputeImageResizePreview({normalizeImageResizeHandleName,
//   clampImageResizeSize, computeImageResizeFixedPoint, clone, sortObject})` →
//   `computeImageResizePreview(track, dx, dy, event)` — resize preview geometry for
//   the active handle. Width/height grow per the handle direction; corner handles
//   (or Shift) preserve the aspect ratio. Result carries the applied top-left
//   delta, the new size, and the fixed corner the resize pivots around.

export function shouldReanchorImageObject(object, options) {
    const opts = options || {};
    if (object && (object.fixedOnPage === true || object.FixedOnPage === true)) return false;
    if (object && (object.lockAnchor === true || object.LockAnchor === true)) {
        return opts.explicitDrag === true || opts.ExplicitDrag === true;
    }
    return true;
}

export function createComputeImageResizePreview(options) {
    const opts = options || {};
    for (const key of ['normalizeImageResizeHandleName', 'clampImageResizeSize',
        'computeImageResizeFixedPoint', 'clone', 'sortObject']) {
        if (typeof opts[key] !== 'function') {
            throw new TypeError(`createComputeImageResizePreview requires options.${key} (function)`);
        }
    }
    const {
        normalizeImageResizeHandleName, clampImageResizeSize,
        computeImageResizeFixedPoint, clone, sortObject,
    } = opts;

    return function computeImageResizePreview(track, dx, dy, event) {
        const rawDx = Number(dx || 0) || 0;
        const rawDy = Number(dy || 0) || 0;
        const handle = normalizeImageResizeHandleName((track && track.handleName) || 'se');
        const originalRect = (track && track.originalRect) || {};
        const originalObject = (track && track.originalObject) || {};
        const originalWidth = Math.max(1, Number(originalObject.width || originalRect.width || 1) || 1);
        const originalHeight = Math.max(1, Number(originalObject.height || originalRect.height || 1) || 1);
        const originalLeft = Number(originalRect.x || 0) || 0;
        const originalTop = Number(originalRect.y || 0) || 0;
        const originalRight = originalLeft + originalWidth;
        const originalBottom = originalTop + originalHeight;
        const originalCenterX = originalLeft + originalWidth / 2;
        const originalCenterY = originalTop + originalHeight / 2;
        const west = handle.indexOf('w') >= 0;
        const east = handle.indexOf('e') >= 0;
        const north = handle.indexOf('n') >= 0;
        const south = handle.indexOf('s') >= 0;
        const horizontalDelta = west ? -rawDx : (east ? rawDx : 0);
        const verticalDelta = north ? -rawDy : (south ? rawDy : 0);
        let nextWidth = originalWidth + horizontalDelta;
        let nextHeight = originalHeight + verticalDelta;
        const ratio = Math.max(0.01, originalWidth / Math.max(1, originalHeight));
        const corner = (west || east) && (north || south);
        const shiftKey = event && (event.shiftKey === true || event.ShiftKey === true);
        const preserveAspect = shiftKey || (corner && (track && track.lockAspectRatio !== false));
        if (preserveAspect) {
            if (Math.abs(horizontalDelta) >= Math.abs(verticalDelta)) {
                nextHeight = nextWidth / ratio;
            } else {
                nextWidth = nextHeight * ratio;
            }
        } else {
            if (!west && !east) nextWidth = originalWidth;
            if (!north && !south) nextHeight = originalHeight;
        }

        const clamped = clampImageResizeSize(nextWidth, nextHeight, ratio, preserveAspect,
            (track && track.resizeBounds) || null);
        nextWidth = clamped.width;
        nextHeight = clamped.height;
        const nextLeft = west
            ? originalRight - nextWidth
            : (east ? originalLeft : originalCenterX - nextWidth / 2);
        const nextTop = north
            ? originalBottom - nextHeight
            : (south ? originalTop : originalCenterY - nextHeight / 2);
        return sortObject({
            dx: rawDx,
            dy: rawDy,
            appliedDx: Math.round((nextLeft - originalLeft) * 100) / 100,
            appliedDy: Math.round((nextTop - originalTop) * 100) / 100,
            width: nextWidth,
            height: nextHeight,
            preserveAspectRatio: preserveAspect,
            fixedPoint: clone((track && track.fixedPoint)
                || computeImageResizeFixedPoint(originalRect, handle)),
            guides: [],
        });
    };
}
