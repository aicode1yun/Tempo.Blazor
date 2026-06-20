// Phase D — core/anchor-ranges.mjs
// `createAnchorRanges({asArray, asText, findBlock, blockText,
//   resolveInlineRunDisplayText})` →
//   `{blockOffsetFromInlineIndex, rangeFromCommentAnchor, rangeFromRevision}`.
//
// • blockOffsetFromInlineIndex(block, inlineIndex, offset) — converts an
//   inline-index + intra-inline offset into a flat character offset within the
//   block (sums display-text length of preceding runs).
// • rangeFromCommentAnchor(model, comment) — resolves a comment's anchor into a
//   `{startBlockId, endBlockId, startOffset, endOffset}` range, accepting either
//   inline-index anchors or flat offsets (Pascal/camel + Start/StartText variants).
//   Offsets are clamped to the block's text length; empty ranges extend to length.
// • rangeFromRevision(model, revision) — same for a revision's range/affectedRange.

const REQUIRED = ['asArray', 'asText', 'findBlock', 'blockText', 'resolveInlineRunDisplayText'];

export function createAnchorRanges(options) {
    const opts = options || {};
    for (const key of REQUIRED) {
        if (typeof opts[key] !== 'function') {
            throw new TypeError(`createAnchorRanges requires options.${key} (function)`);
        }
    }
    const { asArray, asText, findBlock, blockText, resolveInlineRunDisplayText } = opts;

    function blockOffsetFromInlineIndex(block, inlineIndex, offset) {
        const runs = asArray(block && block.content && block.content.runs);
        const index = Math.max(0, Math.min(runs.length - 1, Number(inlineIndex || 0) || 0));
        let total = 0;
        for (let i = 0; i < index; i++) total += resolveInlineRunDisplayText(runs[i]).length;
        return total + Math.max(0, Number(offset || 0) || 0);
    }

    function resolveAnchorRange(model, blockIdFields) {
        const blockId = asText(blockIdFields.blockId || '');
        if (!blockId) return null;
        const block = findBlock(model, blockId);
        const length = blockText(block).length;
        const hasInlineStart = blockIdFields.startInlineIndex !== undefined;
        const hasInlineEnd = blockIdFields.endInlineIndex !== undefined;
        let start = hasInlineStart
            ? blockOffsetFromInlineIndex(block, blockIdFields.startInlineIndex, blockIdFields.startOffset ?? 0)
            : Number(blockIdFields.startOffset ?? 0) || 0;
        let end = hasInlineEnd
            ? blockOffsetFromInlineIndex(block, blockIdFields.endInlineIndex, blockIdFields.endOffset ?? length)
            : Number(blockIdFields.endOffset ?? length) || length;
        if (end <= start && length > start) end = length;
        return {
            startBlockId: blockId,
            endBlockId: asText(blockIdFields.endBlockId || blockId),
            startOffset: Math.max(0, Math.min(start, length)),
            endOffset: Math.max(0, Math.min(Math.max(start, end), length)),
        };
    }

    function rangeFromCommentAnchor(model, comment) {
        const anchor = (comment && (comment.anchor || comment.Anchor)) || {};
        return resolveAnchorRange(model, {
            blockId: anchor.BlockId || anchor.blockId || anchor.StartBlockId || anchor.startBlockId || '',
            endBlockId: anchor.EndBlockId || anchor.endBlockId || '',
            startInlineIndex: (anchor.StartInlineIndex !== undefined || anchor.startInlineIndex !== undefined)
                ? (anchor.StartInlineIndex ?? anchor.startInlineIndex)
                : undefined,
            endInlineIndex: (anchor.EndInlineIndex !== undefined || anchor.endInlineIndex !== undefined)
                ? (anchor.EndInlineIndex ?? anchor.endInlineIndex)
                : undefined,
            startOffset: anchor.StartOffset ?? anchor.startOffset
                ?? anchor.StartTextOffset ?? anchor.startTextOffset,
            endOffset: anchor.EndOffset ?? anchor.endOffset
                ?? anchor.EndTextOffset ?? anchor.endTextOffset,
        });
    }

    function rangeFromRevision(model, revision) {
        const source = revision || {};
        const revisionRange = source.range || source.Range
            || source.affectedRange || source.AffectedRange || {};
        return resolveAnchorRange(model, {
            blockId: revisionRange.BlockId || revisionRange.blockId
                || revisionRange.StartBlockId || revisionRange.startBlockId || '',
            endBlockId: revisionRange.EndBlockId || revisionRange.endBlockId || '',
            startInlineIndex: (revisionRange.StartInlineIndex !== undefined
                || revisionRange.startInlineIndex !== undefined)
                ? (revisionRange.StartInlineIndex ?? revisionRange.startInlineIndex)
                : undefined,
            endInlineIndex: (revisionRange.EndInlineIndex !== undefined
                || revisionRange.endInlineIndex !== undefined)
                ? (revisionRange.EndInlineIndex ?? revisionRange.endInlineIndex)
                : undefined,
            startOffset: revisionRange.StartOffset ?? revisionRange.startOffset ?? revisionRange.start,
            endOffset: revisionRange.EndOffset ?? revisionRange.endOffset ?? revisionRange.end,
        });
    }

    return Object.freeze({ blockOffsetFromInlineIndex, rangeFromCommentAnchor, rangeFromRevision });
}
