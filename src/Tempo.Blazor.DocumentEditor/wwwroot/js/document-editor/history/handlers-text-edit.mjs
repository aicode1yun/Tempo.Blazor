// Phase D — history/handlers-text-edit.mjs
// `createTextEditHandlers(deps)` → `{ applyInsertText, applyDeleteRange }`.
// These are the two hottest history operation handlers.
//
// applyInsertText(model, op, differ):
//   • resolves the typing style at the insertion point (explicit op.style wins),
//   • registers an inline insertion revision when op.revision/revisionId is given,
//   • inserts the text run and records the inserted range on the differ,
//   • returns the next caret selection just past the inserted text.
//
// applyDeleteRange(model, op, differ):
//   • tracked path (op.revision/revisionId present): records a Pending Deletion
//     revision over the range, leaving the text in place, and records overlay +
//     marker changes,
//   • untracked path: removes the text and records the removed range.
//   Either way `op.deletedText` is stamped with the removed substring.
//
// All collaborators are injected so this module stays free of the engine graph.

const REQUIRED = [
    'normalizeTarget', 'normalizeRange', 'findBlock', 'blockText', 'asText',
    'clone', 'sortObject', 'normalizeMarks', 'styleHasValues',
    'resolveTypingStyleAtInsertion', 'revisionById', 'insertTextRun',
    'deleteTextRange', 'nextSelectionForOperation', 'normalizeRevision',
    'normalizeRevisionRange', 'setRevisionPayloadText', 'addRevision',
    'setRevisionForRange',
];

export function createTextEditHandlers(deps) {
    const opts = deps || {};
    for (const key of REQUIRED) {
        if (typeof opts[key] !== 'function') {
            throw new TypeError(
                `createTextEditHandlers requires options.${key} (function)`);
        }
    }
    const {
        normalizeTarget, normalizeRange, findBlock, blockText, asText,
        clone, sortObject, normalizeMarks, styleHasValues,
        resolveTypingStyleAtInsertion, revisionById, insertTextRun,
        deleteTextRange, nextSelectionForOperation, normalizeRevision,
        normalizeRevisionRange, setRevisionPayloadText, addRevision,
        setRevisionForRange,
    } = opts;

    function applyInsertText(model, op, differ) {
        const target = normalizeTarget(op.target || op.Target);
        const block = findBlock(model, target.blockId);
        const inserted = asText(op.text ?? op.Text);
        const marks = normalizeMarks(op.marks || op.Marks || []);
        const explicitStyle = op.style || op.Style || {};
        const style = styleHasValues(explicitStyle)
            ? clone(explicitStyle)
            : resolveTypingStyleAtInsertion(block, target.offset, target.affinity);
        const revisionId = op.revisionId || op.RevisionId || null;
        const revisionPayload = op.revision || op.Revision || null;
        if (revisionId && revisionPayload && !revisionById(model, revisionId)) {
            if (!Array.isArray(model.revisions)) model.revisions = [];
            model.revisions.push(sortObject(revisionPayload));
        }
        const attrs = { marks, style, revisionId, affinity: target.affinity };
        if (target.virtualCaret) attrs.commentIds = [];
        insertTextRun(block, target.offset, inserted, attrs);
        const range = {
            blockId: block.id,
            start: target.offset,
            end: target.offset + inserted.length,
        };
        differ.record({ insertedRange: range, invalidatedLayoutScopes: [block.id] });
        return {
            ok: true,
            invalidatedLayoutScopes: [block.id],
            nextSelection: nextSelectionForOperation(model, op, block.id, range.end, target),
        };
    }

    function applyDeleteRange(model, op, differ) {
        const range = normalizeRange(op.range || op.Range);
        const block = findBlock(model, range.blockId);
        const text = blockText(block);
        const removed = text.slice(range.start, range.end);
        op.deletedText = removed;
        let revisionId = op.revisionId || op.RevisionId || null;
        const revisionPayload = op.revision || op.Revision || null;
        if (revisionId || revisionPayload) {
            const deletionRevision = normalizeRevision(revisionPayload || {
                id: revisionId,
                type: 'Deletion',
                status: 'Pending',
                affectedRange: range,
                payload: { text: removed },
                payloadJson: removed,
            });
            revisionId = revisionId || deletionRevision.id;
            deletionRevision.id = revisionId;
            deletionRevision.type = 'Deletion';
            deletionRevision.status = 'Pending';
            deletionRevision.affectedRange = normalizeRevisionRange(
                Object.assign({}, deletionRevision.affectedRange || {}, range));
            deletionRevision.range = deletionRevision.affectedRange;
            if (!revisionPayload
                || (!revisionPayload.payload && !revisionPayload.Payload)) {
                setRevisionPayloadText(deletionRevision, removed);
            }
            addRevision(model, deletionRevision);
            setRevisionForRange(model, revisionId, range);
            op.revisionId = revisionId;
            op.revision = clone(deletionRevision);
            op.trackedDeletion = true;
            differ.record({
                markerChange: { revisionId, status: 'Pending', type: 'Deletion' },
                removedRange: {
                    blockId: block.id, start: range.start, end: range.end,
                    text: removed, tracked: true,
                },
                invalidatedLayoutScopes: [block.id],
                invalidatedOverlayScopes: ['revisions', block.id],
            });
            return {
                ok: true,
                invalidatedLayoutScopes: [block.id],
                nextSelection: nextSelectionForOperation(model, op, block.id, range.start, range),
            };
        }
        deleteTextRange(block, range.start, range.end);
        differ.record({
            removedRange: {
                blockId: block.id, start: range.start, end: range.end, text: removed,
            },
            invalidatedLayoutScopes: [block.id],
        });
        return {
            ok: true,
            invalidatedLayoutScopes: [block.id],
            nextSelection: nextSelectionForOperation(model, op, block.id, range.start, range),
        };
    }

    return Object.freeze({ applyInsertText, applyDeleteRange });
}
