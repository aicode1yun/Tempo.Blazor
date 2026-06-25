// Phase D — core/marker-readers.mjs
// Small readers over comment + revision records used by the marker/overlay layer.
//
// `readCommentId(comment)` — id (Pascal/camel) as string.
// `readCommentStatus(comment)` — `'resolved'` when status is `1` or contains
//   'resolved', else `'open'`.
// `commentById(model, commentId)` — first matching comment by id, or null.
// `createRevisionReaders({normalizeRevisionStatus, normalizeRevisionType})` →
//   `{readRevisionStatus, readRevisionTypeName, readRevisionMarkerType}` — revision
//   status / type-name readers plus the marker token (`revisionDeletion` /
//   `revisionFormat` / `revisionInsertion`).

import { asArray, asText } from './helpers.mjs';

export function readCommentId(comment) {
    return asText((comment && (comment.id || comment.Id)) || '');
}

export function readCommentStatus(comment) {
    const raw = comment && (comment.status ?? comment.Status);
    if (raw === 1) return 'resolved';
    const text = asText(raw || 'Open').toLowerCase();
    return text.indexOf('resolved') >= 0 ? 'resolved' : 'open';
}

export function commentById(model, commentId) {
    return asArray(model && model.comments).find(function (comment) {
        return readCommentId(comment) === commentId;
    }) || null;
}

export function createRevisionReaders(options) {
    const opts = options || {};
    for (const key of ['normalizeRevisionStatus', 'normalizeRevisionType']) {
        if (typeof opts[key] !== 'function') {
            throw new TypeError(`createRevisionReaders requires options.${key} (function)`);
        }
    }
    const { normalizeRevisionStatus, normalizeRevisionType } = opts;

    function readRevisionStatus(revision) {
        return normalizeRevisionStatus(
            revision && (revision.status ?? revision.Status ?? revision.action ?? revision.Action));
    }
    function readRevisionTypeName(revision) {
        return normalizeRevisionType(revision && (revision.type ?? revision.Type));
    }
    function readRevisionMarkerType(revision) {
        const type = readRevisionTypeName(revision);
        if (type === 'Deletion') return 'revisionDeletion';
        if (type === 'FormatChange' || type === 'Formatting') return 'revisionFormat';
        return 'revisionInsertion';
    }

    return Object.freeze({ readRevisionStatus, readRevisionTypeName, readRevisionMarkerType });
}
