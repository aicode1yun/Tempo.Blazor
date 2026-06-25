// Phase D — layout/layout-scope.mjs
// `createLayoutScope` and `inferLayoutScopeFromOperation` — pure functions that decide
// how much of the document needs re-layout for a given mutation. Extracted now that
// `LayoutScopeKinds`, `OperationTypes`, `normalizeTarget`, `normalizeRange` are all
// available as ES modules.

import { asArray, sortObject, unique } from '../core/helpers.mjs';
import { normalizeTarget, normalizeRange } from '../core/normalize-target.mjs';
import { OperationTypes } from '../history/operation-types.mjs';
import { LayoutScopeKinds } from './scope-kinds.mjs';

export function createLayoutScope(kind, options) {
    const opts = options || {};
    return sortObject({
        kind: kind || LayoutScopeKinds.ActiveParagraph,
        blockId: opts.blockId || opts.BlockId || null,
        region: opts.region || opts.Region || 'Body',
        headerFooterId: opts.headerFooterId || opts.HeaderFooterId || null,
        tableId: opts.tableId || opts.TableId || opts.activeTableId || opts.ActiveTableId || null,
        cellId: opts.cellId || opts.CellId || opts.activeTableCellId || opts.ActiveTableCellId || null,
        pageIndex: Number(opts.pageIndex ?? opts.PageIndex ?? 0),
        affectedScopeIds: asArray(opts.affectedScopeIds || opts.AffectedScopeIds
            || (opts.blockId || opts.BlockId ? [opts.blockId || opts.BlockId] : [])),
        reason: opts.reason || opts.Reason || '',
    });
}

export function inferLayoutScopeFromOperation(operation) {
    const op = operation || {};
    const type = op.type || op.Type || '';

    if (type === OperationTypes.InsertText || type === OperationTypes.SetParagraphAttribute) {
        const target = normalizeTarget(op.target || op.Target);
        return createLayoutScope(LayoutScopeKinds.ActiveParagraph,
            Object.assign({}, target, { affectedScopeIds: [target.blockId], reason: type }));
    }
    if (type === OperationTypes.DeleteRange
        || type === OperationTypes.ApplyMark
        || type === OperationTypes.RemoveMark) {
        const range = normalizeRange(op.range || op.Range);
        return createLayoutScope(LayoutScopeKinds.ActiveParagraph,
            Object.assign({}, range, { affectedScopeIds: [range.blockId], reason: type }));
    }
    if (type === OperationTypes.SplitParagraph || type === OperationTypes.MergeParagraph) {
        const paragraphTarget = normalizeTarget(op.target || op.Target);
        return createLayoutScope(LayoutScopeKinds.WholeBlock,
            Object.assign({}, paragraphTarget, {
                affectedScopeIds: [paragraphTarget.blockId, op.newBlockId || op.NewBlockId].filter(Boolean),
                reason: type,
            }));
    }
    if (type === OperationTypes.UpdateImageLayout
        || type === OperationTypes.MoveDrawingObject
        || type === OperationTypes.InsertImage
        || type === OperationTypes.UpdateImageMetadata) {
        const objectTarget = normalizeTarget(op.target || op.Target);
        return createLayoutScope(LayoutScopeKinds.PageRegion, {
            blockId: objectTarget.blockId,
            region: objectTarget.region || 'Body',
            headerFooterId: objectTarget.headerFooterId || null,
            tableId: objectTarget.tableId || null,
            cellId: objectTarget.cellId || null,
            affectedScopeIds: unique([objectTarget.blockId]
                .concat(asArray(op.affectedParagraphIds || op.AffectedParagraphIds))),
            reason: type,
        });
    }
    if (type === OperationTypes.AcceptRevision
        || type === OperationTypes.RejectRevision
        || type === OperationTypes.InsertTable
        || type === OperationTypes.UpdateTableCell) {
        return createLayoutScope(LayoutScopeKinds.WholeDocument, {
            affectedScopeIds: ['document'],
            reason: type,
        });
    }
    return createLayoutScope(LayoutScopeKinds.ActiveParagraph, {
        blockId: '',
        affectedScopeIds: [],
        reason: type || 'unknown',
    });
}
