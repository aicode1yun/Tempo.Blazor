// Phase D — core/insert-text-run.mjs
// `insertTextRun(block, offset, text, attributes)` — mutating insert of text at a
// paragraph offset. Splits the existing run at the insertion point (if needed),
// inserts a new run with `attributes.marks/style/revisionId`, inherits comments via
// `commentIdsAtInsertionOffset`, and merges adjacent runs that share styling.
//
// Pure mutator — modifies `block.content.runs` in place. Used by `applyInsertText`.

import { asArray, asText, clone, hasOwn, sortObject, unique } from './helpers.mjs';
import { normalizeMarks } from './marks.mjs';
import {
    isDrawingRunSource,
    normalizeDrawingRun,
    normalizeTextRunForMerge,
    mergeAdjacentTextRuns,
} from './inline-runs.mjs';
import { commentIdsAtInsertionOffset } from './comment-resolver.mjs';

function stableId(prefix, path) {
    return String(prefix || 'id') + '-' + String(path || '0').replace(/[^a-z0-9_-]+/gi, '-');
}

export function insertTextRun(block, offset, text, attributes) {
    if (!block.content) block.content = { type: 'paragraph', runs: [] };
    const attrs = attributes || {};
    const targetOffset = Math.max(0, Number(offset || 0) || 0);
    const affinity = (attrs.affinity === 'before' || attrs.Affinity === 'before') ? 'before' : 'after';
    const hasExplicitCommentIds = hasOwn(attrs, 'commentIds') || hasOwn(attrs, 'CommentIds');
    const inheritedCommentIds = hasExplicitCommentIds
        ? unique(asArray(attrs.commentIds || attrs.CommentIds).map(asText).filter(Boolean)).sort()
        : commentIdsAtInsertionOffset(block, targetOffset);

    function createInsertedRun(index) {
        return sortObject(Object.assign({
            id: attrs.id || stableId('inline',
                block.id + '-insert-' + Date.now() + (index === undefined ? '' : '-' + index)),
            kind: 'text',
            text: asText(text),
            marks: normalizeMarks(attrs.marks || []),
            style: clone(attrs.style || {}),
        }, inheritedCommentIds.length ? { commentIds: inheritedCommentIds } : {},
            attrs.revisionId ? { revisionId: attrs.revisionId } : {}));
    }

    const result = [];
    let cursor = 0;
    let inserted = false;

    asArray(block.content.runs).forEach((run, index) => {
        if (run && (run.kind === 'drawing' || isDrawingRunSource(run))) {
            const drawing = normalizeDrawingRun(run,
                run.id || run.objectId || ((block.id || 'block') + '-drawing-' + index));
            if (!inserted && targetOffset === cursor) {
                if (affinity === 'before') {
                    result.push(createInsertedRun(index));
                    result.push(drawing);
                } else {
                    result.push(drawing);
                    result.push(createInsertedRun(index));
                }
                inserted = true;
                return;
            }
            result.push(drawing);
            return;
        }

        const runText = asText(run.text);
        const runStart = cursor;
        const runEnd = cursor + runText.length;
        cursor = runEnd;

        if (!inserted && targetOffset >= runStart && targetOffset <= runEnd) {
            const local = Math.max(0, Math.min(runText.length, targetOffset - runStart));
            if (local > 0) {
                const before = clone(run);
                before.id = run.id + '-before';
                before.text = runText.slice(0, local);
                result.push(normalizeTextRunForMerge(before));
            }
            result.push(createInsertedRun(index));
            if (local < runText.length) {
                const after = clone(run);
                after.id = run.id + '-after';
                after.text = runText.slice(local);
                result.push(normalizeTextRunForMerge(after));
            }
            inserted = true;
        } else {
            result.push(normalizeTextRunForMerge(run));
        }
    });

    if (!inserted) {
        result.push(createInsertedRun());
    }

    block.content.runs = mergeAdjacentTextRuns(result);
}
