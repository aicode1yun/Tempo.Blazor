// Phase D — core/run-mutators.mjs
// In-place run mutators used by the apply-operation handlers.
//   `deleteTextRange(block, start, end)` — splice text out of a paragraph
//   `splitParagraphRuns(block, offset)` — split runs into before/after lists (returned)
//   `splitRunsForRange(block, start, end, mark, remove)` — apply/remove a mark across a range
//   `setParagraphText(block, text)` — replace all runs with a single text run
//   `cloneRunSlice(run, start, end, suffix)` — produce a cloned run with text sliced
//
// Pure mutators (they mutate `block.content.runs`); cloneRunSlice is purely
// functional.

import { asArray, asText, clone } from './helpers.mjs';
import { clampTextRange } from './text-helpers.mjs';
import {
    normalizeTextRunForMerge,
    mergeAdjacentTextRuns,
    plainRuns,
} from './inline-runs.mjs';
import { updateMarks } from './marks.mjs';

function stableId(prefix, path) {
    return String(prefix || 'id') + '-' + String(path || '0').replace(/[^a-z0-9_-]+/gi, '-');
}

// Set the entire paragraph text to a single plain run.
export function setParagraphText(block, text) {
    if (!block.content) block.content = { type: 'paragraph', runs: [] };
    block.content.runs = plainRuns(text, block.id + '-run-0');
}

// Slice a run between `start` and `end`, returning a normalised text run with the
// `suffix` appended to its id.
export function cloneRunSlice(run, start, end, suffix) {
    const text = asText(run && run.text);
    const range = clampTextRange(text, start, end);
    const next = clone(run || {});
    next.id = asText(next.id || next.Id || stableId('inline', 'run')) + suffix;
    next.text = text.slice(range.start, range.end);
    return normalizeTextRunForMerge(next);
}

// Splice text from `start` to `end` out of a paragraph, preserving runs outside the
// range and slicing the runs at the boundaries.
export function deleteTextRange(block, start, end) {
    if (!block || block.type !== 'paragraph') return;
    if (!block.content) block.content = { type: 'paragraph', runs: [] };
    const from = Math.max(0, Math.min(start, end));
    const to = Math.max(from, Math.max(start, end));
    const result = [];
    let cursor = 0;
    asArray(block.content.runs).forEach(run => {
        const runText = asText(run.text);
        const runStart = cursor;
        const runEnd = cursor + runText.length;
        cursor = runEnd;
        if (runEnd <= from || runStart >= to || runText.length === 0) {
            result.push(normalizeTextRunForMerge(run));
            return;
        }
        const localStart = Math.max(0, from - runStart);
        const localEnd = Math.min(runText.length, to - runStart);
        if (localStart > 0) result.push(cloneRunSlice(run, 0, localStart, '-d-before'));
        if (localEnd < runText.length) result.push(cloneRunSlice(run, localEnd, runText.length, '-d-after'));
    });
    block.content.runs = mergeAdjacentTextRuns(
        result.length ? result : plainRuns('', block.id + '-run-0'));
}

// Split a paragraph's runs at `offset`. Returns `{ before, after }` — two arrays of
// runs the caller can place into separate blocks. Does NOT mutate `block`.
export function splitParagraphRuns(block, offset) {
    const before = [];
    const after = [];
    let cursor = 0;
    asArray(block && block.content && block.content.runs).forEach(run => {
        const runText = asText(run.text);
        const runStart = cursor;
        const runEnd = cursor + runText.length;
        cursor = runEnd;
        if (runEnd <= offset) {
            before.push(normalizeTextRunForMerge(run));
            return;
        }
        if (runStart >= offset) {
            after.push(normalizeTextRunForMerge(run));
            return;
        }
        const local = Math.max(0, Math.min(runText.length, offset - runStart));
        if (local > 0) before.push(cloneRunSlice(run, 0, local, '-s-before'));
        if (local < runText.length) after.push(cloneRunSlice(run, local, runText.length, '-s-after'));
    });
    return {
        before: mergeAdjacentTextRuns(before.length ? before : plainRuns('', block.id + '-before-empty')),
        after: mergeAdjacentTextRuns(after.length ? after : plainRuns('', block.id + '-after-empty')),
    };
}

// Apply or remove `mark` across the text range `start..end` in a paragraph.
// Runs are split at the boundaries; the middle slice gets `updateMarks(marks, mark, remove)`.
export function splitRunsForRange(block, start, end, mark, remove) {
    const result = [];
    let cursor = 0;
    asArray(block.content && block.content.runs).forEach(run => {
        const text = asText(run.text);
        const runStart = cursor;
        const runEnd = cursor + text.length;
        cursor = runEnd;
        if (runEnd <= start || runStart >= end || text.length === 0) {
            result.push(normalizeTextRunForMerge(run));
            return;
        }
        let localStart = Math.max(0, start - runStart);
        let localEnd = Math.min(text.length, end - runStart);
        const localRange = clampTextRange(text, localStart, localEnd);
        localStart = localRange.start;
        localEnd = localRange.end;
        if (localStart > 0) {
            const before = clone(run);
            before.id = run.id + '-a';
            before.text = text.slice(0, localStart);
            result.push(normalizeTextRunForMerge(before));
        }
        const middle = clone(run);
        middle.id = run.id + '-m';
        middle.text = text.slice(localStart, localEnd);
        middle.marks = updateMarks(middle.marks, mark, remove);
        result.push(normalizeTextRunForMerge(middle));
        if (localEnd < text.length) {
            const after = clone(run);
            after.id = run.id + '-b';
            after.text = text.slice(localEnd);
            result.push(normalizeTextRunForMerge(after));
        }
    });
    block.content.runs = mergeAdjacentTextRuns(result);
}
