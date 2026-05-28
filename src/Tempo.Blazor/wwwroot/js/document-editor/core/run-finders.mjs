// Phase D — core/run-finders.mjs
// Pure helpers for locating inline runs within a paragraph block by text offset.
// Used by the input/selection/history layers to translate `(blockId, offset)` into
// `(blockId, runIndex, offsetWithinRun)`.

import { asArray, asText } from './helpers.mjs';
import { blockText } from './text-helpers.mjs';

// For a paragraph block, find the run containing the given offset. Returns
// `{ run, start, end, index }` where start/end are character offsets within the
// paragraph. When `offset` is past the last run, the last run is returned (this is
// the legacy contract).
export function findRunAtOffset(block, offset) {
    let cursor = 0;
    const runs = asArray(block && block.content && block.content.runs);
    for (let i = 0; i < runs.length; i++) {
        const text = asText(runs[i].text);
        const end = cursor + text.length;
        if ((offset >= cursor && offset <= end) || i === runs.length - 1) {
            return { run: runs[i], start: cursor, end, index: i };
        }
        cursor = end;
    }
    return null;
}

// Like `findRunAtOffset` but returns `{ run, localOffset, start, end }` and only works
// on paragraphs. The `localOffset` is the cursor position within the run's text.
// Used by selection-to-DOM mapping.
export function inlineAtOffset(block, offset) {
    if (!block || block.type !== 'paragraph') return null;
    let cursor = 0;
    const runs = asArray(block.content && block.content.runs);
    for (let i = 0; i < runs.length; i++) {
        const length = asText(runs[i].text).length;
        if (offset <= cursor + length || i === runs.length - 1) {
            return {
                run: runs[i],
                localOffset: Math.max(0, Math.min(length, offset - cursor)),
                start: cursor,
                end: cursor + length,
            };
        }
        cursor += length;
    }
    return null;
}

// Translate a `(block, offset, affinity)` triple to an `{ inlineIndex, localOffset,
// run }` triple. `affinity === 'before'` snaps to the end of the previous run when
// offset lands on a boundary; otherwise snaps to the start of the next run.
export function resolveTextOffsetToInlineIndex(block, offset, affinity) {
    if (!block || block.type !== 'paragraph') return null;
    const runs = asArray(block.content && block.content.runs);
    const textLength = blockText(block).length;
    const target = Math.max(0, Math.min(textLength, Number(offset || 0) || 0));
    const direction = affinity === 'before' ? 'before' : 'after';

    let cursor = 0;
    for (let i = 0; i < runs.length; i++) {
        const length = asText(runs[i].text).length;
        const end = cursor + length;
        const onBoundary = target === end;
        const inside = target >= cursor && target < end;
        if (inside || (onBoundary && (direction === 'before' || i === runs.length - 1))) {
            return {
                inlineIndex: i,
                localOffset: Math.max(0, Math.min(length, target - cursor)),
                run: runs[i],
            };
        }
        cursor = end;
    }
    // Empty paragraph fallback
    if (runs.length === 0) return null;
    const last = runs[runs.length - 1];
    return {
        inlineIndex: runs.length - 1,
        localOffset: asText(last.text).length,
        run: last,
    };
}
