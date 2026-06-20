// Phase D — core/runtime-markers.mjs
// `createRuntimeMarkerBuilders(deps)` →
//   `{buildRuntimeCommentMarkers, buildRuntimeRevisionMarkers}`.
//
// buildRuntimeCommentMarkers(model) — one marker per comment whose range can be
//   resolved (inline ranges preferred, else the comment anchor). Skips comments
//   with no id or a degenerate range. Carries thread/target id, resolved status,
//   and a fixed priority (60).
// buildRuntimeRevisionMarkers(model) — one marker per *Pending* revision with a
//   resolvable range. Computes inserted/original text from the payload depending on
//   the marker type (insertion vs deletion) and a format delta for FormatChange.

const REQUIRED = [
    'asArray', 'asText', 'sortObject', 'collectInlineCommentRanges',
    'collectInlineRevisionRanges', 'readCommentId', 'readCommentStatus',
    'rangeFromCommentAnchor', 'normalizeRevision', 'readRevisionStatus',
    'rangeFromRevision', 'readRevisionMarkerType',
];

export function createRuntimeMarkerBuilders(deps) {
    const opts = deps || {};
    for (const key of REQUIRED) {
        if (typeof opts[key] !== 'function') {
            throw new TypeError(`createRuntimeMarkerBuilders requires options.${key} (function)`);
        }
    }
    const {
        asArray, asText, sortObject, collectInlineCommentRanges,
        collectInlineRevisionRanges, readCommentId, readCommentStatus,
        rangeFromCommentAnchor, normalizeRevision, readRevisionStatus,
        rangeFromRevision, readRevisionMarkerType,
    } = opts;

    function buildRuntimeCommentMarkers(model) {
        const inlineRanges = collectInlineCommentRanges(model);
        return asArray(model && model.comments).map(function (comment) {
            const commentId = readCommentId(comment);
            if (!commentId) return null;
            const range = inlineRanges[commentId] || rangeFromCommentAnchor(model, comment);
            if (!range || !range.startBlockId || range.endOffset <= range.startOffset) return null;
            const status = readCommentStatus(comment);
            return sortObject({
                blockId: range.startBlockId,
                id: 'comment:' + commentId,
                isActive: false,
                isResolved: status === 'resolved',
                startOffset: range.startOffset,
                endOffset: range.endOffset,
                type: 'comment',
                threadId: commentId,
                targetId: commentId,
                status,
                range,
                affectsData: true,
                priority: 60,
                source: 'document',
            });
        }).filter(Boolean);
    }

    function buildRuntimeRevisionMarkers(model) {
        const inlineRanges = collectInlineRevisionRanges(model);
        return asArray(model && model.revisions).map(function (revision) {
            const normalized = normalizeRevision(revision);
            const revisionId = asText(normalized.id);
            if (!revisionId || readRevisionStatus(normalized) !== 'Pending') return null;
            const range = inlineRanges[revisionId] || rangeFromRevision(model, normalized);
            if (!range || !range.startBlockId || range.endOffset <= range.startOffset) return null;
            const type = readRevisionMarkerType(normalized);
            const payloadText = asText(
                normalized.payloadJson || (normalized.payload && normalized.payload.text) || '');
            return sortObject({
                author: normalized.author,
                blockId: range.startBlockId,
                createdAt: normalized.createdAt || normalized.timestamp || null,
                endOffset: range.endOffset,
                formatDelta: (normalized.payload && normalized.payload.mark) || null,
                id: 'revision:' + revisionId,
                insertedText: type === 'revisionInsertion' ? payloadText : '',
                isActive: false,
                originalText: type === 'revisionDeletion' ? payloadText : '',
                priority: 50,
                range,
                source: 'document',
                startOffset: range.startOffset,
                status: readRevisionStatus(normalized),
                targetId: revisionId,
                threadId: revisionId,
                type,
            });
        }).filter(Boolean);
    }

    return Object.freeze({ buildRuntimeCommentMarkers, buildRuntimeRevisionMarkers });
}
