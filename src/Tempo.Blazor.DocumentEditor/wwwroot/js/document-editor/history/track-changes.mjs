// Phase D — history/track-changes.mjs
// Track-changes resolvers extracted from the legacy IIFE.
//
//   - `resolveTrackChangesState(options)` — collapses a 3-source decision (local
//     instance option > global option > default false) into `{displayMode, enabled,
//     globalEnabled, localEnabled, source}`. `source` is 'local'/'global'/'default'.
//   - `isTrackChangesEnabled(inst)` — `inst.options` → boolean enabled flag.
//   - `resolveRevisionUserId(options)` — falls through author.Id > opts.currentUserId
//     > opts.userId > author.DisplayName > 'local'.
//   - `revisionPayloadText(revision)` / `stableRevisionStringify(value)` — small
//     payload-side helpers (text accessor, stable JSON stringify).

import { asText, sortObject } from '../core/helpers.mjs';
import { readOptionalBoolean } from '../core/value-readers.mjs';

export function resolveTrackChangesState(options) {
    const opts = options || {};
    const localEnabled = readOptionalBoolean(opts, [
        'trackChangesEnabled', 'TrackChangesEnabled',
        'trackChanges', 'TrackChanges',
    ]);
    const globalEnabled = readOptionalBoolean(opts, [
        'globalTrackChangesEnabled', 'GlobalTrackChangesEnabled',
        'defaultTrackChangesEnabled', 'DefaultTrackChangesEnabled',
        'trackChangesDefaultEnabled', 'TrackChangesDefaultEnabled',
    ]);
    const displayMode = opts.reviewDisplayMode || opts.ReviewDisplayMode
        || opts.globalReviewDisplayMode || opts.GlobalReviewDisplayMode
        || 'AllMarkup';
    const source = localEnabled !== null
        ? 'local'
        : globalEnabled !== null
            ? 'global'
            : 'default';
    return sortObject({
        displayMode: asText(displayMode || 'AllMarkup'),
        enabled: localEnabled !== null
            ? localEnabled
            : (globalEnabled !== null ? globalEnabled : false),
        globalEnabled: globalEnabled,
        localEnabled: localEnabled,
        source: source,
    });
}

export function isTrackChangesEnabled(inst) {
    return resolveTrackChangesState(inst && inst.options || {}).enabled === true;
}

export function resolveRevisionUserId(options) {
    const opts = options || {};
    const author = opts.author || opts.Author || {};
    return asText(
        author.Id || author.id
        || opts.currentUserId || opts.CurrentUserId
        || opts.userId || opts.UserId
        || author.DisplayName || author.displayName
        || 'local');
}

export function revisionPayloadText(revision) {
    const payload = revision && (revision.payload || revision.Payload) || {};
    return asText(revision && (revision.payloadJson ?? revision.PayloadJson
        ?? payload.text ?? payload.Text ?? ''));
}

export function stableRevisionStringify(value) {
    if (Array.isArray(value)) {
        return '[' + value.map(stableRevisionStringify).join(',') + ']';
    }
    if (value && typeof value === 'object') {
        return '{' + Object.keys(value).sort().map(function (key) {
            return JSON.stringify(key) + ':' + stableRevisionStringify(value[key]);
        }).join(',') + '}';
    }
    return JSON.stringify(value);
}
