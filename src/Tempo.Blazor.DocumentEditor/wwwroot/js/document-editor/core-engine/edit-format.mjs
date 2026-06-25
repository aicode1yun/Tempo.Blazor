// Phase R.4.6 — core-engine/edit-format.mjs
// Pure inline-mark + paragraph-formatting mutators for the new model-owned surface.
// They mutate the model in place (splitting/merging runs so marks/styles survive) and
// reuse the Phase-D mark helpers, so behaviour matches the rest of the engine.
//
//   blockRangeHasMark(block, start, end, type)            → true if the whole range has it
//   applyMarkToBlockRange(block, start, end, type, opts)  → toggle/set/clear a mark
//        opts: { mode: 'toggle'|'add'|'remove', value? }  (value → set-by-type marks:
//        textcolor / highlight / fontfamily / fontsize)
//   setParagraphProperty(block, key, value)               → alignment / lineSpacing / indent…
//
// `applyMarkToSelection` / `setParagraphAlignment` orchestration over a multi-block
// selection lives in render-host (it owns block order + selection).

import { asArray, asText, clone } from '../core/helpers.mjs';
import { markType, markValue, updateMarks } from '../core/marks.mjs';
import { normalizeTextRunForMerge, mergeAdjacentTextRuns } from '../core/inline-runs.mjs';

// Boolean toggle marks vs set-by-type value marks (only one of a value type per run).
const VALUE_MARK_TYPES = new Set(['fontfamily', 'fontsize', 'textcolor', 'fontcolor', 'foregroundcolor', 'highlight', 'backgroundcolor', 'link', 'hyperlink']);

function isParagraph(block) { return block && block.type === 'paragraph'; }

// Splits the runs at [start,end) and applies `fn(run)` to each run slice inside the range.
// In-place; merges adjacent compatible runs afterwards. (A callback-driven sibling of
// run-mutators.splitRunsForRange.) Exported so comments (R.4.6g) can reuse it.
export function transformRunsInRange(block, start, end, fn) {
    if (!isParagraph(block) || !block.content) return;
    const lo = Math.min(start, end);
    const hi = Math.max(start, end);
    const result = [];
    let cursor = 0;
    asArray(block.content.runs).forEach(function (run) {
        const text = asText(run.text);
        const runStart = cursor;
        const runEnd = cursor + text.length;
        cursor = runEnd;
        // Drawing runs / out-of-range / empty → untouched.
        if (run.kind === 'drawing' || runEnd <= lo || runStart >= hi || text.length === 0) {
            result.push(normalizeTextRunForMerge(run));
            return;
        }
        const localStart = Math.max(0, lo - runStart);
        const localEnd = Math.min(text.length, hi - runStart);
        if (localStart > 0) {
            const before = clone(run); before.id = run.id + '-fa'; before.text = text.slice(0, localStart);
            result.push(normalizeTextRunForMerge(before));
        }
        const middle = clone(run); middle.id = run.id + '-fm'; middle.text = text.slice(localStart, localEnd);
        fn(middle);
        result.push(normalizeTextRunForMerge(middle));
        if (localEnd < text.length) {
            const after = clone(run); after.id = run.id + '-fb'; after.text = text.slice(localEnd);
            result.push(normalizeTextRunForMerge(after));
        }
    });
    block.content.runs = mergeAdjacentTextRuns(result);
}

export function blockRangeHasMark(block, start, end, type) {
    if (!isParagraph(block) || !block.content) return false;
    const lo = Math.min(start, end);
    const hi = Math.max(start, end);
    if (hi <= lo) return false;
    let cursor = 0;
    let covered = false;
    let all = true;
    asArray(block.content.runs).forEach(function (run) {
        const text = asText(run.text);
        const runStart = cursor; const runEnd = cursor + text.length; cursor = runEnd;
        if (runEnd <= lo || runStart >= hi || text.length === 0) return;
        covered = true;
        const has = asArray(run.marks).some(function (m) { return markType(m) === type; });
        if (!has) all = false;
    });
    return covered && all;
}

// The value of the first mark of `type` covering the range (for a collapsed range, the
// run ending at / containing the offset). Used to read the link href under the caret.
export function firstMarkValueInRange(block, start, end, type) {
    if (!isParagraph(block) || !block.content) return null;
    const lo = Math.min(start, end);
    const hi = Math.max(start, end);
    let cursor = 0;
    let found = null;
    asArray(block.content.runs).some(function (run) {
        const text = asText(run.text);
        const rs = cursor; const re = cursor + text.length; cursor = re;
        const overlaps = (hi > lo) ? (re > lo && rs < hi) : (lo > rs && lo <= re);
        if (overlaps) {
            const m = asArray(run.marks).find(function (x) { return markType(x) === type; });
            if (m) { found = markValue(m); return true; }
        }
        return false;
    });
    return found;
}

export function applyMarkToBlockRange(block, start, end, type, opts) {
    if (!isParagraph(block)) return;
    const options = opts || {};
    const value = options.value;
    const isValueMark = VALUE_MARK_TYPES.has(type) || value != null;
    const mark = (value != null) ? { type: type, value: value } : { type: type };
    if (options.markExtra) Object.assign(mark, options.markExtra); // R.5.11 — extra mark fields (e.g. formatrev.format)

    if (isValueMark) {
        // Value marks are keyed by type (one per run). Removal strips by type (the href
        // value need not be supplied); setting replaces any existing same-type mark.
        if (options.mode === 'remove') {
            transformRunsInRange(block, start, end, function (run) {
                run.marks = asArray(run.marks).filter(function (m) { return markType(m) !== type; });
            });
            return;
        }
        transformRunsInRange(block, start, end, function (run) {
            run.marks = asArray(run.marks).filter(function (m) { return markType(m) !== type; });
            run.marks = updateMarks(run.marks, mark, false);
        });
        return;
    }
    let remove;
    if (options.mode === 'add') remove = false;
    else if (options.mode === 'remove') remove = true;
    else remove = blockRangeHasMark(block, start, end, type); // toggle
    transformRunsInRange(block, start, end, function (run) {
        run.marks = updateMarks(run.marks, mark, remove);
    });
}

// Paragraph-level property (alignment / lineSpacing / indentLeft / …) on content.
export function setParagraphProperty(block, key, value) {
    if (!isParagraph(block)) return false;
    if (!block.content) block.content = { type: 'paragraph', runs: [] };
    if (value == null) delete block.content[key];
    else block.content[key] = value;
    return true;
}
