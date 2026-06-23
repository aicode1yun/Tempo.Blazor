// Phase D — input/layout-text-edit-model.mjs
// `applyLayoutTextEditModel(segments, change)` — applies a `beforeinput` change
// against the flat layout segment array. Used by the keystroke pipeline to compute
// the post-edit text + caret position without round-tripping through the full model.
//
// Supported `inputType`s: `insertText`, `deleteContentBackward`, `deleteContentForward`,
// `insertParagraph`. Returns `{ Handled, Text, CaretOffset, DeletedText? }` on hit,
// or `{ Handled: false, MergePrevious?/MergeNext? }` when the edit crosses a segment
// boundary and needs to be re-dispatched at the model level.

import { asArray, asText } from '../core/helpers.mjs';

export function applyLayoutTextEditModel(segments, change) {
    const ordered = asArray(segments).slice().sort(function (a, b) {
        return Number(a.StartOffset || 0) - Number(b.StartOffset || 0);
    });
    const text = ordered.map(function (segment) { return asText(segment.Text); }).join('');
    const offset = Math.max(0, Math.min(text.length, Number(change && change.offset || 0) || 0));
    const inputType = change && change.inputType || '';
    if (inputType === 'insertText') {
        const data = asText(change.data || '');
        return {
            Handled: true,
            Text: text.slice(0, offset) + data + text.slice(offset),
            CaretOffset: offset + data.length,
        };
    }
    if (inputType === 'deleteContentBackward') {
        if (offset <= 0) return { Handled: false, MergePrevious: true };
        return {
            Handled: true,
            Text: text.slice(0, offset - 1) + text.slice(offset),
            DeletedText: text.slice(offset - 1, offset),
            CaretOffset: offset - 1,
        };
    }
    if (inputType === 'deleteContentForward') {
        if (offset >= text.length) return { Handled: false, MergeNext: true };
        return {
            Handled: true,
            Text: text.slice(0, offset) + text.slice(offset + 1),
            DeletedText: text.slice(offset, offset + 1),
            CaretOffset: offset,
        };
    }
    if (inputType === 'insertParagraph') {
        return {
            Handled: true,
            SplitBefore: text.slice(0, offset),
            SplitAfter: text.slice(offset),
            StartOffset: offset,
        };
    }
    return { Handled: false };
}
