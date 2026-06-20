// Phase D — history/revision-list.mjs
// `createRevisionList({normalizeRevision, buildIndexes})` factory → revision-list
// mutators that maintain `model.revisions` + rebuild the model indexes after each
// change.
//
//   - `ensureRevisionList(model)` — coerces to Array, normalises each entry, rebuilds
//     indexes, returns the live array.
//   - `getRevisionById(model, revisionId)` — linear lookup honouring Pascal/camel id.
//   - `addRevision(model, revision)` — upsert: if same `id` exists, merges in place;
//     otherwise appends. Always returns the normalised record.
//   - `updateRevisionStatus(model, revisionId, status)` — mutates status field in place
//     on the matching revision, then rebuilds indexes.

import { asArray } from '../core/helpers.mjs';

export function createRevisionList(options) {
    const opts = options || {};
    if (typeof opts.normalizeRevision !== 'function') {
        throw new TypeError(
            'createRevisionList requires options.normalizeRevision (function)');
    }
    if (typeof opts.buildIndexes !== 'function') {
        throw new TypeError(
            'createRevisionList requires options.buildIndexes (function)');
    }
    const { normalizeRevision, buildIndexes } = opts;

    function ensureRevisionList(model) {
        if (!Array.isArray(model.revisions)) model.revisions = [];
        model.revisions = model.revisions.map(normalizeRevision);
        buildIndexes(model);
        return model.revisions;
    }

    function getRevisionById(model, revisionId) {
        ensureRevisionList(model);
        return asArray(model.revisions).find(function (revision) {
            return revision.id === revisionId || revision.Id === revisionId;
        }) || null;
    }

    function addRevision(model, revision) {
        ensureRevisionList(model);
        const normalized = normalizeRevision(revision);
        const existing = model.revisions.find(function (item) {
            return item.id === normalized.id;
        });
        if (existing) Object.assign(existing, normalized);
        else model.revisions.push(normalized);
        buildIndexes(model);
        return normalized;
    }

    function updateRevisionStatus(model, revisionId, status) {
        ensureRevisionList(model).forEach(function (revision) {
            if (revision.id === revisionId) revision.status = status;
        });
        buildIndexes(model);
    }

    return Object.freeze({
        ensureRevisionList,
        getRevisionById,
        addRevision,
        updateRevisionStatus,
    });
}
