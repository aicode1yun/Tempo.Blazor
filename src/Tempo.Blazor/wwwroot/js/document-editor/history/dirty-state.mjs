// Phase D — history/dirty-state.mjs
// `createInitialDirtyState()` — fresh dirty-tracking record for an editor instance.
//   `epoch`/`savedEpoch` track unsaved edits; `lastSavedMarker` is the document
//   fingerprint at last save; `pendingPatchCount` counts in-flight DOM patches.
//   Output is `sortObject`-stable.
// `getOperationId(operation)` — extracts an operation's id from any of the
//   `id`/`operationId` Pascal/camel variants, coerced to a string (empty when absent).

import { asText, sortObject } from '../core/helpers.mjs';

export function createInitialDirtyState() {
    return sortObject({
        isDirty: false,
        epoch: 0,
        savedEpoch: 0,
        version: null,
        lastSavedMarker: '',
        lastFailure: null,
        pendingPatchCount: 0,
    });
}

export function getOperationId(operation) {
    return asText(operation && (operation.id || operation.Id
        || operation.operationId || operation.OperationId || ''));
}
