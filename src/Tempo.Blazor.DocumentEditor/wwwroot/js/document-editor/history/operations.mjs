// Phase D — history/operations.mjs
// Operation factory + reversal/JSON helpers. Extracted from the legacy IIFE.
//
// Factory pattern: `createOperationsModule({ idCounters })` returns the bound functions
// that need the operation id counter. The pure helpers that don't need state are
// exported standalone.

import { asArray, asText, clone } from '../core/helpers.mjs';
import { OperationTypes } from './operation-types.mjs';
import { isTypingLikeTransactionType } from './operation-types.mjs';

// ────────────────────────────────────────────────────────────────────────────────
// Pure helpers — no closure state required
// ────────────────────────────────────────────────────────────────────────────────

export function isSelectionOnlyOperation(operation) {
    const type = (operation && (operation.type || operation.Type)) || '';
    return type === OperationTypes.SetSelection;
}

export function operationsAffectDocument(operations) {
    return asArray(operations).some(op => op && !isSelectionOnlyOperation(op));
}

export function transactionAffectsDocument(transaction) {
    return !!(transaction && operationsAffectDocument(transaction.operations));
}

// Whether the operation can participate in the local undo/redo stack. Operations that
// carry a revision id are tracked-changes and bypass history.
export function supportsOperationHistory(operation) {
    const type = (operation && (operation.type || operation.Type)) || '';
    if (operation && (operation.revisionId || operation.RevisionId || operation.revision || operation.Revision)) {
        return false;
    }
    return [
        OperationTypes.InsertText,
        OperationTypes.DeleteRange,
        OperationTypes.ApplyMark,
        OperationTypes.RemoveMark,
        OperationTypes.SetParagraphAttribute,
        OperationTypes.MoveDrawingObject,
        OperationTypes.UpdateImageLayout,
        OperationTypes.SetSelection,
        OperationTypes.RestoreSnapshot,
    ].indexOf(type) >= 0;
}

export function supportsLightweightTransactionSnapshots(operations, transactionType) {
    const list = asArray(operations);
    return list.length > 0
        && isTypingLikeTransactionType(transactionType)
        && list.every(supportsOperationHistory);
}

// ────────────────────────────────────────────────────────────────────────────────
// Factory — stateful helpers that need the id counter
// ────────────────────────────────────────────────────────────────────────────────

