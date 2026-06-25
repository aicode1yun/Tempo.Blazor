// Phase D — history/operation-affected.mjs
// `operationAffectedBlockIds(operation)` + `transactionAffectedBlockIds(transaction,
// operations)` — collect the set of block ids that an operation/transaction might
// invalidate, including `'revisions'` sentinel for tracked-changes-touching ops.
//
// Pure functions. Used by render-invalidation logic to decide what to re-layout.

import { asArray, asText, unique } from '../core/helpers.mjs';

export function operationAffectedBlockIds(operation) {
    const op = operation || {};
    const ids = [];
    const target = op.target || op.Target || null;
    const range = op.range || op.Range || null;
    const selection = op.selection || op.Selection || null;
    if (target && (target.blockId || target.BlockId)) ids.push(target.blockId || target.BlockId);
    if (range && (range.blockId || range.BlockId)) ids.push(range.blockId || range.BlockId);
    if (selection && (selection.blockId || selection.BlockId)) ids.push(selection.blockId || selection.BlockId);
    if (op.blockId || op.BlockId) ids.push(op.blockId || op.BlockId);
    if (op.newBlockId || op.NewBlockId) ids.push(op.newBlockId || op.NewBlockId);
    if (op.revisionId || op.RevisionId) ids.push('revisions');
    asArray(op.affectedScopeIds || op.AffectedScopeIds
        || op.affectedParagraphIds || op.AffectedParagraphIds
        || op.affectedSelectable || op.AffectedSelectable)
        .forEach(id => { if (id) ids.push(id); });
    return unique(ids.map(asText).filter(Boolean));
}

export function transactionAffectedBlockIds(transaction, operations) {
    let ids = [];
    asArray(operations).forEach(operation => {
        ids = ids.concat(operationAffectedBlockIds(operation));
    });
    ids = ids.concat(asArray(transaction && transaction.invalidatedScopes));
    return unique(ids.map(asText).filter(Boolean));
}
