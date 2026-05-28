// Phase D — history/operation-classifiers.mjs
// Pure predicates on operations that determine whether the operation interacts with
// the tracked-changes (revisions) system or causes wholesale snapshot replacement.
// Used by the autosave/notify/render pipelines to decide whether to emit revision
// notifications.

import { OperationTypes } from './operation-types.mjs';

// True when the operation reads/writes a revision id, accepts a revision, or rejects
// a revision.
export function operationTouchesRevisions(operation) {
    const op = operation || {};
    const type = op.type || op.Type || '';
    return !!(op.revisionId || op.RevisionId || op.revision || op.Revision
        || type === OperationTypes.AcceptRevision
        || type === OperationTypes.RejectRevision);
}

// True when the operation might change the set of pending revisions in the document.
// Includes `RestoreSnapshot` because that can wholesale-replace the revisions array.
export function operationMayChangeRevisions(operation) {
    const type = (operation && (operation.type || operation.Type)) || '';
    return operationTouchesRevisions(operation) || type === OperationTypes.RestoreSnapshot;
}

// True when the operation only produces a visual formatting change — applying marks,
// removing marks, or setting paragraph attributes. The strict-mode latency tracker
// uses this to attribute toolbar-visible-style timings to the right histogram bucket.
export function isFormattingVisualOperation(operationOrType) {
    const type = typeof operationOrType === 'string'
        ? operationOrType
        : (operationOrType && (operationOrType.type || operationOrType.Type) || '');
    return type === OperationTypes.ApplyMark
        || type === OperationTypes.RemoveMark
        || type === OperationTypes.SetParagraphAttribute;
}
