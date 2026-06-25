// Phase D — core/range-formatting.mjs
// Range-level mark/style mutators used by the toolbar command pipeline.
//
//   - `removeMarksForCommandInRange(block, range, commandId)` — strips marks
//     matching the given command from every run intersecting `range`.
//   - `clearFormattingInRange(block, range)` — wipes both marks AND style on every
//     intersecting run, then renormalises runs for merge.
//
// Both functions mutate the paragraph in place and rely on `transformRunsInRange`
// to handle slicing/merging boundary runs. Factory pattern injects the run helpers
// so the legacy IIFE can keep its inline copies.

import { asArray } from './helpers.mjs';
import { normalizeMarks } from './marks.mjs';
import { normalizeTextRunForMerge } from './inline-runs.mjs';
import { markMatchesCommand } from '../input/command-classifiers.mjs';

export function createRangeFormatting(options) {
    const opts = options || {};
    if (typeof opts.transformRunsInRange !== 'function') {
        throw new TypeError(
            'createRangeFormatting requires options.transformRunsInRange (function)');
    }
    const { transformRunsInRange } = opts;

    function removeMarksForCommandInRange(block, range, commandId) {
        if (!block || block.type !== 'paragraph') return;
        transformRunsInRange(block, range.start, range.end, function (run) {
            run.marks = normalizeMarks(asArray(run.marks).filter(function (mark) {
                return !markMatchesCommand(mark, commandId);
            }));
            return run;
        });
    }

    function clearFormattingInRange(block, range) {
        if (!block || block.type !== 'paragraph') return;
        transformRunsInRange(block, range.start, range.end, function (run) {
            run.marks = [];
            run.style = {};
            return normalizeTextRunForMerge(run);
        });
    }

    return Object.freeze({
        removeMarksForCommandInRange,
        clearFormattingInRange,
    });
}
