// Phase D — core/object-selection-snapshot.mjs
// `createObjectSelectionSnapshotFactory({ findDrawingRunByObjectId })` →
//   `createObjectSelectionSnapshot(model, input, previousTextSelection)` — builds an
//   Object-mode selection snapshot for a drawing object. Resolves the object's anchor
//   (block/offset/inline-index/region/header-footer/table/cell) from the model via the
//   injected `findDrawingRunByObjectId`, layering any explicit values from `input` on
//   top, and preserves an underlying text selection so returning to Text mode lands
//   the caret where it was. Only `findDrawingRunByObjectId` is engine-state dependent;
//   everything else is pure selection-snapshot machinery.

import { asText } from './helpers.mjs';
import { normalizeTextExclusionColumnIndex } from './normalize-target.mjs';
import {
    createLogicalRange,
    normalizeTextSelectionPayload,
    createSelectionSnapshot,
} from './selection-snapshot.mjs';

export function createObjectSelectionSnapshotFactory(options) {
    const opts = options || {};
    if (typeof opts.findDrawingRunByObjectId !== 'function') {
        throw new TypeError(
            'createObjectSelectionSnapshotFactory requires options.findDrawingRunByObjectId (function)');
    }
    const { findDrawingRunByObjectId } = opts;

    return function createObjectSelectionSnapshot(model, input, previousTextSelection) {
        const body = typeof input === 'string' ? { objectId: input } : (input || {});
        const objectId = asText(body.objectId || body.ObjectId || body.activeObjectId || body.ActiveObjectId || '');
        const hasExplicitRegion = !!asText(body.region || body.Region || '');
        let region = asText(body.region || body.Region || 'Body');
        let headerFooterId = body.headerFooterId || body.HeaderFooterId || null;
        let tableId = body.tableId || body.TableId || body.activeTableId || body.ActiveTableId || null;
        let cellId = body.cellId || body.CellId || body.activeTableCellId || body.ActiveTableCellId || null;
        let columnIndex = normalizeTextExclusionColumnIndex(body.columnIndex ?? body.ColumnIndex);
        const found = objectId ? findDrawingRunByObjectId(model, objectId) : null;
        let blockId = '';
        let anchorOffset = 0;
        let anchorInlineIndex = -1;
        let runId = null;
        let kind = 'image';

        if (found && found.object) {
            blockId = found.object.anchorBlockId || found.blockId || '';
            anchorOffset = Number(found.object.anchorOffset ?? 0) || 0;
            anchorInlineIndex = Number(found.object.anchorInlineIndex ?? found.inlineIndex ?? -1);
            runId = (found.run && found.run.id) || found.inlineId || null;
            kind = found.object.kind || 'image';
            region = hasExplicitRegion && region !== 'Body'
                ? region
                : asText(found.object.anchorRegion || found.object.region || found.region || region || 'Body');
            headerFooterId = headerFooterId || found.object.anchorHeaderFooterId || found.object.headerFooterId || found.headerFooterId || null;
            tableId = tableId || found.object.anchorTableId || found.object.tableId || found.tableId || null;
            cellId = cellId || found.object.anchorCellId || found.object.cellId || found.cellId || null;
            columnIndex = normalizeTextExclusionColumnIndex(columnIndex ?? found.object.anchorColumnIndex ?? found.object.columnIndex ?? found.columnIndex);
        }

        blockId = asText(body.anchorBlockId || body.AnchorBlockId || blockId || body.blockId || body.BlockId);
        anchorOffset = Number(body.anchorOffset ?? body.AnchorOffset ?? body.offset ?? body.Offset ?? anchorOffset) || 0;
        anchorInlineIndex = Number(body.anchorInlineIndex ?? body.AnchorInlineIndex ?? body.inlineIndex ?? body.InlineIndex ?? anchorInlineIndex);
        runId = body.runId || body.RunId || body.inlineId || body.InlineId || runId;
        const textSelection = previousTextSelection || body.textSelection || body.TextSelection || body.previousTextSelection || body.PreviousTextSelection || null;
        const fallbackText = normalizeTextSelectionPayload({ region, blockId, offset: anchorOffset, headerFooterId, tableId, cellId, columnIndex }, null, region);
        const preservedTextSelection = textSelection
            ? normalizeTextSelectionPayload(Object.assign({}, textSelection, {
                region: textSelection.region || textSelection.Region || region,
                headerFooterId: textSelection.headerFooterId || textSelection.HeaderFooterId || headerFooterId,
                tableId: textSelection.tableId || textSelection.TableId || textSelection.activeTableId || textSelection.ActiveTableId || tableId,
                cellId: textSelection.cellId || textSelection.CellId || textSelection.activeTableCellId || textSelection.ActiveTableCellId || cellId,
            }), null, region)
            : fallbackText;
        const objectRange = createLogicalRange(
            { region, blockId, objectId, offset: anchorOffset, affinity: 'before', headerFooterId, tableId, cellId },
            { region, blockId, objectId, offset: anchorOffset, affinity: 'after', headerFooterId, tableId, cellId },
            'none');
        objectRange.isCollapsed = false;
        return createSelectionSnapshot({
            region,
            range: objectRange,
            blockId,
            objectId,
            activeObjectId: objectId,
            activeImageBlockId: blockId,
            headerFooterId,
            tableId,
            cellId,
            activeTableId: tableId,
            activeTableCellId: cellId,
            columnIndex,
            hitTargetKind: kind,
            selectionMode: 'Object',
            isObjectSelection: true,
            isCollapsed: false,
            textSelection: preservedTextSelection,
            objectSelection: {
                region,
                kind,
                objectId,
                blockId,
                anchorBlockId: blockId,
                anchorOffset,
                anchorInlineIndex,
                inlineIndex: anchorInlineIndex,
                runId,
                headerFooterId,
                tableId,
                cellId,
                columnIndex,
                textSelection: preservedTextSelection,
            },
        });
    };
}
