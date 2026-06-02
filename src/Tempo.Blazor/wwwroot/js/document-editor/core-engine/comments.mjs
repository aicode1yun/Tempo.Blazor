// Phase R.4.6g — core-engine/comments.mjs
// Comments anchored to text ranges. A commented range carries a `comment` mark
// ({ type:'comment', value: commentId }) on its runs — multiple comments may overlap the
// same run (each is a distinct mark). Comment metadata (author/text/resolved) lives in
// `model.comments`. Resolving a comment strips its anchor marks (highlight clears) but
// keeps the record; removing drops both. Pure — mutates the model in place.
//
//   COMMENT_MARK
//   addCommentMarkToRange(block, start, end, commentId)
//   stripCommentMark(model, commentId)        → removes the anchor marks everywhere
//   commentIdsInRange(block, start, end)      → comment ids covering the range
//   commentAnchorText(model, commentId)       → the commented text (concatenated)
//   collectCommentIds(model)                  → all anchored comment ids present

import { asArray } from '../core/helpers.mjs';
import { markType, markValue, updateMarks } from '../core/marks.mjs';
import { transformRunsInRange } from './edit-format.mjs';

export const COMMENT_MARK = 'comment';

function commentIdsOnRun(run) {
    return asArray(run && run.marks)
        .filter(function (m) { return markType(m) === COMMENT_MARK; })
        .map(function (m) { return markValue(m); })
        .filter(Boolean);
}

function eachParagraph(model, visit) {
    asArray(model && model.body && model.body.blocks).forEach(function walk(block) {
        if (!block) return;
        if (block.type === 'paragraph') { visit(block); return; }
        if (block.type === 'table') {
            asArray(block.content && block.content.rows).forEach(function (row) {
                asArray(row.cells).forEach(function (cell) { asArray(cell.blocks).forEach(walk); });
            });
        }
    });
}

// Appends a comment anchor mark across [start,end) — additive, so overlapping comments
// coexist on the same run.
export function addCommentMarkToRange(block, start, end, commentId) {
    if (!block || block.type !== 'paragraph') return;
    transformRunsInRange(block, start, end, function (run) {
        run.marks = updateMarks(run.marks, { type: COMMENT_MARK, value: commentId }, false);
    });
}

export function stripCommentMark(model, commentId) {
    let changed = false;
    eachParagraph(model, function (block) {
        asArray(block.content && block.content.runs).forEach(function (run) {
            if (commentIdsOnRun(run).indexOf(commentId) !== -1) {
                run.marks = asArray(run.marks).filter(function (m) {
                    return !(markType(m) === COMMENT_MARK && markValue(m) === commentId);
                });
                changed = true;
            }
        });
    });
    return changed;
}

export function commentIdsInRange(block, start, end) {
    const lo = Math.min(start, end);
    const hi = Math.max(start, end);
    const ids = [];
    let cursor = 0;
    asArray(block && block.content && block.content.runs).forEach(function (run) {
        const len = String(run.text == null ? '' : run.text).length;
        const rs = cursor; const re = cursor + len; cursor = re;
        const overlaps = (hi > lo) ? (re > lo && rs < hi) : (lo > rs && lo <= re);
        if (overlaps) commentIdsOnRun(run).forEach(function (id) { if (ids.indexOf(id) === -1) ids.push(id); });
    });
    return ids;
}

export function commentAnchorText(model, commentId) {
    let text = '';
    eachParagraph(model, function (block) {
        asArray(block.content && block.content.runs).forEach(function (run) {
            if (commentIdsOnRun(run).indexOf(commentId) !== -1) text += String(run.text == null ? '' : run.text);
        });
    });
    return text;
}

export function collectCommentIds(model) {
    const ids = [];
    eachParagraph(model, function (block) {
        asArray(block.content && block.content.runs).forEach(function (run) {
            commentIdsOnRun(run).forEach(function (id) { if (ids.indexOf(id) === -1) ids.push(id); });
        });
    });
    return ids;
}
