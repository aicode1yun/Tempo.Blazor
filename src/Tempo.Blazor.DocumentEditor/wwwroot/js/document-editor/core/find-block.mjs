// Phase D — core/find-block.mjs
// `createFindBlock({buildIndexes})` factory → `findBlock(model, blockId)` — looks
// up a block by id via the model's `indexes.blocks` cache. Rebuilds the cache when
// missing or stale (the indexes are lazily populated on first use after import).

import { asText } from './helpers.mjs';

export function createFindBlock(options) {
    const opts = options || {};
    if (typeof opts.buildIndexes !== 'function') {
        throw new TypeError(
            'createFindBlock requires options.buildIndexes (function)');
    }
    const { buildIndexes } = opts;

    return function findBlock(model, blockId) {
        const id = asText(blockId);
        if (!model || !id) return null;
        if (!model.indexes || !model.indexes.blocks || !model.indexes.blocks[id]) {
            buildIndexes(model);
        }
        return model.indexes && model.indexes.blocks
            ? model.indexes.blocks[id] || null
            : null;
    };
}
