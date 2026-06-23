// Phase D — core/first-block.mjs
// `firstTextBlock(model)` — first paragraph in the body (or the first block of any
// type if no paragraph exists). Used by the command pipeline as the fallback
// insertion target when no selection is active.
//
// `firstModelSelection(model)` — synthesize a collapsed-caret selection at the
// start of the first text block. Used as the initial selection state on a freshly
// loaded model.

import { asArray } from './helpers.mjs';

export function firstTextBlock(model) {
    const blocks = asArray(model && model.body && model.body.blocks);
    for (let i = 0; i < blocks.length; i++) {
        if (blocks[i] && blocks[i].type === 'paragraph') return blocks[i];
    }
    return blocks[0] || null;
}

export function firstModelSelection(model) {
    const block = firstTextBlock(model);
    return {
        region: 'Body',
        blockId: (block && block.id) || '',
        offset: 0,
        isCollapsed: true,
    };
}
