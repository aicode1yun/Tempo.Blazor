// Phase D — history/revision-merge.mjs
// Revision-mergeability predicates + run-level rewriters used when normalising
// the revision list (deduplicating contiguous same-author/same-formatting
// edits into a single revision).
//
//   - `revisionAuthorMergeKey(revision)` — author id key for merge equality.
//   - `revisionRunFormattingMergeKey(run)` — formatting fingerprint excluding the
//     revision mark itself.
//   - `canMergeAdjacentRevisionRuns(...)` — predicate combining status/type/author
//     /formatting + contiguity.
//   - `replaceRevisionIdOnRun(run, sourceId, targetId)` — rewrites both `run.revisionId`
//     and any revision marks pointing at `sourceId`.

import { asArray, asText, clone } from '../core/helpers.mjs';
import {
    markType,
    normalizeMarks,
    readRevisionIdFromMark,
} from '../core/marks.mjs';
import { stableRevisionStringify } from './track-changes.mjs';
import {
    readRevisionStatus,
    readRevisionTypeName,
} from './revision-helpers.mjs';

export function revisionAuthorMergeKey(revision) {
    const author = (revision && (revision.authorObject || revision.Author || revision.author)) || {};
    return asText(
        author.Id || author.id
        || (revision && (revision.authorId || revision.AuthorId
            || revision.author || revision.Author))
        || author.DisplayName || author.displayName
        || '');
}

export function revisionRunFormattingMergeKey(run) {
    const marks = normalizeMarks(asArray(run && (run.marks || run.Marks)).filter(function (mark) {
        return markType(mark) !== 'revision';
    }));
    return stableRevisionStringify({
        commentIds: asArray(run && (run.commentIds || run.CommentIds)).map(asText).sort(),
        marks: marks,
        style: run && (run.style || run.Style) || {},
    });
}

export function canMergeAdjacentRevisionRuns(
    leftRevision, rightRevision, leftRun, rightRun, leftEnd, rightStart) {
    if (!leftRevision || !rightRevision || leftRevision.id === rightRevision.id) return false;
    if (Number(leftEnd || 0) !== Number(rightStart || 0)) return false;
    if (readRevisionStatus(leftRevision) !== 'Pending'
        || readRevisionStatus(rightRevision) !== 'Pending') return false;
    if (readRevisionTypeName(leftRevision) !== readRevisionTypeName(rightRevision)) return false;
    if (revisionAuthorMergeKey(leftRevision) !== revisionAuthorMergeKey(rightRevision)) return false;
    return revisionRunFormattingMergeKey(leftRun) === revisionRunFormattingMergeKey(rightRun);
}

export function replaceRevisionIdOnRun(run, sourceId, targetId) {
    if (!run) return;
    if (run.revisionId === sourceId || run.RevisionId === sourceId) {
        run.revisionId = targetId;
        delete run.RevisionId;
    }
    run.marks = normalizeMarks(asArray(run.marks || run.Marks).map(function (mark) {
        if (readRevisionIdFromMark(mark) !== sourceId) return mark;
        const next = clone(mark);
        next.revisionId = targetId;
        next.RevisionId = targetId;
        return next;
    }));
    delete run.Marks;
}
