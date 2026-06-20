// Phase D — history/handlers-split.mjs
// `applySplitParagraph` handler (untracked variant). Splits the current paragraph at
// `target.offset`, creating a new paragraph after the current one with the
// after-runs. The new block id can be specified via `op.newBlockId`.
//
// Factory pattern: takes `findBlockContainer` (model walker), `splitParagraphRuns`
// (the pure run splitter), `importBlock` (block normaliser), `nextSelectionForOperation`
// (post-op selection compute), `operationRegionInfo` (region context).
//
// Tracked-changes (revision) path stays in the legacy IIFE because it needs
// `normalizeRevision` + `addRevision` (id-generating non-determinism). The dispatcher
// caller can use the legacy path when `op.revisionId` is present.

import { asArray, clone } from '../core/helpers.mjs';
import { blockText } from '../core/text-helpers.mjs';
import { normalizeTarget } from '../core/normalize-target.mjs';

function stableId(prefix, path) {
    return String(prefix || 'id') + '-' + String(path || '0').replace(/[^a-z0-9_-]+/gi, '-');
}

export function createSplitHandler(options) {
    const opts = options || {};
    const required = [
        'findBlockContainer',
        'splitParagraphRuns',
        'importBlock',
        'nextSelectionForOperation',
        'operationRegionInfo',
    ];
    for (const key of required) {
        if (typeof opts[key] !== 'function') {
            throw new TypeError(`createSplitHandler requires options.${key} (function)`);
        }
    }
    const {
        findBlockContainer,
        splitParagraphRuns,
        importBlock,
        nextSelectionForOperation,
        operationRegionInfo,
    } = opts;

    function applySplitParagraph(model, op, differ) {
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

        differ.record({
            insertedRange: { blockId: newBlock.id, start: 0, end: blockText(newBlock).length },
            invalidatedLayoutScopes: [block.id, newBlock.id],
        });

        return {
            ok: true,
            invalidatedLayoutScopes: [block.id, newBlock.id],
            nextSelection: nextSelectionForOperation(model, op, newBlock.id, 0,
                operationRegionInfo(model, op, block.id, target)),
            insertedBlockId: newBlock.id,
        };
    }

    return Object.freeze({ applySplitParagraph });
}
