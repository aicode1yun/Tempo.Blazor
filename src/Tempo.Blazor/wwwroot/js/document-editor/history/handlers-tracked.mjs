// Phase D — history/handlers-tracked.mjs
// Tracked-changes (revisions) variants of `applyDeleteRange` and `applySplitParagraph`.
// Factory pattern: takes the run mutators + revision-helpers + region info that the
// tracked path needs, injected from `core/run-mutators.mjs` + `history/revision-helpers.mjs`.
//
// The dispatcher's caller checks `op.revisionId || op.revision` to decide between
// the untracked variant (from `handlers-text` / `handlers-split`) and these tracked
// variants.

import { asText, clone } from '../core/helpers.mjs';
import { blockText } from '../core/text-helpers.mjs';
import { normalizeTarget, normalizeRange } from '../core/normalize-target.mjs';
import { normalizeRevisionRange } from '../core/revision-normalize.mjs';

export function createTrackedHandlers(options) {
    const opts = options || {};
    const required = [
        'findBlock',
        'findBlockContainer',
        'normalizeRevision',
        'addRevision',
        'setRevisionForRange',
        'setRevisionPayloadText',
        'splitParagraphRuns',
        'importBlock',
        'nextSelectionForOperation',
        'operationRegionInfo',
    ];
    for (const key of required) {
        if (typeof opts[key] !== 'function') {
            throw new TypeError(`createTrackedHandlers requires options.${key} (function)`);
        }
    }
    const {
        findBlock,
        findBlockContainer,
        normalizeRevision,
        addRevision,
        setRevisionForRange,
        setRevisionPayloadText,
        splitParagraphRuns,
        importBlock,
        nextSelectionForOperation,
        operationRegionInfo,
    } = opts;

    function stableId(prefix, path) {
        return String(prefix || 'id') + '-' + String(path || '0').replace(/[^a-z0-9_-]+/gi, '-');
    }

    // Tracked-deletion path: instead of splicing text, stamps a Deletion revision
    // across the range. The text stays in the document but renders strikethrough via
    // the revision overlay layer.
    function applyDeleteRangeTracked(model, op, differ) {
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

        let revisionId = op.revisionId || op.RevisionId || null;
        const revisionPayload = op.revision || op.Revision || null;
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

    // Tracked-split path: split the paragraph as usual, but also stamp a Structure
    // revision so the change can be reviewed.
    function applySplitParagraphTracked(model, op, differ) {
        const target = normalizeTarget(op.target || op.Target);
        const container = findBlockContainer(model, target.blockId);
        if (!container) {
            return {
                ok: false,
                errors: [{ code: 'missing-target-block', path: 'operation.target.blockId', blockId: target.blockId }],
            };
        }
        const block = container.block;
        const splitRuns = splitParagraphRuns(block, target.offset);
        const newBlock = importBlock({
            Id: op.newBlockId || op.NewBlockId
                || stableId('block', block.id + '-split-' + Date.now()),
            Type: 'Paragraph',
            Content: {
                Inlines: splitRuns.after,
                Alignment: block.content && block.content.alignment,
                LineSpacing: block.content && block.content.lineSpacing,
                Style: clone((block.content && block.content.style) || {}),
            },
            Style: clone(block.style || {}),
        }, block.id + '-split');

        block.content.runs = splitRuns.before;
        container.blocks.splice(container.index + 1, 0, newBlock);

        let revisionId = op.revisionId || op.RevisionId || null;
        const revisionPayload = op.revision || op.Revision || null;
        const splitRevision = normalizeRevision(revisionPayload || {
            id: revisionId,
            type: 'Structure',
            status: 'Pending',
            affectedRange: { blockId: block.id, start: target.offset, end: target.offset },
            payload: { text: 'SplitBlock' },
            payloadJson: 'SplitBlock',
        });
        revisionId = revisionId || splitRevision.id;
        splitRevision.id = revisionId;
        splitRevision.type = 'Structure';
        splitRevision.status = 'Pending';
        splitRevision.affectedRange = normalizeRevisionRange(splitRevision.affectedRange
            || { blockId: block.id, start: target.offset, end: target.offset });
        splitRevision.range = splitRevision.affectedRange;
        addRevision(model, splitRevision);
        op.revisionId = revisionId;
        op.revision = clone(splitRevision);

        differ.record({
            insertedRange: { blockId: newBlock.id, start: 0, end: blockText(newBlock).length },
            invalidatedLayoutScopes: [block.id, newBlock.id],
        });
        differ.record({
            markerChange: { revisionId, status: 'Pending', type: 'Structure' },
            invalidatedOverlayScopes: ['revisions'],
        });

        return {
            ok: true,
            invalidatedLayoutScopes: [block.id, newBlock.id],
            nextSelection: nextSelectionForOperation(model, op, newBlock.id, 0,
                operationRegionInfo(model, op, block.id, target)),
            insertedBlockId: newBlock.id,
        };
    }

    return Object.freeze({ applyDeleteRangeTracked, applySplitParagraphTracked });
}
