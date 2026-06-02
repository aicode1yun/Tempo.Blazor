// Phase D — history/handlers-image.mjs
// `createImageHandlers(deps)` → `{ applyInsertImage, applyUpdateImageLayout,
//   applyMoveDrawingObject, applyUpdateImageMetadata }`.
// The drawing/image operation handlers, extracted as a factory so the engine can
// wire its internal collaborators (findBlock, drawing-run mutators, selection
// snapshot builders, …) without this module importing the whole graph.
//
// • applyInsertImage — inserts a drawing run at a text offset, promoting a fresh
//   paragraph when the target block isn't editable text; returns an object
//   selection on the inserted drawing.
// • applyUpdateImageLayout — replaces the drawing run's layout + size box and
//   recomputes the affected-paragraph set around old + new anchor blocks.
// • applyMoveDrawingObject — normalises old/new layout + anchor onto the op, then
//   delegates to applyUpdateImageLayout with the merged layout.
// • applyUpdateImageMetadata — shallow-merges metadata onto the run.

const REQUIRED_FUNCS = [
    'normalizeTarget', 'findBlockContainer', 'isEditableTextBlock', 'importBlock',
    'stableId', 'blockText', 'operationRegionInfo', 'splitInlineListForDrawingInsert',
    'createDrawingRunFromImageInsert', 'insertDrawingRunAtTextOffset', 'buildIndexes',
    'createSelectionSnapshot', 'createObjectSelectionSnapshot', 'unique', 'asArray',
    'asText', 'findDrawingRunByObjectId', 'findBlock', 'clone', 'syncImageLayoutCase',
    'sortObject', 'normalizeImageObject', 'affectedParagraphsAroundObject',
    'imageObjectToLayout',
];

