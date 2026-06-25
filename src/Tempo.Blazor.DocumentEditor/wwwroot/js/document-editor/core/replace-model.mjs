// Phase D — core/replace-model.mjs
// `createReplaceModelContents({ buildIndexes })` → `replaceModelContents(target, source)`.
//
// Used by `applyRestoreSnapshot` and the transaction rollback path to restore a
// model in place. In-place mutation (rather than reassignment) is required so that
// holders of the model reference see the restored state without re-reading.
//
// Pure factory — `buildIndexes` is injected (from `core/indexes.mjs`).

import { clone } from './helpers.mjs';

export function createReplaceModelContents(options) {
    const opts = options || {};
    if (typeof opts.buildIndexes !== 'function') {
        throw new TypeError('createReplaceModelContents requires options.buildIndexes (function)');
    }
    const { buildIndexes } = opts;

    return function replaceModelContents(target, source) {
        Object.keys(target).forEach(key => { delete target[key]; });
        Object.assign(target, clone(source));
        buildIndexes(target);
    };
}
