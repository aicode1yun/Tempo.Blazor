// Phase D — core/marks.mjs
// Inline-mark normalisers, sort/dedup logic, and metadata readers (comments, revisions).
// All pure (no closure over engine state) — operate only on plain mark/run records.
//
// Mirrors the inline implementations in the legacy IIFE so the bundled engine and the
// monolith produce byte-identical mark arrays after normalisation.

import { asArray, asText, clone, sortObject, unique } from './helpers.mjs';

// Canonical numeric order of mark types, used for both numeric → name resolution and
// stable sort priority. Mirrors the legacy IIFE list exactly.
const MARK_TYPE_NAMES = Object.freeze([
    'bold',
    'italic',
    'underline',
    'strikethrough',
    'superscript',
    'subscript',
    'link',
    'commentanchor',
    'revision',
    'highlight',
    'textcolor',
    'fontfamily',
    'fontsize',
]);

// Canonical lowercase mark name. Accepts numeric ordinal or string (any casing/spacing).
export function markType(mark) {
    const raw = mark && (mark.type ?? mark.Type);
    if (typeof raw === 'number' && Number.isInteger(raw) && raw >= 0 && raw < MARK_TYPE_NAMES.length) {
        return MARK_TYPE_NAMES[raw];
    }
    return String(raw ?? '').replace(/\s+/g, '').toLowerCase();
}

// Generic value accessor — handles `value`, `color`, and `href` (Pascal+camel).
export function markValue(mark) {
    return mark
        && (mark.value
            ?? mark.Value
            ?? mark.color
            ?? mark.Color
            ?? mark.href
            ?? mark.Href
            ?? null);
}

// Sort priority for two marks. Numeric `type` wins by ordinal; string `type` falls back
// to the position in MARK_TYPE_NAMES; unknown → 999.
export function markOrderValue(mark) {
    const raw = mark && (mark.type ?? mark.Type);
    if (typeof raw === 'number' && Number.isFinite(raw)) return raw;
    const type = markType(mark);
    const order = MARK_TYPE_NAMES.indexOf(type);
    return order >= 0 ? order : 999;
}

// Clone + sortObject. The resulting object has all keys sorted alphabetically and any
// hidden `__dom`/`__runtime`/`_runtime` keys stripped (per sortObject's contract).
export function normalizeMark(mark) {
    return sortObject(clone(mark || {}));
}

// Stable string key for dedup. Two marks with the same normalised JSON are considered
// duplicates by normalizeMarks below.
export function markKey(mark) {
    return JSON.stringify(normalizeMark(mark));
}

// Sort key for stable ordering — combines (orderValue, value, revisionId, commentId, key).
// The unit-separator () avoids collisions between fields.
export function markSortKey(mark) {
    const normalized = normalizeMark(mark);
    return [
        String(markOrderValue(normalized)).padStart(3, '0'),
        String(markValue(normalized) ?? ''),
        String(normalized.revisionId || normalized.RevisionId || ''),
        String(normalized.commentId || normalized.CommentId || ''),
        markKey(normalized),
    ].join('');
}

// Normalise an array of marks: clone each, sort by markSortKey, drop exact duplicates.
export function normalizeMarks(marks) {
    const seen = new Set();
    return asArray(marks)
        .map(normalizeMark)
        .sort((left, right) => {
            const leftKey = markSortKey(left);
            const rightKey = markSortKey(right);
            return leftKey < rightKey ? -1 : (leftKey > rightKey ? 1 : 0);
        })
        .filter(mark => {
            const key = markKey(mark);
            if (seen.has(key)) return false;
            seen.add(key);
            return true;
        });
}

// Add or remove a specific mark from an array, then re-normalise.
// `remove === true` drops every mark with the same key; otherwise the mark is appended.
export function updateMarks(marks, mark, remove) {
    const source = normalizeMarks(marks);
    const key = markKey(mark);
    const without = source.filter(item => markKey(item) !== key);
    if (remove) return normalizeMarks(without);
    without.push(normalizeMark(mark || {}));
    return normalizeMarks(without);
}

// ────────────────────────────────────────────────────────────────────────────────
// Inline mark type readers — extract specific semantic kinds (CommentAnchor, Revision)
// out of a mark or an inline run.
// ────────────────────────────────────────────────────────────────────────────────

// Detect well-known anchor kinds. CommentAnchor=7 and Revision=8 are numeric in the
// wire format but may also arrive as string identifiers (e.g. from clipboard imports).
export function readInlineMarkType(mark) {
    const value = mark && (mark.type ?? mark.Type);
    if (value === 7) return 'CommentAnchor';
    if (value === 8) return 'Revision';
    const key = asText(value).replace(/[^a-z]/gi, '').toLowerCase();
    if (key === 'commentanchor') return 'CommentAnchor';
    if (key === 'revision' || key === 'revisionanchor') return 'Revision';
    return asText(value);
}

export function readCommentIdFromMark(mark) {
    if (readInlineMarkType(mark) !== 'CommentAnchor') return '';
    const anchor = (mark && (mark.CommentAnchor || mark.commentAnchor)) || {};
    return asText(anchor.CommentId || anchor.commentId || mark.CommentId || mark.commentId || '');
}

export function readCommentIdsFromRun(run) {
    const ids = asArray(run && (run.commentIds || run.CommentIds)).map(asText).filter(Boolean);
    asArray(run && (run.marks || run.Marks)).forEach(mark => {
        const id = readCommentIdFromMark(mark);
        if (id && ids.indexOf(id) < 0) ids.push(id);
    });
    return ids;
}

export function readRevisionIdFromMark(mark) {
    if (readInlineMarkType(mark) !== 'Revision') return '';
    return asText(mark && (mark.revisionId || mark.RevisionId || mark.value || mark.Value || ''));
}

export function readRevisionIdFromMarks(marks) {
    let id = '';
    asArray(marks).some(mark => {
        id = readRevisionIdFromMark(mark);
        return !!id;
    });
    return id;
}

export function readRevisionIdsFromRun(run) {
    const ids = [];
    const direct = asText(run && (run.revisionId || run.RevisionId || ''));
    if (direct) ids.push(direct);
    asArray(run && (run.marks || run.Marks)).forEach(mark => {
        const id = readRevisionIdFromMark(mark);
        if (id && ids.indexOf(id) < 0) ids.push(id);
    });
    return ids;
}

// Re-export the canonical name list as a frozen array for callers that need to
// enumerate the supported marks (e.g. UI dropdowns).
export const MarkTypeNames = MARK_TYPE_NAMES;
