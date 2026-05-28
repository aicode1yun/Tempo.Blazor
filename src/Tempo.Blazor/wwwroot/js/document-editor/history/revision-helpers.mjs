// Phase D — history/revision-helpers.mjs
// Pure helpers for the tracked-changes (revisions) pipeline.
//   `revisionById(model, revisionId)` — look up a revision by id (camel or Pascal)
//   `readRevisionStatus(revision)` — canonical status ('Pending', 'Accepted', 'Rejected')
//   `readRevisionTypeName(revision)` — canonical type ('Insertion', 'Deletion', …)
//   `readRevisionMarkerType(revision)` — marker layer name ('revisionDeletion', etc.)
//   `setRevisionPayloadText(revision, text)` — set both payload.text + payloadJson
//   `createTrackedRevisionPayload(type, range, text, userId, source, extra)` — build
//                                                                                a complete
//                                                                                revision record
//   `transformRunsInRange(block, start, end, transform)` — slice runs at boundaries,
//                                                          run `transform(middle)` on
//                                                          each affected slice
//   `setRevisionForRange(model, revisionId, range)` — apply `transformRunsInRange` to
//                                                     stamp revisionId across a range
//
// Pure factory for ensure/add helpers (need buildIndexes injection).

import { asArray, asText, clone, sortObject } from '../core/helpers.mjs';
import { clampTextRange } from '../core/text-helpers.mjs';
import {
    normalizeRevisionType,
    normalizeRevisionStatus,
    normalizeRevisionRange,
} from '../core/revision-normalize.mjs';
import {
    normalizeTextRunForMerge,
    mergeAdjacentTextRuns,
} from '../core/inline-runs.mjs';

export function revisionById(model, revisionId) {
    const id = asText(revisionId);
    return asArray(model && model.revisions).find(revision =>
        asText(revision && (revision.id || revision.Id)) === id) || null;
}

export function readRevisionStatus(revision) {
    return normalizeRevisionStatus(revision
        && (revision.status ?? revision.Status ?? revision.action ?? revision.Action));
}

export function readRevisionTypeName(revision) {
    return normalizeRevisionType(revision && (revision.type ?? revision.Type));
}

export function readRevisionMarkerType(revision) {
    const type = readRevisionTypeName(revision);
    if (type === 'Deletion') return 'revisionDeletion';
    if (type === 'FormatChange' || type === 'Formatting') return 'revisionFormat';
    return 'revisionInsertion';
}

export function setRevisionPayloadText(revision, text) {
    if (!revision) return;
    const value = asText(text);
    revision.payload = sortObject(Object.assign({}, revision.payload || {}, { text: value }));
    revision.payloadJson = value;
}

// Thin wrappers around `createTrackedRevisionPayload` for the common revision kinds.
// `createInsertionRevisionPayload` and `createStructureRevisionPayload` are pure
// (no model dependency). `createDeletionRevisionPayload` needs the model so it can
// extract the deleted text from `model.indexes` — exposed as a factory so the engine
// can inject its `findBlock` / `blockText`.

export function createInsertionRevisionPayload(range, text, userId, source, extra) {
    return createTrackedRevisionPayload(
        'Insertion', range, text, userId, source || 'typing', extra);
}

export function createStructureRevisionPayload(range, label, userId, source, extra) {
    return createTrackedRevisionPayload(
        'Structure', range, label || 'SplitBlock', userId, source || 'structure', extra);
}

export function createDeletionRevisionPayloadFactory(options) {
    const opts = options || {};
    if (typeof opts.findBlock !== 'function') {
        throw new TypeError(
            'createDeletionRevisionPayloadFactory requires options.findBlock (function)');
    }
    if (typeof opts.blockText !== 'function') {
        throw new TypeError(
            'createDeletionRevisionPayloadFactory requires options.blockText (function)');
    }
    const { findBlock, blockText } = opts;
    return function createDeletionRevisionPayload(model, range, userId, source, extra) {
        const normalizedRange = normalizeRevisionRange(range);
        const block = findBlock(model, normalizedRange.blockId);
        let deletedText = asText(extra && (extra.text || extra.Text));
        if (!deletedText && block) {
            deletedText = blockText(block).slice(normalizedRange.start, normalizedRange.end);
        }
        return createTrackedRevisionPayload(
            'Deletion', normalizedRange, deletedText, userId, source || 'delete', extra);
    };
}

