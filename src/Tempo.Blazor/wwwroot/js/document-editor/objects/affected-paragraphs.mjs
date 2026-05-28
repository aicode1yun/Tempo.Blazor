// Phase D — objects/affected-paragraphs.mjs
// `createAffectedParagraphsAroundObject({findBlockContainer})` factory →
// `affectedParagraphsAroundObject(model, blockId, options?)` — returns the ids of
// paragraphs near the given block that could be affected by an object layout change
// (the block itself if it's a paragraph + a configurable count of following blocks).

import { asArray } from '../core/helpers.mjs';

export function createAffectedParagraphsAroundObject(options) {
    const opts = options || {};
    if (typeof opts.findBlockContainer !== 'function') {
        throw new TypeError(
            'createAffectedParagraphsAroundObject requires options.findBlockContainer (function)');
    }
    const { findBlockContainer } = opts;

    return function affectedParagraphsAroundObject(model, blockId, callOpts) {
        const o = callOpts || {};
        const container = findBlockContainer(model, blockId);
        const blocks = asArray(
            (container && container.blocks)
            || (model && model.body && model.body.blocks));
        const index = blocks.findIndex(function (block) { return block.id === blockId; });
        if (index < 0) return [];
        const owner = blocks[index] || null;
        const rawFollowingCount = o.followingCount ?? o.FollowingCount ?? 3;
        const parsedFollowingCount = Number(rawFollowingCount);
        const followingCount = Math.max(0,
            Number.isFinite(parsedFollowingCount) ? parsedFollowingCount : 3);
        const start = owner && owner.type === 'paragraph'
            ? index
            : Math.max(0, index - 1);
        return blocks
            .slice(start, index + followingCount + 1)
            .filter(function (block) { return block && block.type === 'paragraph'; })
            .map(function (block) { return block.id; });
    };
}
