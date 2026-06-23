// Phase D — objects/active-image-target.mjs
// `createActiveImageTarget({asText, findDrawingRunByObjectId,
//   findDrawingRunByAsset, sortObject})` →
//   `activeImageTarget(inst, payload?)` — resolves the image the runtime image
//   command should act on. Lookup order:
//     1. explicit `payload.objectId` / current selection's object id,
//     2. asset id from payload + objectId (for cross-document references).
//   Returns a `{kind:'drawing', blockId, objectId, inlineIndex, inlineId, region,
//   headerFooterId, tableId, cellId, columnIndex, run, object}` descriptor, with
//   anchor* scope fields from the normalised image preferred over the per-drawing
//   ones, or null when no drawing matches.

export function createActiveImageTarget(options) {
    const opts = options || {};
    for (const key of ['asText', 'findDrawingRunByObjectId',
        'findDrawingRunByAsset', 'sortObject']) {
        if (typeof opts[key] !== 'function') {
            throw new TypeError(
                `createActiveImageTarget requires options.${key} (function)`);
        }
    }
    const { asText, findDrawingRunByObjectId, findDrawingRunByAsset, sortObject } = opts;

    return function activeImageTarget(inst, payload) {
        const body = payload || {};
        const selection = (inst && inst.selection) || {};
        const objectSelection = selection.objectSelection || selection.ObjectSelection || {};
        const objectId = asText(
            body.objectId || body.ObjectId
            || objectSelection.objectId || objectSelection.ObjectId
            || selection.activeObjectId || selection.ActiveObjectId
            || selection.objectId || selection.ObjectId
            || '');
        if (objectId) {
            const drawing = findDrawingRunByObjectId(inst && inst.model, objectId);
            if (drawing) {
                return sortObject({
                    kind: 'drawing',
                    blockId: drawing.blockId,
                    objectId,
                    inlineIndex: drawing.inlineIndex,
                    inlineId: drawing.inlineId,
                    region: drawing.region
                        || (drawing.object && drawing.object.anchorRegion) || null,
                    headerFooterId: drawing.headerFooterId
                        || (drawing.object && drawing.object.anchorHeaderFooterId) || null,
                    tableId: drawing.tableId
                        || (drawing.object && drawing.object.anchorTableId) || null,
                    cellId: drawing.cellId
                        || (drawing.object && drawing.object.anchorCellId) || null,
                    columnIndex: drawing.columnIndex
                        ?? (drawing.object
                            ? (drawing.object.anchorColumnIndex
                                ?? drawing.object.columnIndex ?? null)
                            : null),
                    run: drawing.run,
                    object: drawing.object,
                });
            }
        }

        const assetDrawing = findDrawingRunByAsset(
            inst && inst.model, body.assetId || body.AssetId || '', objectId);
        if (assetDrawing) {
            return sortObject({
                kind: 'drawing',
                blockId: assetDrawing.blockId,
                objectId: assetDrawing.objectId,
                inlineIndex: assetDrawing.inlineIndex,
                inlineId: assetDrawing.inlineId,
                region: (assetDrawing.object && assetDrawing.object.anchorRegion) || null,
                headerFooterId: (assetDrawing.object && assetDrawing.object.anchorHeaderFooterId) || null,
                tableId: (assetDrawing.object && assetDrawing.object.anchorTableId) || null,
                cellId: (assetDrawing.object && assetDrawing.object.anchorCellId) || null,
                columnIndex: assetDrawing.columnIndex
                    ?? (assetDrawing.object
                        ? (assetDrawing.object.anchorColumnIndex
                            ?? assetDrawing.object.columnIndex ?? null)
                        : null),
                run: assetDrawing.run,
                object: assetDrawing.object,
            });
        }

        return null;
    };
}
