// Phase D — history/handlers-simple.mjs
// Factory for the simpler `applyXxx` handlers used by the apply-operation dispatcher.
// Covers handlers that don't need access to the inline-run pipeline or revision engine:
//   applySetParagraphAttribute — mutate a single paragraph attribute (alignment,
//                                 lineSpacing, …) and record the change
//   applyRestoreSnapshot — wholesale replace the model contents (used by undo/redo
//                          for snapshot-based history)
//
// The more complex handlers (applyInsertText/applyDeleteRange/applyInsertImage/…)
// stay in the legacy IIFE because they need the inline run mutator family
// (`_insertTextRun`, `splitInlineListForDrawingInsert`, …) which is itself ~500
// lines of mutable model operations.

import { asArray } from '../core/helpers.mjs';
import { normalizeTarget } from '../core/normalize-target.mjs';
import { firstModelSelection } from '../core/first-block.mjs';
import { createSelectionSnapshot } from '../core/selection-snapshot.mjs';

// Factory — `createSimpleHandlers({ findBlock, replaceModelContents, nextSelectionForOperation })`
// returns `{ applySetParagraphAttribute, applyRestoreSnapshot }`.
//
// `findBlock(model, blockId)` — lookup helper (model.indexes or scan); the legacy
//                                IIFE's `_findBlock` is the canonical implementation.
// `replaceModelContents(model, snapshot)` — in-place restore (mutates `model` to
//                                            match `snapshot`); from the legacy IIFE.
// `nextSelectionForOperation(model, op, blockId, offset, fallback)` — selection
//                                                                     computation;
//                                                                     from the legacy
//                                                                     IIFE.
export function createSimpleHandlers(options) {
    const opts = options || {};
    const required = ['findBlock', 'replaceModelContents', 'nextSelectionForOperation'];
    for (const key of required) {
        if (typeof opts[key] !== 'function') {
            throw new TypeError(`createSimpleHandlers requires options.${key} (function)`);
        }
    }
    const { findBlock, replaceModelContents, nextSelectionForOperation } = opts;

    function applySetParagraphAttribute(model, op, differ) {
        const target = normalizeTarget(op.target || op.Target);
        const block = findBlock(model, target.blockId);
        if (!block) {
            return {
                ok: false,
                errors: [{ code: 'missing-target-block', path: 'operation.target.blockId', blockId: target.blockId }],
            };
        }
        if (!block.content) block.content = { type: 'paragraph', runs: [] };
        const name = op.attributeName || op.AttributeName;
        op.previousValue = block.content[name];
        block.content[name] = op.value ?? op.Value;
        differ.record({
            attributeChange: {
                blockId: block.id,
                attributeName: name,
                value: block.content[name],
            },
            invalidatedLayoutScopes: [block.id],
        });
        return {
            ok: true,
            invalidatedLayoutScopes: [block.id],
            nextSelection: nextSelectionForOperation(model, op, block.id, target.offset, target),
        };
    }

    function applyRestoreSnapshot(model, op, differ) {
        const snapshot = op.snapshot || op.Snapshot || null;
        if (!snapshot) {
            return {
                ok: false,
                errors: [{ code: 'missing-restore-snapshot', path: 'operation.snapshot' }],
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
    }

    return Object.freeze({ applySetParagraphAttribute, applyRestoreSnapshot });
}
