// Phase D — core/typing-style.mjs
// `resolveTypingStyleAtInsertion(block, offset, affinity)` — when typing at a caret
// position, infer the inline style (font, weight, color, …) the new text should
// inherit. Walks the paragraph runs, picks the run before/after the caret based on
// affinity, falls back to paragraph or block style.
//
// `styleHasValues(style)` — quick check whether a style object has any meaningful
// keys (used to skip empty styles before allocating).
//
// Pure functions.

import { asArray, asText, clone } from './helpers.mjs';
import { blockText } from './text-helpers.mjs';
import { isDrawingRunSource } from './inline-runs.mjs';
import { resolveInlineRunDisplayText } from '../render/run-text.mjs';

export function styleHasValues(style) {
    return !!(style && typeof style === 'object' && Object.keys(style).length > 0);
}

export function resolveTypingStyleAtInsertion(block, offset, affinity) {
    if (!block || block.type !== 'paragraph') return {};
    const target = Math.max(0, Math.min(blockText(block).length, Number(offset || 0) || 0));
    const direction = affinity === 'before' ? 'before' : 'after';
    let previous = null;
    let next = null;
    let cursor = 0;
    asArray(block.content && block.content.runs).some(run => {
        if (!run || run.kind === 'drawing' || isDrawingRunSource(run)) return false;
        const runText = resolveInlineRunDisplayText(run);
        const length = asText(runText).length;
        if (length <= 0) return false;
        const start = cursor;
        const end = cursor + length;
        cursor = end;
        if (target > start && target < end) {
            previous = run;
            next = null;
            return true;
        }
        if (target === start) {
            if (!next) next = run;
            return direction === 'before' && !!previous;
        }
        if (target === end) {
            previous = run;
            return direction === 'after';
        }
        if (end < target || end === target) previous = run;
        if (start > target && !next) next = run;
        return false;
    });

    const candidate = direction === 'before' ? (previous || next) : (previous || next);
    if (candidate && styleHasValues(candidate.style)) return clone(candidate.style);
    if (styleHasValues(block.content && block.content.style)) return clone(block.content.style);
    if (styleHasValues(block.style)) return clone(block.style);
    return {};
}
