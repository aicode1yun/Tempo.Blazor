// Phase D — core/comment-resolver.mjs
// `commentIdsAtInsertionOffset(block, offset)` — when inserting at a character offset
// inside a paragraph, which existing comments should the new text inherit?
// Returns only the intersection of "comments on the left run" and "comments on the
// right run" — i.e. comments that span both sides of the insertion point. This
// prevents typing inside a comment from accidentally extending into uncommented
// text on either side.
//
// Pure function.

import { asArray, unique } from './helpers.mjs';
import { blockText } from './text-helpers.mjs';
import { readCommentIdsFromRun } from './marks.mjs';
import { resolveInlineRunDisplayText } from '../render/run-text.mjs';

export function commentIdsAtInsertionOffset(block, offset) {
    if (!block || block.type !== 'paragraph') return [];
    const target = Math.max(0, Math.min(blockText(block).length, Number(offset || 0) || 0));
    let leftIds = [];
    let rightIds = [];
    let cursor = 0;
    asArray(block.content && block.content.runs).forEach(run => {
        const runText = resolveInlineRunDisplayText(run);
        const runStart = cursor;
        const runEnd = cursor + runText.length;
        cursor = runEnd;
        if (runEnd <= runStart) return;
        const runCommentIds = readCommentIdsFromRun(run);
        if (runCommentIds.length === 0) return;
        if (target > runStart && target <= runEnd) {
            leftIds = unique(leftIds.concat(runCommentIds));
        }
        if (target >= runStart && target < runEnd) {
            rightIds = unique(rightIds.concat(runCommentIds));
        }
    });
    return unique(leftIds.filter(commentId => rightIds.indexOf(commentId) >= 0)).sort();
}
