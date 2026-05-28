// Phase D — core/build-indexes.mjs
// `createBuildIndexes({normalizeImageObject, createBlockIndexContext})` factory →
// `buildIndexes(model)` — rebuilds `model.indexes` with `{blocks, inlines, objects,
// drawingObjectsById, drawingRunsByBlockId, revisions, comments}` maps.
//
// Walks body / headers / footers, recurses into table cells. Drawing runs get a
// derived layout entry via `normalizeImageObject`. Each rebuild bumps
// `model.indexVersion` and stamps `model.indexesBuiltAt`.

import { asArray } from './helpers.mjs';
import { normalizeTextExclusionColumnIndex } from './normalize-target.mjs';

export function createBuildIndexes(options) {
    const opts = options || {};
    const required = ['normalizeImageObject', 'createBlockIndexContext'];
    for (const key of required) {
        if (typeof opts[key] !== 'function') {
            throw new TypeError(
                `createBuildIndexes requires options.${key} (function)`);
        }
    }
    const { normalizeImageObject, createBlockIndexContext } = opts;

    return function buildIndexes(model) {
        const indexes = {
            blocks: {},
            inlines: {},
            objects: {},
            drawingObjectsById: {},
            drawingRunsByBlockId: {},
            revisions: {},
            comments: {},
        };

        function visitBlock(block, context) {
            if (!block || !block.id) return;
            const blockContext = createBlockIndexContext(context);
            indexes.blocks[block.id] = block;
            if (block.type === 'paragraph') {
                asArray(block.content && block.content.runs).forEach(function (run, index) {
                    if (!run || !run.id) return;
                    indexes.inlines[run.id] = run;
                    if (run.kind === 'field' || run.kind === 'token') {
                        indexes.objects[run.id] = run;
                    }
                    if (run.kind === 'drawing') {
                        const object = normalizeImageObject(run, Object.assign(
                            { blockId: block.id, inlineIndex: index }, blockContext));
                        const objectId = object.objectId || run.objectId || run.id;
                        const objectRegion = (blockContext.region
                            && blockContext.region !== 'Body'
                            && (!object.anchorRegion || object.anchorRegion === 'Body'))
                            ? blockContext.region
                            : (object.anchorRegion || blockContext.region || 'Body');
                        const headerFooterId = object.anchorHeaderFooterId
                            || blockContext.headerFooterId || null;
                        const tableId = object.anchorTableId || blockContext.tableId || null;
                        const cellId = object.anchorCellId || blockContext.cellId || null;
                        const columnIndex = normalizeTextExclusionColumnIndex(
                            object.anchorColumnIndex ?? object.columnIndex
                            ?? blockContext.columnIndex);
                        indexes.objects[objectId] = run;
                        if (!indexes.drawingRunsByBlockId[block.id]) {
                            indexes.drawingRunsByBlockId[block.id] = [];
                        }
                        indexes.drawingRunsByBlockId[block.id].push(run);
                        indexes.drawingObjectsById[objectId] = {
                            objectId: objectId,
                            blockId: block.id,
                            inlineId: run.id,
                            inlineIndex: index,
                            run: run,
                            layout: object,
                            region: objectRegion,
                            headerFooterId: headerFooterId,
                            tableId: tableId,
                            cellId: cellId,
                            columnIndex: columnIndex,
                        };
                    }
                });
            }
            if (block.type === 'table') {
                asArray(block.content && block.content.rows).forEach(function (row) {
                    asArray(row.cells).forEach(function (cell, columnIndex) {
                        const cellContext = createBlockIndexContext(blockContext, {
                            region: 'TableCell',
                            tableId: block.id,
                            cellId: (cell && cell.id) || null,
                            columnIndex: columnIndex,
                        });
                        asArray(cell.blocks).forEach(function (childBlock) {
                            visitBlock(childBlock, cellContext);
                        });
                    });
                });
            }
        }

        asArray(model.body && model.body.blocks).forEach(function (block) {
            visitBlock(block, { region: 'Body' });
        });
        asArray(model.headers).forEach(function (region) {
            asArray(region.blocks).forEach(function (block) {
                visitBlock(block, {
                    region: 'Header',
                    headerFooterId: (region && region.id) || null,
                });
            });
        });
        asArray(model.footers).forEach(function (region) {
            asArray(region.blocks).forEach(function (block) {
                visitBlock(block, {
                    region: 'Footer',
                    headerFooterId: (region && region.id) || null,
                });
            });
        });
        asArray(model.revisions).forEach(function (revision) {
            if (revision && (revision.id || revision.Id)) {
                indexes.revisions[revision.id || revision.Id] = revision;
            }
        });
        asArray(model.comments).forEach(function (comment) {
            if (comment && (comment.id || comment.Id)) {
                indexes.comments[comment.id || comment.Id] = comment;
            }
        });
        model.indexes = indexes;
        model.indexVersion = Number(model.indexVersion || 0) + 1;
        model.indexesBuiltAt = Date.now();
        return indexes;
    };
}