// Build a complete revision record from scratch. Generates a non-deterministic id
// (`rev-<type>-<timestamp>-<random>`) which is why it's not a pure factory — the
// random id makes it a small concession for ergonomics. Callers that need
// determinism should pass `opts.id`.
export function createTrackedRevisionPayload(type, range, text, userId, source, extra) {
    const normalizedType = normalizeRevisionType(type);
    const normalizedRange = normalizeRevisionRange(range);
    const revisionText = asText(text);
    const opts = extra || {};
    const payload = Object.assign({}, opts.payload || opts.Payload || {}, { text: revisionText });
    return sortObject({
        id: opts.id || opts.Id
            || 'rev-' + normalizedType.toLowerCase() + '-' + Date.now()
                + '-' + Math.floor(Math.random() * 100000),
        type: normalizedType,
        status: 'Pending',
        author: asText(opts.author || opts.Author || userId || 'local'),
        authorId: asText(opts.authorId || opts.AuthorId || userId || 'local'),
        source: source || opts.source || opts.Source || '',
        affectedRange: normalizedRange,
        range: normalizedRange,
        payload: sortObject(payload),
        payloadJson: revisionText,
        timestamp: opts.timestamp || opts.Timestamp || Date.now(),
    });
}

// Slice the paragraph's runs at `start`/`end`, run `transform(middle)` on each
// affected slice, then merge adjacent same-styled runs. Returns the list of
// affected (transformed) runs for the caller to inspect.
export function transformRunsInRange(block, start, end, transform) {
    if (!block || block.type !== 'paragraph') return [];
    const result = [];
    const affected = [];
    let cursor = 0;
    asArray(block.content && block.content.runs).forEach(run => {
        const text = asText(run.text);
        const runStart = cursor;
        const runEnd = cursor + text.length;
        cursor = runEnd;
        if (runEnd <= start || runStart >= end || text.length === 0) {
            result.push(clone(run));
            return;
        }
        let localStart = Math.max(0, start - runStart);
        let localEnd = Math.min(text.length, end - runStart);
        const localRange = clampTextRange(text, localStart, localEnd);
        localStart = localRange.start;
        localEnd = localRange.end;
        if (localStart > 0) {
            const before = clone(run);
            before.id = run.id + '-pre-' + start;
            before.text = text.slice(0, localStart);
            result.push(normalizeTextRunForMerge(before));
        }
        let middle = clone(run);
        middle.id = run.id + '-rev-' + start + '-' + end;
        middle.text = text.slice(localStart, localEnd);
        middle = transform(middle) || middle;
        if (middle.text !== '') {
            middle = normalizeTextRunForMerge(middle);
            result.push(middle);
            affected.push(middle);
        }
        if (localEnd < text.length) {
            const after = clone(run);
            after.id = run.id + '-post-' + end;
            after.text = text.slice(localEnd);
            result.push(normalizeTextRunForMerge(after));
        }
    });
    block.content.runs = mergeAdjacentTextRuns(result);
    return affected;
}

// Factory — `ensureRevisionList(model)` and `addRevision(model, revision)` both need
// `normalizeRevision` (which generates non-deterministic ids) + `buildIndexes`. They
// take both as injected deps.
export function createRevisionListHelpers(options) {
    const opts = options || {};
    const required = ['normalizeRevision', 'buildIndexes'];
    for (const key of required) {
        if (typeof opts[key] !== 'function') {
            throw new TypeError(`createRevisionListHelpers requires options.${key} (function)`);
        }
    }
    const { normalizeRevision, buildIndexes } = opts;

    function ensureRevisionList(model) {
        if (!Array.isArray(model.revisions)) model.revisions = [];
        model.revisions = model.revisions.map(normalizeRevision);
        buildIndexes(model);
        return model.revisions;
    }

    function addRevision(model, revision) {
        ensureRevisionList(model);
        const normalized = normalizeRevision(revision);
        const existing = model.revisions.find(item => item.id === normalized.id);
        if (existing) Object.assign(existing, normalized);
        else model.revisions.push(normalized);
        buildIndexes(model);
        return normalized;
    }

    function getRevisionById(model, revisionId) {
        ensureRevisionList(model);
        return asArray(model.revisions).find(revision =>
            revision.id === revisionId || revision.Id === revisionId) || null;
    }

    return Object.freeze({ ensureRevisionList, addRevision, getRevisionById });
}

// Factory — `setRevisionForRange(model, revisionId, range)` needs `findBlock`.
export function createSetRevisionForRange(options) {
    const opts = options || {};
    if (typeof opts.findBlock !== 'function') {
        throw new TypeError('createSetRevisionForRange requires options.findBlock');
    }
    const { findBlock } = opts;

    return function setRevisionForRange(model, revisionId, range) {
        const normalizedRange = normalizeRevisionRange(range);
        const block = findBlock(model, normalizedRange.blockId);
        return transformRunsInRange(block, normalizedRange.start, normalizedRange.end, run => {
            run.revisionId = revisionId;
            return run;
        });
    };
}
