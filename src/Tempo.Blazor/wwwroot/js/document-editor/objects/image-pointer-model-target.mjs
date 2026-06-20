// Phase D — objects/image-pointer-model-target.mjs
// `createResolveImageObjectPointerModelTarget({findDrawingRunByObjectId,
//   normalizeImageObject})` →
//   `resolveImageObjectPointerModelTarget(inst, pointerTarget)` — given a
//   pointer-target descriptor from `getObjectPointerTarget`, looks up the
//   drawing run in `inst.model` by objectId and returns `{kind:'drawing', objectId,
//   blockId, object}` (with the normalised image object) or `null` when no
//   matching drawing exists.

export function createResolveImageObjectPointerModelTarget(options) {
    const opts = options || {};
    for (const key of ['findDrawingRunByObjectId', 'normalizeImageObject']) {
        if (typeof opts[key] !== 'function') {
            throw new TypeError(
                `createResolveImageObjectPointerModelTarget requires options.${key} (function)`);
        }
    }
    const { findDrawingRunByObjectId, normalizeImageObject } = opts;

    return function resolveImageObjectPointerModelTarget(inst, pointerTarget) {
        if (!inst || !pointerTarget) return null;
        const objectId = pointerTarget.objectId;
        const drawing = objectId ? findDrawingRunByObjectId(inst.model, objectId) : null;
        if (drawing) {
            return {
                kind: 'drawing',
                objectId,
                blockId: drawing.blockId,
                object: normalizeImageObject(drawing.run || {}, {
                    blockId: drawing.blockId,
                    inlineIndex: drawing.inlineIndex,
                }),
            };
        }

        return null;
    };
}
