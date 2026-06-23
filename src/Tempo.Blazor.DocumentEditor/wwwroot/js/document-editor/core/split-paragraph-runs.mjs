// Phase D — core/split-paragraph-runs.mjs
// `splitParagraphRunsAtOffset(block, offset)` — splits a paragraph's runs at the
// given character offset into `{before, after}` arrays. Runs straddling the offset
// are split into two slices (with `-split-before` / `-split-after` id suffixes).
// Empty halves fall back to a single plain run for callers that need a non-empty list.

import { asArray, asText, clone } from './helpers.mjs';
import { blockText } from './text-helpers.mjs';
import { mergeAdjacentTextRuns, plainRuns } from './inline-runs.mjs';

export function splitParagraphRunsAtOffset(block, offset) {
    const before = [];
    const after = [];
    let cursor = 0;
    const targetOffset = Math.max(0,
        Math.min(blockText(block).length, Number(offset || 0)));
    asArray(block && block.content && block.content.runs).forEach(function (run) {
        const text = asText(run.text);
        const runStart = cursor;
        const runEnd = cursor + text.length;
        cursor = runEnd;
        if (runEnd <= targetOffset) {
            before.push(clone(run));
            return;
        }
        if (runStart >= targetOffset) {
            after.push(clone(run));
            return;
        }
        const local = Math.max(0, Math.min(text.length, targetOffset - runStart));
        if (local > 0) {
            const beforeRun = clone(run);
            beforeRun.id = run.id + '-split-before';
            beforeRun.text = text.slice(0, local);
            before.push(beforeRun);
        }
        if (local < text.length) {
            const afterRun = clone(run);
            afterRun.id = run.id + '-split-after';
            afterRun.text = text.slice(local);
            after.push(afterRun);
        }
    });
    return {
        before: before.length > 0
            ? mergeAdjacentTextRuns(before)
            : plainRuns('', block.id + '-split-before-empty'),
        after: after.length > 0
            ? mergeAdjacentTextRuns(after)
            : plainRuns('', block.id + '-split-after-empty'),
    };
}
