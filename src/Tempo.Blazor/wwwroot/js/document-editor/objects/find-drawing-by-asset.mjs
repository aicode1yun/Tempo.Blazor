// Phase D — objects/find-drawing-by-asset.mjs
// `createFindDrawingRunByAsset({ensureDrawingIndexes, normalizeImageObject})` factory
// → `findDrawingRunByAsset(model, assetId, objectId)` — linear scan over
// drawingObjectsById; matches either by objectId or assetId. Returns null when
// no match.

import { asText, sortObject } from '../core/helpers.mjs';

export function createFindDrawingRunByAsset(options) {
    const opts = options || {};
    if (typeof opts.ensureDrawingIndexes !== 'function') {
        throw new TypeError(
            'createFindDrawingRunByAsset requires options.ensureDrawingIndexes (function)');
    }
    if (typeof opts.normalizeImageObject !== 'function') {
        throw new TypeError(
            'createFindDrawingRunByAsset requires options.normalizeImageObject (function)');
    }
    const { ensureDrawingIndexes, normalizeImageObject } = opts;

    return function findDrawingRunByAsset(model, assetId, objectId) {
        const wantedAssetId = asText(assetId || '');
        const wantedObjectId = asText(objectId || '');
        ensureDrawingIndexes(model);
        const objects = (model && model.indexes && model.indexes.drawingObjectsById) || {};
        const keys = Object.keys(objects);
        for (let i = 0; i < keys.length; i++) {
            const entry = objects[keys[i]];
            const run = (entry && entry.run) || {};
            const runAssetId = asText(
                run.assetId || run.AssetId
                || (entry && entry.object && entry.object.assetId)
                || '');
            const runObjectId = asText(
                run.objectId || run.ObjectId
                || (entry && entry.objectId) || '');
            if ((wantedObjectId && runObjectId === wantedObjectId)
                || (wantedAssetId && runAssetId === wantedAssetId)) {
                return sortObject({
                    blockId: entry.blockId || '',
                    objectId: runObjectId,
                    inlineIndex: Number(entry.inlineIndex ?? -1),
                    inlineId: entry.inlineId || run.id || run.Id || null,
                    run: run,
                    object: normalizeImageObject(run, {
                        blockId: entry.blockId || '',
                        inlineIndex: Number(entry.inlineIndex ?? -1),
                    }),
                });
            }
        }
        return null;
    };
}
