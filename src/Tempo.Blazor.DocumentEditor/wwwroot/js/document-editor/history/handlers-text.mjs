// Phase D — history/handlers-text.mjs
// Factory for the text-manipulation `applyXxx` handlers used by the dispatcher.
// Covers handlers that only need the pure run mutators (insertTextRun /
// deleteTextRange / splitParagraphRuns / splitRunsForRange) and don't touch the
// revision-tracking pipeline.
//
//   `applyInsertText`        — insert text at a target offset using insertTextRun
//   `applyDeleteRangeUntracked` — splice a range out of a paragraph (no revision)
//   `applyMarkOperation`     — toggle a mark across a range
//   `applyMergeParagraph`    — merge a paragraph into the previous one
//
// Note: the legacy `applyDeleteRange` and `applySplitParagraph` also handle the
// tracked-changes (revision) path; that part needs `normalizeRevision` + `addRevision`
// + `setRevisionForRange` + `setRevisionPayloadText`, which still live in the legacy
// IIFE. This factory exposes the untracked-only variants. The dispatcher's caller
// can pick the right handler based on `op.revisionId` presence.

import { asArray, asText, clone } from '../core/helpers.mjs';
import { findBlockContainer } from '../core/model-finders.mjs';
import { blockText, isEditableTextBlock } from '../core/text-helpers.mjs';
import { normalizeTarget, normalizeRange } from '../core/normalize-target.mjs';
import { insertTextRun } from '../core/insert-text-run.mjs';
import {
    deleteTextRange,
    splitRunsForRange,
    setParagraphText,
} from '../core/run-mutators.mjs';
import {
    nextSelectionForOperation,
    operationRegionInfo,
} from '../core/region-info.mjs';
import {
    styleHasValues,
    resolveTypingStyleAtInsertion,
} from '../core/typing-style.mjs';
import { normalizeMarks } from '../core/marks.mjs';

// Factory — requires `findBlock` (index-based lookup) and `revisionById` (so the
// untracked InsertText can attach an existing revision payload if the op carries
// one). The full revision-handling path stays in the legacy IIFE.
export function createTextHandlers(options) {
    const opts = options || {};
    const required = ['findBlock'];
    for (const key of required) {
        if (typeof opts[key] !== 'function') {
            throw new TypeError(`createTextHandlers requires options.${key} (function)`);
        }
    }
    const { findBlock } = opts;
    const revisionById = (typeof opts.revisionById === 'function') ? opts.revisionById : null;

    function applyInsertText(model, op, differ) {
        const target = normalizeTarget(op.target || op.Target);
        const block = findBlock(model, target.blockId);
        if (!block) {
            return {
                ok: false,
                errors: [{ code: 'missing-target-block', path: 'operation.target.blockId', blockId: target.blockId }],
            };
        }
        const inserted = asText(op.text ?? op.Text);
        const marks = normalizeMarks(op.marks || op.Marks || []);
        const explicitStyle = op.style || op.Style || {};
        const style = styleHasValues(explicitStyle)
            ? clone(explicitStyle)
            : resolveTypingStyleAtInsertion(block, target.offset, target.affinity);
        const revisionId = op.revisionId || op.RevisionId || null;
        const revisionPayload = op.revision || op.Revision || null;
        // Caller is responsible for revision-tracking when both revisionId + payload
        // are present; this factory only attaches the existing revision if findBlock
        // says nothing (we keep the model intact regardless).
        if (revisionId && revisionPayload && revisionById && !revisionById(model, revisionId)) {
            if (!Array.isArray(model.revisions)) model.revisions = [];
            model.revisions.push(revisionPayload);
        }
        const attrs = { marks, style, revisionId, affinity: target.affinity };
        if (target.virtualCaret) attrs.commentIds = [];
        insertTextRun(block, target.offset, inserted, attrs);
        const range = { blockId: block.id, start: target.offset, end: target.offset + inserted.length };
        differ.record({ insertedRange: range, invalidatedLayoutScopes: [block.id] });
        return {
            ok: true,
            invalidatedLayoutScopes: [block.id],
            nextSelection: nextSelectionForOperation(model, op, block.id, range.end, target),
        };
    }

    function applyDeleteRangeUntracked(model, op, differ) {
        const range = normalizeRange(op.range || op.Range);
        const block = findBlock(model, range.blockId);
        if (!block) {
            return {
                ok: false,
                errors: [{ code: 'missing-target-block', path: 'operation.range.blockId', blockId: range.blockId }],
            };
        }
        const text = blockText(block);
        const removed = text.slice(range.start, range.end);
        op.deletedText = removed;
        deleteTextRange(block, range.start, range.end);
        differ.record({
            removedRange: { blockId: block.id, start: range.start, end: range.end, text: removed },
            invalidatedLayoutScopes: [block.id],
        });
        return {
            ok: true,
            invalidatedLayoutScopes: [block.id],
            nextSelection: nextSelectionForOperation(model, op, block.id, range.start, range),
        };
    }

    function applyMarkOperation(model, op, differ, remove) {
        const range = normalizeRange(op.range || op.Range);
        const block = findBlock(model, range.blockId);
        if (!block) {
            return {
                ok: false,
                errors: [{ code: 'missing-target-block', path: 'operation.range.blockId', blockId: range.blockId }],
            };
        }
        splitRunsForRange(block, range.start, range.end, op.mark || op.Mark || {}, remove);
        differ.record({
            attributeChange: { blockId: block.id, range, attributeName: 'marks' },
            invalidatedLayoutScopes: [block.id],
            invalidatedOverlayScopes: [block.id],
        });
        return {
            ok: true,
            invalidatedLayoutScopes: [block.id],
            nextSelection: nextSelectionForOperation(model, op, block.id, range.end, range),
        };
    }

    function applyMergeParagraph(model, op, differ) {
        const target = normalizeTarget(op.target || op.Target);
        const container = findBlockContainer(model, target.blockId);
        if (!container) {
            return {
                ok: false,
                errors: [{ code: 'missing-target-block', path: 'operation.target.blockId', blockId: target.blockId }],
            };
        }
        const { index, block } = container;
        const previous = container.blocks[index - 1] || null;
        if (!previous || !isEditableTextBlock(previous) || !isEditableTextBlock(block)) {
            return {
                ok: false,
                errors: [{ code: 'missing-previous-paragraph', path: 'operation.target.blockId', blockId: target.blockId }],
            };
        }
        const offset = blockText(previous).length;
        setParagraphText(previous, blockText(previous) + blockText(block));
        container.blocks.splice(index, 1);
        differ.record({
            removedRange: { blockId: block.id, start: 0, end: blockText(block).length },
            invalidatedLayoutScopes: [previous.id, block.id],
        });
        return {
            ok: true,
            invalidatedLayoutScopes: [previous.id, block.id],
            nextSelection: nextSelectionForOperation(model, op, previous.id, offset,
                operationRegionInfo(model, op, block.id, target)),
        };
    }

    return Object.freeze({
        applyInsertText,
        applyDeleteRangeUntracked,
        applyMarkOperation,
        applyMergeParagraph,
    });
}
