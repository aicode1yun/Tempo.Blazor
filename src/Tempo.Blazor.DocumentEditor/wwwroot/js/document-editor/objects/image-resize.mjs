// Phase D — objects/image-resize.mjs
// Pure geometry helpers for the image-resize interaction.
//
//   - Constants `IMAGE_RESIZE_MIN_WIDTH` / `..._HEIGHT` clamp the bottom edge of resize bounds.
//   - `normalizeImageResizeHandleName(value)` — canonicalise to one of 8 cardinal handles.
//   - `imageResizeHandleIndex(value)` — ordinal index (0..7) of the resolved handle.
//   - `computeImageResizeFixedPoint(rect, handle)` — the corner/edge that stays anchored
//     during the drag (opposite of `handle`).
//   - `createImageResizeBounds(opts)` — assemble min/max from caller options.
//   - `clampImageResizeSize(width, height, ratio, preserveAspect, bounds)` — apply bounds
//     and aspect ratio. Returns rounded {width, height}.

import { asText, sortObject } from '../core/helpers.mjs';

export const IMAGE_RESIZE_MIN_WIDTH = 32;
export const IMAGE_RESIZE_MIN_HEIGHT = 24;

const HANDLE_ORDER = Object.freeze(['nw', 'n', 'ne', 'e', 'se', 's', 'sw', 'w']);

export function normalizeImageResizeHandleName(value) {
    const raw = asText(value || 'se').trim().toLowerCase();
    return HANDLE_ORDER.indexOf(raw) >= 0 ? raw : 'se';
}

export function imageResizeHandleIndex(value) {
    return HANDLE_ORDER.indexOf(normalizeImageResizeHandleName(value));
}

export function computeImageResizeFixedPoint(rect, handleName) {
    const handle = normalizeImageResizeHandleName(handleName);
    const left = Number(rect && rect.x || 0) || 0;
    const top = Number(rect && rect.y || 0) || 0;
    const width = Math.max(1, Number(rect && rect.width || 1) || 1);
    const height = Math.max(1, Number(rect && rect.height || 1) || 1);
    const right = left + width;
    const bottom = top + height;
    const centerX = left + width / 2;
    const centerY = top + height / 2;
    return sortObject({
        x: handle.indexOf('w') >= 0 ? right : (handle.indexOf('e') >= 0 ? left : centerX),
        y: handle.indexOf('n') >= 0 ? bottom : (handle.indexOf('s') >= 0 ? top : centerY),
    });
}

export function createImageResizeBounds(options) {
    const opts = options || {};
    const snap = opts.snapContext || null;
    const bodyRect = snap && (snap.bodyRect || snap.BodyRect) || null;
    const bodyWidth = bodyRect ? (bodyRect.Width ?? bodyRect.width) : 0;
    const bodyHeight = bodyRect ? (bodyRect.Height ?? bodyRect.height) : 0;
    const width = Number(opts.maxWidth ?? opts.MaxWidth ?? bodyWidth ?? 0) || 0;
    const height = Number(opts.maxHeight ?? opts.MaxHeight ?? bodyHeight ?? 0) || 0;
    return sortObject({
        minWidth: Math.max(1,
            Number(opts.minWidth ?? opts.MinWidth ?? IMAGE_RESIZE_MIN_WIDTH) || IMAGE_RESIZE_MIN_WIDTH),
        minHeight: Math.max(1,
            Number(opts.minHeight ?? opts.MinHeight ?? IMAGE_RESIZE_MIN_HEIGHT) || IMAGE_RESIZE_MIN_HEIGHT),
        maxWidth: width > 0 ? width : null,
        maxHeight: height > 0 ? height : null,
    });
}

export function clampImageResizeSize(width, height, ratio, preserveAspect, bounds) {
    const minWidth = Math.max(1,
        Number(bounds && bounds.minWidth || IMAGE_RESIZE_MIN_WIDTH) || IMAGE_RESIZE_MIN_WIDTH);
    const minHeight = Math.max(1,
        Number(bounds && bounds.minHeight || IMAGE_RESIZE_MIN_HEIGHT) || IMAGE_RESIZE_MIN_HEIGHT);
    const maxWidth = Number(bounds && bounds.maxWidth || 0) || 0;
    const maxHeight = Number(bounds && bounds.maxHeight || 0) || 0;
    let nextWidth = Math.max(minWidth, Number(width || 0) || minWidth);
    let nextHeight = Math.max(minHeight, Number(height || 0) || minHeight);
    if (maxWidth > 0) nextWidth = Math.min(maxWidth, nextWidth);
    if (maxHeight > 0) nextHeight = Math.min(maxHeight, nextHeight);
    if (preserveAspect) {
        const r = Math.max(0.01, Number(ratio || 1) || 1);
        if (nextWidth / r < minHeight) nextWidth = minHeight * r;
        if (nextHeight * r < minWidth) nextHeight = minWidth / r;
        if (maxWidth > 0 && nextWidth > maxWidth) nextWidth = maxWidth;
        nextHeight = nextWidth / r;
        if (maxHeight > 0 && nextHeight > maxHeight) {
            nextHeight = maxHeight;
            nextWidth = nextHeight * r;
        }
        nextWidth = Math.max(minWidth, nextWidth);
        nextHeight = Math.max(minHeight, nextHeight);
    }
    return {
        width: Math.round(nextWidth * 100) / 100,
        height: Math.round(nextHeight * 100) / 100,
    };
}
