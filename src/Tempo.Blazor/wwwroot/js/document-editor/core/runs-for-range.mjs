// Phase D — core/runs-for-range.mjs
// `runsForRange(block, range)` — collects the paragraph runs that intersect the
// given character range. Collapsed ranges return only the run containing the
// caret offset (via `findRunAtOffset`). Non-paragraph blocks return `[]`.

import { asArray, asText } from './helpers.mjs';
import { findRunAtOffset } from './run-finders.mjs';

export function runsForRange(block, range) {
    if (!block || block.type !== 'paragraph') return [];
    if (!range || range.collapsed) {
        const info = findRunAtOffset(block, range ? range.start : 0);
        return info && info.run ? [info.run] : [];
    }
    const result = [];
    let cursor = 0;
    asArray(block.content && block.content.runs).forEach(function (run) {
        const text = asText(run.text);
        const runStart = cursor;
        const runEnd = cursor + text.length;
        cursor = runEnd;
        if (runEnd > range.start && runStart < range.end) result.push(run);
    });
    return result;
}
