// Phase D — core/indexes.mjs
// Factory: `createIndexBuilder({ normalizeImageObject })` returns `buildIndexes(model)`.
// Walks the model tree and builds the `model.indexes` map (blocks/inlines/objects/
// drawingObjectsById/drawingRunsByBlockId/revisions/comments).
//
// The image-normalisation step is injected because `normalizeImageObject` lives in the
// image pipeline and would create a circular dependency if we imported it here. Callers
// pass it in (or pass a no-op stub for tests that don't exercise drawing runs).

import { asArray, asText, sortObject } from './helpers.mjs';
import { normalizeTextExclusionColumnIndex } from './normalize-target.mjs';

export function createBlockIndexContext(context, overrides) {
    const source = Object.assign({}, context || {}, overrides || {});
    return sortObject({
        region: asText(source.region || source.Region || 'Body') || 'Body',
        headerFooterId: source.headerFooterId || source.HeaderFooterId || null,
        tableId: source.tableId || source.TableId || null,
        cellId: source.cellId || source.CellId || null,
        columnIndex: normalizeTextExclusionColumnIndex(source.columnIndex ?? source.ColumnIndex),
        pageFrame: source.pageFrame || source.PageFrame || null,
        pageIndex: source.pageIndex ?? source.PageIndex ?? null,
    });
}

// Default image normaliser used when caller doesn't provide one. Produces a minimal
// object record with just the fields the index needs to wire up the drawing object
// entries. The real image pipeline replaces this with `normalizeImageObject` when
// floating-image layout is required.
function defaultNormalizeImageObject(run /* , context */) {
    return {
        objectId: run && (run.objectId || run.ObjectId || run.id || run.Id || ''),
        anchorRegion: null,
        anchorHeaderFooterId: null,
        anchorTableId: null,
        anchorCellId: null,
        anchorColumnIndex: null,
        columnIndex: null,
    };
}

export function createIndexBuilder(options) {
    const opts = options || {};
    const normalizeImageObject = (typeof opts.normalizeImageObject === 'function')
        ? opts.normalizeImageObject
        : defaultNormalizeImageObject;

    function buildIndexes(model) {
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
                asArray(block.content && block.content.runs).forEach((run, index) => {
                    if (!run || !run.id) return;
                    indexes.inlines[run.id] = run;
                    if (run.kind === 'field' || run.kind === 'token') {
                        indexes.objects[run.id] = run;
                    }
                    if (run.kind === 'drawing') {
                        const object = normalizeImageObject(run,
                            Object.assign({ blockId: block.id, inlineIndex: index }, blockContext));
                        const objectId = object.objectId || run.objectId || run.id;
                        const objectRegion = (blockContext.region && blockContext.region !== 'Body'
                            && (!object.anchorRegion || object.anchorRegion === 'Body'))
                            ? blockContext.region
                            : (object.anchorRegion || blockContext.region || 'Body');
                        const headerFooterId = object.anchorHeaderFooterId
                            || blockContext.headerFooterId || null;
                        const tableId = object.anchorTableId || blockContext.tableId || null;
                        const cellId = object.anchorCellId || blockContext.cellId || null;
                        const columnIndex = normalizeTextExclusionColumnIndex(
                            object.anchorColumnIndex ?? object.columnIndex ?? blockContext.columnIndex);

                        indexes.objects[objectId] = run;
                        if (!indexes.drawingRunsByBlockId[block.id]) {
                            indexes.drawingRunsByBlockId[block.id] = [];
                        }
                        indexes.drawingRunsByBlockId[block.id].push(run);
                        indexes.drawingObjectsById[objectId] = {
                            objectId,
                            blockId: block.id,
                            inlineId: run.id,
                            inlineIndex: index,
                            run,
                            layout: object,
                            region: objectRegion,
                            headerFooterId,
                            tableId,
                            cellId,
                            columnIndex,
                        };
                    }
                });
            }

            if (block.type === 'table') {
                asArray(block.content && block.content.rows).forEach(row => {
                    asArray(row.cells).forEach((cell, columnIndex) => {
                        const cellContext = createBlockIndexContext(blockContext, {
                            region: 'TableCell',
                            tableId: block.id,
                            cellId: (cell && cell.id) || null,
                            columnIndex,
                        });
                        asArray(cell.blocks).forEach(childBlock => {
                            visitBlock(childBlock, cellContext);
                        });
                    });
                });
            }
        }

        asArray(model.body && model.body.blocks).forEach(block => {
            visitBlock(block, { region: 'Body' });
        });
        asArray(model.headers).forEach(region => {
            asArray(region.blocks).forEach(block => {
                visitBlock(block, { region: 'Header', headerFooterId: (region && region.id) || null });
            });
        });
        asArray(model.footers).forEach(region => {
            asArray(region.blocks).forEach(block => {
                visitBlock(block, { region: 'Footer', headerFooterId: (region && region.id) || null });
            });
        });
        asArray(model.revisions).forEach(revision => {
            if (revision && (revision.id || revision.Id)) {
                indexes.revisions[revision.id || revision.Id] = revision;
            }
        });
        asArray(model.comments).forEach(comment => {
            if (comment && (comment.id || comment.Id)) {
                indexes.comments[comment.id || comment.Id] = comment;
            }
        });

        model.indexes = indexes;
        model.indexVersion = Number(model.indexVersion || 0) + 1;
        model.indexesBuiltAt = Date.now();
        return indexes;
    }

    return Object.freeze({ buildIndexes, createBlockIndexContext });
}

// Index-based block finder. Pure function that uses an existing `model.indexes.blocks`
// map (built by `buildIndexes`). Falls back to null when missing.
export function findBlockByIndex(model, blockId) {
    const id = asText(blockId);
    if (!model || !id) return null;
    if (!model.indexes || !model.indexes.blocks) return null;
    return model.indexes.blocks[id] || null;
}
