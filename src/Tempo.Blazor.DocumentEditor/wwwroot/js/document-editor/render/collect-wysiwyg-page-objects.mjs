// Phase D — render/collect-wysiwyg-page-objects.mjs
// `createCollectWysiwygPageObjectEntries({isDrawingRunSource, createWysiwygObjectRenderEntry})` →
//   `collectWysiwygPageObjectEntries(inst, blocks)` — walks the paragraphs in
//   `blocks`, picks out runs with `kind === 'drawing'` or those identified by
//   `isDrawingRunSource(run)`, and pushes a render entry per match. Non-paragraph
//   blocks are skipped; null / falsy blocks are skipped; null entry results from
//   `createWysiwygObjectRenderEntry` are dropped.

import { asArray } from '../core/helpers.mjs';

export function createCollectWysiwygPageObjectEntries(options) {
    const opts = options || {};
    if (typeof opts.isDrawingRunSource !== 'function') {
        throw new TypeError(
            'createCollectWysiwygPageObjectEntries requires options.isDrawingRunSource (function)');
    }
    if (typeof opts.createWysiwygObjectRenderEntry !== 'function') {
        throw new TypeError(
            'createCollectWysiwygPageObjectEntries requires options.createWysiwygObjectRenderEntry (function)');
    }
    const { isDrawingRunSource, createWysiwygObjectRenderEntry } = opts;

    return function collectWysiwygPageObjectEntries(inst, blocks) {
        const entries = [];
        asArray(blocks).forEach(function (block) {
            if (!block) return;
            if (block.type !== 'paragraph') return;
            asArray(block.content && block.content.runs).forEach(function (run, runIndex) {
                if (run && (run.kind === 'drawing' || isDrawingRunSource(run))) {
                    const entry = createWysiwygObjectRenderEntry(
                        inst, block, run, runIndex, 'drawing-run');
                    if (entry) entries.push(entry);
                }
            });
        });
        return entries;
    };
}
