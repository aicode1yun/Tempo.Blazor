// Phase D — history/handlers-restore-snapshot.mjs
// `createRestoreSnapshotHandler({replaceModelContents, createSelectionSnapshot, firstModelSelection})`
// factory → `applyRestoreSnapshot(model, op, differ)` — handles the RestoreSnapshot
// operation by wholesale-replacing model contents via `replaceModelContents` and
// emitting a 'restore-snapshot' diff entry. Records nextSelection from op.selection
// or falls back to first-model-selection.

import { asArray } from '../core/helpers.mjs';

export function createRestoreSnapshotHandler(options) {
    const opts = options || {};
    const required = [
        'replaceModelContents',
        'createSelectionSnapshot',
        'firstModelSelection',
    ];
    for (const key of required) {
        if (typeof opts[key] !== 'function') {
            throw new TypeError(
                `createRestoreSnapshotHandler requires options.${key} (function)`);
        }
    }
    const {
        replaceModelContents,
        createSelectionSnapshot,
        firstModelSelection,
    } = opts;

    return function applyRestoreSnapshot(model, op, differ) {
        const snapshot = op.snapshot || op.Snapshot || null;
        if (!snapshot) {
            return {
                ok: false,
                errors: [{
                    code: 'missing-restore-snapshot',
                    path: 'operation.snapshot',
                }],
            };
        }
        replaceModelContents(model, snapshot);
        const scopes = asArray(op.affectedScopeIds || op.AffectedScopeIds || ['document']);
        differ.record({
            objectChange: { blockId: 'document', type: 'restore-snapshot' },
            invalidatedLayoutScopes: scopes,
            invalidatedOverlayScopes: scopes,
        });
        return {
            ok: true,
            invalidatedLayoutScopes: scopes,
            nextSelection: createSelectionSnapshot(
                op.selection || op.Selection || firstModelSelection(model)),
        };
    };
}
