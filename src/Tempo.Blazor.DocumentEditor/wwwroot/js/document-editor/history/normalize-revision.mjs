// Phase D — history/normalize-revision.mjs
// `normalizeRevision(raw)` — coerces a partial/inconsistent revision payload
// (Pascal or camel, with various aliases) into the canonical engine shape:
//   { id, type, author, authorObject, createdAt, groupId, timestamp, range,
//     affectedRange, payload, payloadJson, status }
//
// Non-deterministic id fallback (Date.now + random) only kicks in when caller
// supplies no id. Pure of closure state — depends only on the already-extracted
// revision-normalize + helpers modules.

import { asText, sortObject } from '../core/helpers.mjs';
import {
    normalizeRevisionType,
    normalizeRevisionStatus,
    normalizeRevisionRange,
} from '../core/revision-normalize.mjs';

export function normalizeRevision(raw) {
    const source = raw || {};
    const sourceRange = source.affectedRange || source.AffectedRange
        || source.range || source.Range || {};
    const range = normalizeRevisionRange(sourceRange);
    const author = source.Author || source.authorObject || source.author || {};
    const payload = source.payload || source.Payload
        || source.PayloadJson || source.payloadJson || {};
    return sortObject({
        id: asText(source.id || source.Id
            || ('rev-' + Date.now() + '-' + Math.floor(Math.random() * 100000))),
        type: normalizeRevisionType(
            source.type ?? source.Type
            ?? source.revisionType ?? source.RevisionType
            ?? 'Insertion'),
        author: asText(
            author.DisplayName || author.displayName
            || source.authorName || source.AuthorName
            || source.author || source.Author
            || source.authorId || source.AuthorId
            || 'local'),
        authorObject: sortObject(author || {}),
        createdAt: source.CreatedAt || source.createdAt || null,
        groupId: source.GroupId || source.groupId || null,
        timestamp: Number(source.timestamp || source.Timestamp || Date.now()) || Date.now(),
        range: sortObject(sourceRange || {}),
        affectedRange: range,
        payload: typeof payload === 'string'
            ? { text: payload }
            : sortObject(payload || {}),
        payloadJson: source.PayloadJson || source.payloadJson || null,
        status: normalizeRevisionStatus(
            source.action ?? source.Action
            ?? source.status ?? source.Status),
    });
}