export function createOperationsModule(options) {
    const opts = options || {};
    const idCounters = opts.idCounters;
    if (!idCounters || typeof idCounters.nextOperationId !== 'function') {
        throw new TypeError('createOperationsModule requires options.idCounters with nextOperationId()');
    }

    function attachOperationMethods(operation) {
        if (!operation || typeof operation !== 'object') return operation;
        Object.defineProperty(operation, 'getReversed', {
            configurable: true,
            enumerable: false,
            value: () => getReversedOperation(operation),
        });
        Object.defineProperty(operation, 'toJSON', {
            configurable: true,
            enumerable: false,
            value: () => {
                const result = {};
                Object.keys(operation).sort().forEach(key => {
                    if (typeof operation[key] !== 'function') result[key] = operation[key];
                });
                return result;
            },
        });
        return operation;
    }

    function createOperation(type, payload, options) {
        const opts2 = options || {};
        const body = payload || {};
        const operation = Object.assign({}, body, {
            id: asText(body.id || body.Id || opts2.id || idCounters.nextOperationId()),
            type: asText(type || body.type || body.Type),
            timestamp: Number(body.timestamp || body.Timestamp || opts2.timestamp || Date.now()),
            source: asText(body.source || body.Source || opts2.source || 'local'),
            baseVersion: body.baseVersion ?? body.BaseVersion ?? opts2.baseVersion ?? null,
            batchId: body.batchId || body.BatchId || opts2.batchId || null,
            affectedSelectable: asArray(body.affectedSelectable || body.AffectedSelectable || opts2.affectedSelectable),
        });
        return attachOperationMethods(operation);
    }

    function getReversedOperation(operation) {
        const op = operation || {};
        const undoOpts = { source: 'undo', baseVersion: op.baseVersion, batchId: op.batchId };
        switch (op.type) {
            case OperationTypes.InsertText:
                return createOperation(OperationTypes.DeleteRange, {
                    target: op.target,
                    range: {
                        blockId: op.target && op.target.blockId,
                        start: op.target && op.target.offset,
                        end: Number((op.target && op.target.offset) || 0) + asText(op.text).length,
                    },
                    text: op.text,
                }, undoOpts);
            case OperationTypes.DeleteRange:
                return createOperation(OperationTypes.InsertText, {
                    target: { blockId: op.range && op.range.blockId, offset: op.range && op.range.start },
                    text: op.deletedText || op.text || '',
                }, undoOpts);
            case OperationTypes.ApplyMark:
                return createOperation(OperationTypes.RemoveMark, { range: op.range, mark: op.mark }, undoOpts);
            case OperationTypes.RemoveMark:
                return createOperation(OperationTypes.ApplyMark, { range: op.range, mark: op.mark }, undoOpts);
            case OperationTypes.SetParagraphAttribute:
                return createOperation(OperationTypes.SetParagraphAttribute, {
                    target: op.target,
                    attributeName: op.attributeName,
                    value: op.previousValue,
                }, undoOpts);
            case OperationTypes.MoveDrawingObject:
                return createOperation(OperationTypes.MoveDrawingObject, {
                    target: op.target,
                    oldLayout: clone(op.newLayout || op.NewLayout || op.layout || op.Layout || null),
                    newLayout: clone(op.oldLayout || op.OldLayout || null),
                    oldAnchor: clone(op.newAnchor || op.NewAnchor || null),
                    newAnchor: clone(op.oldAnchor || op.OldAnchor || null),
                    layout: clone(op.oldLayout || op.OldLayout || null),
                    affectedParagraphIds: asArray(op.affectedParagraphIds || op.AffectedParagraphIds),
                }, undoOpts);
            case OperationTypes.UpdateImageLayout: {
                const currentLayout = clone(op.layout || op.Layout || op.newLayout || op.NewLayout || null);
                const previousLayout = clone(op.oldLayout || op.OldLayout || op.previousLayout || op.PreviousLayout || currentLayout);
                return createOperation(OperationTypes.UpdateImageLayout, {
                    target: op.target,
                    oldLayout: currentLayout,
                    newLayout: previousLayout,
                    layout: previousLayout,
                    affectedParagraphIds: asArray(op.affectedParagraphIds || op.AffectedParagraphIds),
                }, undoOpts);
            }
            case OperationTypes.SetSelection:
                return createOperation(OperationTypes.SetSelection, {
                    selection: op.previousSelection || null,
                }, undoOpts);
            case OperationTypes.RestoreSnapshot:
                return createOperation(OperationTypes.RestoreSnapshot, {
                    snapshot: op.previousSnapshot || op.snapshot || null,
                    previousSnapshot: op.snapshot || null,
                    selection: op.previousSelection || op.selection || null,
                    previousSelection: op.selection || null,
                    affectedScopeIds: op.affectedScopeIds || ['document'],
                }, undoOpts);
            default:
                return createOperation(op.type || 'Unknown', clone(op), undoOpts);
        }
    }

    function toOperationJson(operation) {
        const attached = attachOperationMethods(clone(operation));
        return attached && attached.toJSON ? attached.toJSON() : clone(attached);
    }

    function createReversedOperationJson(operation) {
        return toOperationJson(getReversedOperation(attachOperationMethods(clone(operation))));
    }

    function createRedoHistoryOperations(operations) {
        return asArray(operations).map(toOperationJson);
    }

    function createUndoHistoryOperations(operations) {
        return asArray(operations).map(createReversedOperationJson);
    }

    return Object.freeze({
        createOperation,
        attachOperationMethods,
        getReversedOperation,
        toOperationJson,
        createReversedOperationJson,
        createRedoHistoryOperations,
        createUndoHistoryOperations,
    });
}
