// Phase D — core/comment-revision-export.mjs
// `exportComment` + `exportRevision` — top-level serializers for comments and
// tracked-changes records. Pure functions, extracted from the legacy IIFE.

import { asArray, asText, clone, sortObject } from './helpers.mjs';
import {
    exportCommentAnchorType,
    exportCommentStatus,
    exportCommentVisibility,
    exportRevisionType,
    exportRevisionAction,
    exportRevisionAuthor,
    exportDateTimeOffset,
} from './export-types.mjs';
import { readCommentId } from './block-export.mjs';

function stableId(prefix, path) {
    return String(prefix || 'id') + '-' + String(path || '0').replace(/[^a-z0-9_-]+/gi, '-');
}

export function exportComment(comment) {
    const source = comment || {};
    const anchor = source.Anchor || source.anchor || {};
    return sortObject({
        Id: readCommentId(source),
        Anchor: {
            Type: exportCommentAnchorType(anchor.Type ?? anchor.type),
            BlockId: anchor.BlockId ?? anchor.blockId ?? null,
            StartInlineIndex: anchor.StartInlineIndex ?? anchor.startInlineIndex ?? null,
            StartOffset: anchor.StartOffset ?? anchor.startOffset ?? null,
            EndInlineIndex: anchor.EndInlineIndex ?? anchor.endInlineIndex ?? null,
            EndOffset: anchor.EndOffset ?? anchor.endOffset ?? null,
            ExternalAnchorId: anchor.ExternalAnchorId ?? anchor.externalAnchorId ?? null,
            RenditionAnchorId: anchor.RenditionAnchorId ?? anchor.renditionAnchorId ?? null,
            IsOrphaned: anchor.IsOrphaned === true || anchor.isOrphaned === true,
        },
        Entries: asArray(source.Entries || source.entries).map(entry => sortObject({
            Id: entry.Id || entry.id
                || stableId('comment-entry', readCommentId(source) + '-entry'),
            Author: clone(entry.Author || entry.author || {}),
            IsExternalAuthor: entry.IsExternalAuthor === true || entry.isExternalAuthor === true,
            Text: asText(entry.Text || entry.text),
            CreatedAt: entry.CreatedAt || entry.createdAt || null,
            ModifiedAt: entry.ModifiedAt || entry.modifiedAt || null,
        })),
        Status: exportCommentStatus(source.Status ?? source.status),
        Visibility: exportCommentVisibility(source.Visibility ?? source.visibility),
        SourceFormat: source.SourceFormat ?? source.sourceFormat ?? null,
        ExternalId: source.ExternalId ?? source.externalId ?? null,
        ResolvedAt: source.ResolvedAt ?? source.resolvedAt ?? null,
        ResolvedBy: clone(source.ResolvedBy || source.resolvedBy || null),
    });
}

export function exportRevision(revision) {
    const source = revision || {};
    const range = source.Range || source.range || source.affectedRange || source.AffectedRange || {};
    let payload = source.PayloadJson ?? source.payloadJson;
    if (payload === undefined && source.payload !== undefined) {
        payload = typeof source.payload === 'string'
            ? source.payload
            : JSON.stringify(source.payload || {});
    }
    const authorValue = source.Author || source.authorObject || source.author || {};
    const authorId = source.AuthorId || source.authorId
        || source.author || source.Author || 'local';
    return sortObject({
        Id: asText(source.Id || source.id),
        Type: exportRevisionType(source.Type ?? source.type),
        Range: {
            BlockId: range.BlockId ?? range.blockId ?? null,
            SourceBlockId: range.SourceBlockId ?? range.sourceBlockId ?? null,
            StartInlineIndex: range.StartInlineIndex ?? range.startInlineIndex ?? null,
            StartOffset: range.StartOffset ?? range.startOffset ?? range.start ?? null,
            EndInlineIndex: range.EndInlineIndex ?? range.endInlineIndex ?? null,
            EndOffset: range.EndOffset ?? range.endOffset ?? range.end ?? null,
        },
        Author: exportRevisionAuthor(authorValue, authorId),
        CreatedAt: exportDateTimeOffset(source.CreatedAt ?? source.createdAt ?? source.timestamp ?? null),
        Action: exportRevisionAction(source.Action ?? source.action ?? source.status),
        PayloadJson: payload ?? null,
        GroupId: source.GroupId ?? source.groupId ?? null,
    });
}
