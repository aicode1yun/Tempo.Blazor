// Phase D — objects/overlap-geometry.mjs
// `intervalEndGeometry(interval)` — `x + width`.
// `subtractGeometryInterval(intervals, blockedLeft, blockedRight, minWidth, atY, lineHeight)`
//   — clips an array of horizontal intervals against a blocked range. Pieces shorter
//   than `minWidth` are dropped, keeping caller layout free from tiny gaps.
// `objectOverlapCollisionRect(object)` — wrap-aware footprint rect used for collision.
// `resolveObjectOverlapGeometry(existingObjects, object, bodyFrame)` — slides the
// object's rect down past colliding peers within the same wrap layer until either
// (a) there's no overlap, (b) the body bottom is reached, or (c) the 64-iteration guard
// trips. Mutates `object.rect.y` in place and returns the same object.

import { asArray } from '../core/helpers.mjs';
import {
    rectFromGeometry,
    rectBottomGeometry,
    rectIntersectsGeometry,
    createObjectFootprintRect,
} from './geometry.mjs';
import { drawingLayerForWrapMode } from './layer-priority.mjs';

export function intervalEndGeometry(interval) {
    return Number((interval && interval.x) || 0) + Number((interval && interval.width) || 0);
}

export function subtractGeometryInterval(
    intervals, blockedLeft, blockedRight, minWidth, atY, lineHeight) {
    const result = [];
    asArray(intervals).forEach(function (interval) {
        const intervalLeft = Number(interval.x || 0);
        const intervalRight = intervalEndGeometry(interval);
        if (blockedRight <= intervalLeft || blockedLeft >= intervalRight) {
            result.push(interval);
            return;
        }
        if (blockedLeft > intervalLeft && blockedLeft - intervalLeft >= minWidth) {
            result.push({
                x: intervalLeft,
                y: atY,
                width: blockedLeft - intervalLeft,
                height: lineHeight,
            });
        }
        if (blockedRight < intervalRight && intervalRight - blockedRight >= minWidth) {
            result.push({
                x: blockedRight,
                y: atY,
                width: intervalRight - blockedRight,
                height: lineHeight,
            });
        }
    });
    return result;
}

export function objectOverlapCollisionRect(object) {
    return createObjectFootprintRect(object || {}, rectFromGeometry((object && object.rect) || {}));
}

export function resolveObjectOverlapGeometry(existingObjects, object, bodyFrame) {
    if (!object || object.allowOverlap === true) return object;
    const body = rectFromGeometry(bodyFrame || { x: 0, y: 0, width: 640, height: 900 });
    const layer = drawingLayerForWrapMode(object.wrapMode);
    let guard = 0;
    while (guard++ < 64) {
        const collisionRect = objectOverlapCollisionRect(object);
        const overlap = asArray(existingObjects)
            .filter(function (existing) {
                if (!existing
                    || existing.allowOverlap === true
                    || existing.inlineObject === true
                    || !existing.rect) return false;
                return drawingLayerForWrapMode(existing.wrapMode) === layer
                    && rectIntersectsGeometry(
                        objectOverlapCollisionRect(existing), collisionRect);
            })
            .sort(function (left, right) {
                return rectBottomGeometry(objectOverlapCollisionRect(left))
                    - rectBottomGeometry(objectOverlapCollisionRect(right));
            })
            .pop();
        if (!overlap) break;
        const overlapRect = objectOverlapCollisionRect(overlap);
        let nextY = rectBottomGeometry(overlapRect) + 8;
        if (nextY + collisionRect.height > rectBottomGeometry(body)) {
            nextY = Math.max(body.y, rectBottomGeometry(body) - collisionRect.height);
            if (nextY <= collisionRect.y + 0.01) break;
        }
        const delta = nextY - Number(object.rect.y || 0);
        if (Math.abs(delta) < 0.01) break;
        object.rect.y += delta;
    }
    return object;
}