export function createImageHandlers(deps) {
    const opts = deps || {};
    for (const key of REQUIRED_FUNCS) {
        if (typeof opts[key] !== 'function') {
            throw new TypeError(`createImageHandlers requires options.${key} (function)`);
        }
    }
    if (!opts.OperationTypes || typeof opts.OperationTypes !== 'object') {
        throw new TypeError('createImageHandlers requires options.OperationTypes (object)');
    }
    const {
        normalizeTarget, findBlockContainer, isEditableTextBlock, importBlock,
        stableId, blockText, operationRegionInfo, splitInlineListForDrawingInsert,
        createDrawingRunFromImageInsert, insertDrawingRunAtTextOffset, buildIndexes,
        createSelectionSnapshot, createObjectSelectionSnapshot, unique, asArray,
        asText, findDrawingRunByObjectId, findBlock, clone, syncImageLayoutCase,
        sortObject, normalizeImageObject, affectedParagraphsAroundObject,
        imageObjectToLayout, OperationTypes,
    } = opts;

    function applyInsertImage(model, op, differ) {
        let target = normalizeTarget(op.target || op.Target);
        let container = findBlockContainer(model, target.blockId);
        if (!container || !container.block) {
            return { ok: false, errors: [{ code: 'missing-target-block', path: 'operation.target.blockId', blockId: target.blockId }] };
        }

        let block = container.block;
        if (!isEditableTextBlock(block)) {
            block = importBlock({
                Id: op.paragraphId || op.ParagraphId
                    || stableId('image-paragraph', (container.block.id || 'block') + '-' + Date.now()),
                Type: 'Paragraph',
                Content: { Inlines: [{ Id: stableId('inline', 'insert-image-empty'), Text: '' }] },
            }, 'insert-image-paragraph');
            container.blocks.splice(container.index + 1, 0, block);
            container = { blocks: container.blocks, index: container.index + 1, block };
            target = Object.assign({}, target, { blockId: block.id, offset: 0 });
        }

        const offset = Math.max(0, Math.min(blockText(block).length, Number(target.offset || 0) || 0));
        const regionInfo = operationRegionInfo(model, op, block.id, target);
        const split = splitInlineListForDrawingInsert(block, offset, { affinity: target.affinity || target.Affinity || 'after' });
        const drawing = createDrawingRunFromImageInsert(block, split, op, regionInfo);
        const insert = insertDrawingRunAtTextOffset(block, offset, drawing, { affinity: target.affinity || target.Affinity || 'after' });
        buildIndexes(model);

        const restoreSelection = createSelectionSnapshot({
            region: regionInfo.region || target.region || 'Body',
            blockId: block.id,
            offset: insert.offset,
            affinity: 'after',
            inlineIndex: insert.inlineIndex + 1,
            anchorInlineIndex: insert.inlineIndex + 1,
            isCollapsed: true,
            headerFooterId: regionInfo.headerFooterId || target.headerFooterId || null,
            tableId: regionInfo.tableId || target.tableId || null,
            cellId: regionInfo.cellId || target.cellId || null,
            activeTableId: regionInfo.tableId || target.tableId || null,
            activeTableCellId: regionInfo.cellId || target.cellId || null,
            columnIndex: regionInfo.columnIndex ?? target.columnIndex ?? null,
        });
        const nextSelection = createObjectSelectionSnapshot(model, {
            region: regionInfo.region || target.region || 'Body',
            blockId: block.id,
            objectId: insert.objectId,
            anchorBlockId: block.id,
            anchorOffset: insert.offset,
            anchorInlineIndex: insert.inlineIndex,
            inlineIndex: insert.inlineIndex,
            runId: insert.runId,
            headerFooterId: regionInfo.headerFooterId || target.headerFooterId || null,
            tableId: regionInfo.tableId || target.tableId || null,
            cellId: regionInfo.cellId || target.cellId || null,
            columnIndex: regionInfo.columnIndex ?? target.columnIndex ?? null,
            textSelection: restoreSelection,
        }, restoreSelection);
        const affected = unique([block.id].concat(asArray(op.affectedParagraphIds || op.AffectedParagraphIds)));
        differ.record({
            objectChange: { blockId: block.id, objectId: insert.objectId, inlineIndex: insert.inlineIndex, type: 'insert-drawing-run' },
            invalidatedLayoutScopes: affected,
            invalidatedOverlayScopes: [block.id, insert.objectId],
        });
        return {
            ok: true,
            invalidatedLayoutScopes: affected,
            nextSelection,
            insertedBlockId: null,
            insertedObjectId: insert.objectId,
            insertedRunId: insert.runId,
            insertedInlineIndex: insert.inlineIndex,
        };
    }

    function applyUpdateImageLayout(model, op, differ) {
        const target = normalizeTarget(op.target || op.Target);
        const objectId = target.objectId || asText(op.objectId || op.ObjectId || '');
        const drawing = objectId ? findDrawingRunByObjectId(model, objectId) : null;
        if (drawing) {
            const drawingBlock = findBlock(model, drawing.blockId);
            const runs = drawingBlock && drawingBlock.content && drawingBlock.content.runs;
            const runIndex = Array.isArray(runs)
                ? runs.findIndex(function (run) {
                    return run && (run.objectId === objectId || run.ObjectId === objectId
                        || run.id === drawing.inlineId || run.Id === drawing.inlineId);
                })
                : -1;
            if (runIndex < 0) return { ok: false, errors: [{ code: 'drawing-run-not-found', objectId, blockId: drawing.blockId }] };

            const drawingLayout = syncImageLayoutCase(clone(op.newLayout || op.NewLayout || op.layout || op.Layout || {}));
            runs[runIndex].layout = drawingLayout;
            const drawingTransform = drawingLayout.Transform || drawingLayout.transform || {};
            const drawingSize = runs[runIndex].size || runs[runIndex].Size || {};
            runs[runIndex].size = sortObject({
                width: drawingTransform.Width ?? drawingTransform.width ?? drawingSize.width ?? drawingSize.Width ?? null,
                height: drawingTransform.Height ?? drawingTransform.height ?? drawingSize.height ?? drawingSize.Height ?? null,
                lockAspectRatio: (drawingTransform.LockAspectRatio ?? drawingTransform.lockAspectRatio ?? drawingSize.lockAspectRatio ?? drawingSize.LockAspectRatio ?? true) !== false,
            });
            buildIndexes(model);
            const drawingAnchor = drawingLayout.Anchor || drawingLayout.anchor || {};
            const updatedObject = normalizeImageObject(runs[runIndex], { blockId: drawing.blockId, inlineIndex: runIndex });
            const anchorBlockId = drawingAnchor.BlockId || drawingAnchor.blockId || drawing.blockId;
            const affectedDrawing = unique([drawing.blockId, anchorBlockId]
                .concat(affectedParagraphsAroundObject(model, drawing.blockId))
                .concat(anchorBlockId !== drawing.blockId ? affectedParagraphsAroundObject(model, anchorBlockId) : [])
                .concat(asArray(op.affectedParagraphIds || op.AffectedParagraphIds))
                .filter(Boolean));
            differ.record({
                objectChange: { blockId: drawing.blockId, objectId, inlineIndex: runIndex, type: 'layout' },
                invalidatedLayoutScopes: affectedDrawing,
                invalidatedOverlayScopes: [drawing.blockId, objectId],
            });
            const selectionRegion = updatedObject.anchorRegion || updatedObject.region || drawing.region || target.region || 'Body';
            return {
                ok: true,
                invalidatedLayoutScopes: affectedDrawing,
                nextSelection: createObjectSelectionSnapshot(model, {
                    region: selectionRegion,
                    blockId: drawing.blockId,
                    objectId,
                    anchorBlockId: drawingAnchor.BlockId || drawingAnchor.blockId || drawing.blockId,
                    anchorInlineIndex: runIndex,
                    headerFooterId: updatedObject.anchorHeaderFooterId || drawing.headerFooterId || target.headerFooterId || null,
                    tableId: updatedObject.anchorTableId || drawing.tableId || target.tableId || null,
                    cellId: updatedObject.anchorCellId || drawing.cellId || target.cellId || null,
                    columnIndex: updatedObject.anchorColumnIndex ?? updatedObject.columnIndex ?? drawing.columnIndex ?? target.columnIndex ?? null,
                }),
            };
        }

        return { ok: false, errors: [{ code: 'drawing-layout-target-not-found', blockId: target.blockId, objectId }] };
    }

    function applyMoveDrawingObject(model, op, differ) {
        const target = normalizeTarget(op.target || op.Target);
        const objectId = target.objectId || asText(op.objectId || op.ObjectId || '');
        const drawing = objectId ? findDrawingRunByObjectId(model, objectId) : null;
        const currentLayout = drawing
            ? imageObjectToLayout(normalizeImageObject(drawing.run || {}, {
                blockId: drawing.blockId,
                inlineIndex: drawing.inlineIndex,
                region: drawing.region || (drawing.object && drawing.object.anchorRegion) || null,
                headerFooterId: drawing.headerFooterId || (drawing.object && drawing.object.anchorHeaderFooterId) || null,
                tableId: drawing.tableId || (drawing.object && drawing.object.anchorTableId) || null,
                cellId: drawing.cellId || (drawing.object && drawing.object.anchorCellId) || null,
            }))
            : null;
        if (!currentLayout) {
            return { ok: false, errors: [{ code: 'drawing-layout-target-not-found', blockId: target.blockId, objectId }] };
        }
        let nextLayout = syncImageLayoutCase(clone(op.newLayout || op.NewLayout || op.layout || op.Layout || currentLayout || {}));
        const newAnchor = clone(op.newAnchor || op.NewAnchor || null);
        if (newAnchor) {
            nextLayout.Anchor = Object.assign({}, nextLayout.Anchor || nextLayout.anchor || {}, newAnchor);
            nextLayout.anchor = Object.assign({}, nextLayout.anchor || nextLayout.Anchor || {}, newAnchor);
            nextLayout = syncImageLayoutCase(nextLayout);
        }

        const oldLayout = syncImageLayoutCase(clone(op.oldLayout || op.OldLayout || currentLayout || {}));
        op.oldLayout = oldLayout;
        op.OldLayout = oldLayout;
        op.newLayout = nextLayout;
        op.NewLayout = nextLayout;
        op.layout = nextLayout;
        op.Layout = nextLayout;
        op.oldAnchor = clone(op.oldAnchor || op.OldAnchor || oldLayout.Anchor || oldLayout.anchor || {});
        op.OldAnchor = clone(op.oldAnchor);
        op.newAnchor = clone(op.newAnchor || op.NewAnchor || nextLayout.Anchor || nextLayout.anchor || {});
        op.NewAnchor = clone(op.newAnchor);

        const oldAnchor = oldLayout.Anchor || oldLayout.anchor || {};
        const nextAnchor = nextLayout.Anchor || nextLayout.anchor || {};
        const oldAnchorBlockId = oldAnchor.BlockId || oldAnchor.blockId || null;
        const nextAnchorBlockId = nextAnchor.BlockId || nextAnchor.blockId || null;
        const affected = unique([
            target.blockId,
            drawing && drawing.blockId,
            oldAnchorBlockId,
            nextAnchorBlockId,
        ]
            .concat(affectedParagraphsAroundObject(model, drawing && drawing.blockId))
            .concat(oldAnchorBlockId ? affectedParagraphsAroundObject(model, oldAnchorBlockId) : [])
            .concat(nextAnchorBlockId ? affectedParagraphsAroundObject(model, nextAnchorBlockId) : [])
            .concat(asArray(op.affectedParagraphIds || op.AffectedParagraphIds))
            .filter(Boolean));
        op.affectedParagraphIds = affected;
        op.AffectedParagraphIds = affected;

        return applyUpdateImageLayout(model, Object.assign({}, op, {
            type: OperationTypes.UpdateImageLayout,
            layout: nextLayout,
            Layout: nextLayout,
            affectedParagraphIds: affected,
            AffectedParagraphIds: affected,
        }), differ);
    }

    function applyUpdateImageMetadata(model, op, differ) {
        const target = normalizeTarget(op.target || op.Target);
        const metadata = op.metadata || op.Metadata || {};
        const objectId = target.objectId || asText(op.objectId || op.ObjectId || '');
        const drawing = objectId ? findDrawingRunByObjectId(model, objectId) : null;
        if (drawing) {
            const drawingBlock = findBlock(model, drawing.blockId);
            const runs = drawingBlock && drawingBlock.content && drawingBlock.content.runs;
            const runIndex = Array.isArray(runs)
                ? runs.findIndex(function (run) {
                    return run && (run.objectId === objectId || run.ObjectId === objectId
                        || run.id === drawing.inlineId || run.Id === drawing.inlineId);
                })
                : -1;
            if (runIndex < 0) return { ok: false, errors: [{ code: 'drawing-run-not-found', objectId, blockId: drawing.blockId }] };

            Object.assign(runs[runIndex], sortObject(metadata));
            buildIndexes(model);
            const object = normalizeImageObject(runs[runIndex], { blockId: drawing.blockId, inlineIndex: runIndex });
            const affectedDrawing = unique([drawing.blockId, object.anchorBlockId]
                .concat(asArray(op.affectedParagraphIds || op.AffectedParagraphIds))
                .filter(Boolean));
            differ.record({
                objectChange: { blockId: drawing.blockId, objectId, inlineIndex: runIndex, type: 'metadata' },
                invalidatedLayoutScopes: affectedDrawing,
                invalidatedOverlayScopes: [drawing.blockId, objectId],
            });
            return {
                ok: true,
                invalidatedLayoutScopes: affectedDrawing,
                nextSelection: createObjectSelectionSnapshot(model, {
                    region: target.region || object.anchorRegion || 'Body',
                    blockId: drawing.blockId,
                    objectId,
                    anchorBlockId: object.anchorBlockId || drawing.blockId,
                    anchorOffset: object.anchorOffset,
                    anchorInlineIndex: runIndex,
                    headerFooterId: object.anchorHeaderFooterId || target.headerFooterId || null,
                    tableId: object.anchorTableId || target.tableId || null,
                    cellId: object.anchorCellId || target.cellId || null,
                }),
            };
        }

        return { ok: false, errors: [{ code: 'drawing-metadata-target-not-found', blockId: target.blockId, objectId }] };
    }

    return Object.freeze({
        applyInsertImage,
        applyUpdateImageLayout,
        applyMoveDrawingObject,
        applyUpdateImageMetadata,
    });
}
