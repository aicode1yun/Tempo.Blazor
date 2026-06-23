// Phase D — objects/anchored-drawing-position.mjs
// Pure positional resolvers for floating drawings.
// `resolvePositionReferenceRect(relativeTo, pageRect, bodyFrame, paragraphRect, characterRect, lineRect)`
//   → picks the reference rect based on a `relativeTo` name (Page/Margin/Column/Paragraph/Character/Line).
// `resolveAlignedHorizontal(position, reference, width)` — left/center/right + offset.
// `resolveAlignedVertical(position, reference, height)` — top/middle/bottom + offset.
// `resolveAnchoredDrawingRect(object, reference, page)` — final on-page rect for an
// anchored drawing using the helpers above; `fixedOnPage=true` collapses paragraph/line/
// character references to the body frame.

import { sortObject } from '../core/helpers.mjs';
import { normalizeRelativePositionName } from './layout-helpers.mjs';

export function resolvePositionReferenceRect(
    relativeTo, pageRect, bodyFrame, paragraphRect, characterRect, lineRect) {
    const key = normalizeRelativePositionName(relativeTo);
    if (key === 'Page') return pageRect || bodyFrame;
    if (key === 'Margin' || key === 'Column') return bodyFrame || pageRect;
    if (key === 'Paragraph') return paragraphRect || bodyFrame || pageRect;
    if (key === 'Character') return characterRect || paragraphRect || bodyFrame || pageRect;
    if (key === 'Line') return lineRect || paragraphRect || bodyFrame || pageRect;
    return bodyFrame || pageRect;
}

export function resolveAlignedHorizontal(position, reference, width) {
    const align = String((position && position.align) || 'Left').toLowerCase();
    const offset = Number((position && position.offset) || 0) || 0;
    const frame = reference || { x: 0, width: 0 };
    if (align === 'center' || align === 'middle') {
        return Number(frame.x || 0) + ((Number(frame.width || 0) - width) / 2) + offset;
    }
    if (align === 'right' || align === 'end') {
        return Number(frame.x || 0) + Number(frame.width || 0) - width + offset;
    }
    return Number(frame.x || 0) + offset;
}

export function resolveAlignedVertical(position, reference, height) {
    const align = String((position && position.align) || 'Top').toLowerCase();
    const offset = Number((position && position.offset) || 0) || 0;
    const frame = reference || { y: 0, height: 0 };
    if (align === 'middle' || align === 'center') {
        return Number(frame.y || 0) + ((Number(frame.height || 0) - height) / 2) + offset;
    }
    if (align === 'bottom' || align === 'end') {
        return Number(frame.y || 0) + Number(frame.height || 0) - height + offset;
    }
    return Number(frame.y || 0) + offset;
}

export function resolveAnchoredDrawingRect(object, reference, page) {
    const source = object || {};
    const pageRect = (page && page.rect) || { x: 0, y: 0, width: 640, height: 900 };
    const bodyFrame = (page && page.bodyFrame) || pageRect;
    const anchorRect = (reference && reference.rect) || bodyFrame;
    const width = Math.max(1, Number(source.width || 1) || 1);
    const height = Math.max(1, Number(source.height || 1) || 1);
    const paragraphRect = source.fixedOnPage === true ? bodyFrame : anchorRect;
    const lineRect = source.fixedOnPage === true ? bodyFrame : anchorRect;
    const characterRect = source.fixedOnPage === true ? bodyFrame : {
        x: Number(anchorRect.x || 0),
        y: Number(anchorRect.y || 0),
        width: 0,
        height: Math.max(1, Number(anchorRect.height || 18) || 18),
    };
    const horizontalReference = resolvePositionReferenceRect(
        source.horizontalPosition && source.horizontalPosition.relativeTo,
        pageRect, bodyFrame, paragraphRect, characterRect, lineRect);
    const verticalReference = resolvePositionReferenceRect(
        source.verticalPosition && source.verticalPosition.relativeTo,
        pageRect, bodyFrame, paragraphRect, characterRect, lineRect);
    return sortObject({
        x: resolveAlignedHorizontal(source.horizontalPosition, horizontalReference, width),
        y: resolveAlignedVertical(source.verticalPosition, verticalReference, height),
        width,
        height,
    });
}
