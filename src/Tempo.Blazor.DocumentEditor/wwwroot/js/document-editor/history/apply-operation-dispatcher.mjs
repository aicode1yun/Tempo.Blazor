// Phase D — history/apply-operation-dispatcher.mjs
// `createApplyOperationDispatcher({ handlers, validateOperation, attachOperationMethods,
//                                    createDiffer, buildIndexes, normalizeRevisionGroups,
//                                    operationAffectedBlockIds })`
// → `applyOperation(model, operation, context)`.
//
// The dispatcher routes by `operation.type` to a handler from the injected `handlers`
// map. The handlers themselves (applyInsertText, applyDeleteRange, etc.) stay in the
// legacy IIFE because each one is 30-100+ lines deeply tied to engine internals; this
// module factors out the routing/validation/post-processing boilerplate that's
// common across all 14+ operation types.
//
// Pure factory — no closure state.

import { clone } from '../core/helpers.mjs';
import { OperationTypes } from './operation-types.mjs';

// Maps operation types to handler keys in the `handlers` map. Some types (ApplyMark
// / RemoveMark, AcceptRevision / RejectRevision) share a single handler with a
// boolean second argument.
const TYPE_HANDLER_MAP = Object.freeze({
    [OperationTypes.InsertText]: { handler: 'applyInsertText' },
    [OperationTypes.DeleteRange]: { handler: 'applyDeleteRange' },
    [OperationTypes.SplitParagraph]: { handler: 'applySplitParagraph' },
    [OperationTypes.MergeParagraph]: { handler: 'applyMergeParagraph' },
    [OperationTypes.ApplyMark]: { handler: 'applyMarkOperation', extra: false },
    [OperationTypes.RemoveMark]: { handler: 'applyMarkOperation', extra: true },
    [OperationTypes.SetParagraphAttribute]: { handler: 'applySetParagraphAttribute' },
    [OperationTypes.InsertImage]: { handler: 'applyInsertImage' },
    [OperationTypes.UpdateImageLayout]: { handler: 'applyUpdateImageLayout' },
    [OperationTypes.MoveDrawingObject]: { handler: 'applyMoveDrawingObject' },
    [OperationTypes.UpdateImageMetadata]: { handler: 'applyUpdateImageMetadata' },
    [OperationTypes.InsertTable]: { handler: 'applyInsertTable' },
    [OperationTypes.UpdateTableCell]: { handler: 'applyUpdateTableCell' },
    [OperationTypes.AcceptRevision]: { handler: 'applyRevisionDecision' },
    [OperationTypes.RejectRevision]: { handler: 'applyRevisionDecision' },
    [OperationTypes.RestoreSnapshot]: { handler: 'applyRestoreSnapshot' },
});

export function createApplyOperationDispatcher(options) {
    const opts = options || {};
    const handlers = opts.handlers || {};
    const required = ['validateOperation', 'attachOperationMethods', 'createDiffer',
        'buildIndexes', 'normalizeRevisionGroups', 'operationAffectedBlockIds'];
    for (const key of required) {
        if (typeof opts[key] !== 'function') {
            throw new TypeError(`createApplyOperationDispatcher requires options.${key} (function)`);
        }
    }
    const {
        validateOperation,
        attachOperationMethods,
        createDiffer,
        buildIndexes,
        normalizeRevisionGroups,
        operationAffectedBlockIds,
    } = opts;

    function applyOperation(model, operation, context) {
        const op = attachOperationMethods(operation || {});
        const validation = validateOperation(model, op);
        if (!validation.ok) {
            return { ok: false, errors: validation.errors, operation: op };
        }

        const differ = (context && context.differ) || createDiffer();
        const selection = context && context.selection ? clone(context.selection) : null;
        let result;

        // SetSelection is a special no-op dispatcher case — no handler needed.
        if (op.type === OperationTypes.SetSelection) {
            result = {
                ok: true,
                nextSelection: clone(op.selection || selection || null),
                invalidatedLayoutScopes: [],
                operation: op,
            };
        } else {
            const route = TYPE_HANDLER_MAP[op.type];
            if (!route) {
                return {
                    ok: false,
                    errors: [{ code: 'unsupported-operation', type: op.type }],
                    operation: op,
                };
            }
            const handler = handlers[route.handler];
            if (typeof handler !== 'function') {
                return {
                    ok: false,
                    errors: [{ code: 'missing-handler', type: op.type, handler: route.handler }],
                    operation: op,
                };
            }
            result = route.extra !== undefined
                ? handler(model, op, differ, route.extra)
                : handler(model, op, differ);
        }

        if (result && result.ok) {
            const revisionNormalization = normalizeRevisionGroups(model,
                result.invalidatedLayoutScopes || operationAffectedBlockIds(op));
            if (!revisionNormalization || revisionNormalization.indexesRebuilt !== true) {
                buildIndexes(model);
            }
            result.differ = differ.snapshot();
            result.operation = op;
        }
        return result;
    }

    return Object.freeze({ applyOperation });
}

// Expose the handler-name map so callers can wire only the handlers they need.
export const ApplyOperationHandlerNames = Object.freeze(
    Array.from(new Set(Object.values(TYPE_HANDLER_MAP).map(route => route.handler))));
