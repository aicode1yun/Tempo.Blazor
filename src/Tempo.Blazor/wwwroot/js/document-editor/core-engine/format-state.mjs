// Phase R.4.8 — core-engine/format-state.mjs
// Reads the active inline-formatting + paragraph alignment for a selection, so the hosted
// toolbar can reflect the engine's state (pressed Bold when the selection is bold, the
// current alignment, …). A mark is "active" when every covered run carries it; for a
// collapsed caret the run ending at the caret is inspected (the typing context).
//
//   formattingStateForBlockRange(block, start, end) → { bold, italic, underline,
//                                                       strikethrough, link, alignment }

import { asArray, asText } from '../core/helpers.mjs';
import { markType } from '../core/marks.mjs';

function markActiveInBlock(block, start, end, type) {
    const lo = Math.min(start, end);
    const hi = Math.max(start, end);
    let cursor = 0;
    let covered = false;
    let all = true;
    asArray(block && block.content && block.content.runs).forEach(function (run) {
        const text = asText(run.text);
        const rs = cursor; const re = cursor + text.length; cursor = re;
        if (text.length === 0) return;
        const overlaps = (hi > lo) ? (re > lo && rs < hi) : (lo > rs && lo <= re);
        if (!overlaps) return;
        covered = true;
        if (!asArray(run.marks).some(function (m) { return markType(m) === type; })) all = false;
    });
    return covered && all;
}

export function formattingStateForBlockRange(block, start, end) {
    const content = (block && block.content) || {};
    return {
        bold: markActiveInBlock(block, start, end, 'bold'),
        italic: markActiveInBlock(block, start, end, 'italic'),
        underline: markActiveInBlock(block, start, end, 'underline'),
        strikethrough: markActiveInBlock(block, start, end, 'strikethrough'),
        link: markActiveInBlock(block, start, end, 'link'),
        alignment: content.alignment || content.Alignment || 'left',
    };
}
