// Phase D — objects/drawing-runs.mjs
// Drawing-run lookup helpers + snapshot builder. Factory pattern: takes `buildIndexes`
// so the lookups can refresh the model's drawing index when needed.
//
// `createDrawingObjectSnapshot(entry)` builds a stable shape from a `drawingObjectsById`
// entry — used by host/Blazor when exposing the drawing object to the C# side.

import { asText, clone, sortObject } from '../core/helpers.mjs';
import { findBlockContainer } from '../core/model-finders.mjs';
import { plainRuns } from '../core/inline-runs.mjs';
import { normalizeDrawingKindName } from './drawing-kind.mjs';
import { normalizeImageObject } from './image-object.mjs';

// Factory — caller supplies a `buildIndexes(model)` function (from `core/indexes.mjs`).
// Returns `{ ensureDrawingIndexes, rebuildDrawingIndexes, findDrawingRunByObjectId,
// findDrawingRunByAsset, removeDrawingRunByObjectId, createDrawingObjectSnapshot }`.
export function createDrawingRunsModule(options) {
    const opts = options || {};
    if (typeof opts.buildIndexes !== 'function') {
        throw new TypeError('createDrawingRunsModule requires options.buildIndexes');
    }
    const { buildIndexes } = opts;

    function ensureDrawingIndexes(model) {
        if (!model) return { drawingObjectsById: {}, drawingRunsByBlockId: {} };
        if (!model.indexes
            || !model.indexes.drawingObjectsById
            || !model.indexes.drawingRunsByBlockId) {
            buildIndexes(model);
        }
        return model.indexes || { drawingObjectsById: {}, drawingRunsByBlockId: {} };
    }

    function rebuildDrawingIndexes(model) {
        if (!model) return { drawingObjectsById: {}, drawingRunsByBlockId: {} };
        buildIndexes(model);
        return model.indexes || { drawingObjectsById: {}, drawingRunsByBlockId: {} };
    }

    function createDrawingObjectSnapshot(entry) {
        if (!entry) return null;
        const run = entry.run || {};
        const layout = normalizeImageObject(run, {
            blockId: entry.blockId,
            inlineIndex: entry.inlineIndex,
            region: entry.region || 'Body',
            headerFooterId: entry.headerFooterId || null,
            tableId: entry.tableId || null,
            cellId: entry.cellId || null,
        });
        return sortObject({
            objectId: layout.objectId || entry.objectId || run.objectId || run.id || '',
            runId: entry.inlineId || run.id || null,
            blockId: entry.blockId || layout.blockId || '',
            region: layout.anchorRegion || entry.region || 'Body',
            headerFooterId: layout.anchorHeaderFooterId || entry.headerFooterId || null,
            tableId: layout.anchorTableId || entry.tableId || null,
            cellId: layout.anchorCellId || entry.cellId || null,
            inlineIndex: Number(entry.inlineIndex ?? layout.anchorInlineIndex ?? -1),
            drawingKind: normalizeDrawingKindName(run.drawingKind || run.DrawingKind || 'Image'),
            source: run.source ?? run.Source ?? 0,
            url: run.url ?? run.Url ?? null,
            assetId: run.assetId ?? run.AssetId ?? null,
            altText: run.altText || run.AltText || '',
            caption: run.caption || run.Caption || '',
            layoutKind: layout.layoutKind,
            isInline: layout.isInline === true,
            isAnchored: layout.isAnchored === true,
            anchorBlockId: layout.anchorBlockId || entry.blockId || '',
            anchorOffset: Number(layout.anchorOffset || 0) || 0,
            anchorInlineIndex: Number(layout.anchorInlineIndex ?? entry.inlineIndex ?? -1),
            anchorRegion: layout.anchorRegion || entry.region || 'Body',
            anchorHeaderFooterId: layout.anchorHeaderFooterId || entry.headerFooterId || '',
            anchorTableId: layout.anchorTableId || entry.tableId || '',
            anchorCellId: layout.anchorCellId || entry.cellId || '',
            wrapMode: layout.wrapMode,
            width: layout.width,
            height: layout.height,
            zIndex: layout.zIndex,
            layout,
        });
    }

    function findDrawingRunByObjectId(model, objectId) {
        const id = asText(objectId);
        if (!id) return null;
        const indexes = ensureDrawingIndexes(model);
        const entry = indexes.drawingObjectsById && indexes.drawingObjectsById[id];
        if (!entry) return null;
        return sortObject({
            objectId: id,
            blockId: entry.blockId || '',
            inlineId: entry.inlineId || null,
            inlineIndex: Number(entry.inlineIndex ?? -1),
            region: entry.region || null,
            headerFooterId: entry.headerFooterId || null,
            tableId: entry.tableId || null,
            cellId: entry.cellId || null,
            run: clone(entry.run || null),
            object: createDrawingObjectSnapshot(entry),
        });
    }

    function findDrawingRunByAsset(model, assetId, objectId) {
        const wantedAssetId = asText(assetId || '');
        const wantedObjectId = asText(objectId || '');
        ensureDrawingIndexes(model);
        const objects = (model && model.indexes && model.indexes.drawingObjectsById) || {};
        const keys = Object.keys(objects);
        for (let i = 0; i < keys.length; i++) {
            const entry = objects[keys[i]];
            const run = (entry && entry.run) || {};
            const runAssetId = asText(run.assetId || run.AssetId
                || (entry && entry.object && entry.object.assetId) || '');
            const runObjectId = asText(run.objectId || run.ObjectId
                || (entry && entry.objectId) || '');
            if ((wantedObjectId && runObjectId === wantedObjectId)
                || (wantedAssetId && runAssetId === wantedAssetId)) {
                return sortObject({
                    blockId: entry.blockId || '',
                    objectId: runObjectId,
                    inlineIndex: Number(entry.inlineIndex ?? -1),
                    inlineId: entry.inlineId || run.id || run.Id || null,
                    run,
                    object: normalizeImageObject(run, {
                        blockId: entry.blockId || '',
                        inlineIndex: Number(entry.inlineIndex ?? -1),
                    }),
                });
            }
        }
        return null;
    }

    function removeDrawingRunByObjectId(model, objectId) {
        const id = asText(objectId);
        if (!id) return { ok: false, error: { code: 'missing-object-id' } };
        const indexes = rebuildDrawingIndexes(model);
        const entry = indexes.drawingObjectsById && indexes.drawingObjectsById[id];
        if (!entry) return { ok: false, error: { code: 'drawing-object-not-found', objectId: id } };
        const container = findBlockContainer(model, entry.blockId || '');
        const block = (container && container.block) || null;
        const runs = block && block.content && block.content.runs;
        if (!Array.isArray(runs)) {
            return { ok: false, error: { code: 'drawing-object-block-not-found', objectId: id, blockId: entry.blockId || '' } };
        }
        const index = runs.findIndex(run =>
            run && (run.objectId === id || run.ObjectId === id || run.id === entry.inlineId));
        if (index < 0) {
            return { ok: false, error: { code: 'drawing-run-not-found', objectId: id, blockId: block.id || '' } };
        }
        const removed = runs.splice(index, 1)[0] || null;
        if (runs.length === 0) {
            block.content.runs = plainRuns('', block.id + '-empty');
        }
        buildIndexes(model);
        return sortObject({
            ok: true,
            deletedObjectId: id,
            deletedKind: 'drawing',
            blockId: block.id || '',
            inlineIndex: index,
            run: clone(removed || null),
            affectedScopeIds: [block.id || 'document'],
        });
    }

    return Object.freeze({
        ensureDrawingIndexes,
        rebuildDrawingIndexes,
        createDrawingObjectSnapshot,
        findDrawingRunByObjectId,
        findDrawingRunByAsset,
        removeDrawingRunByObjectId,
    });
}
