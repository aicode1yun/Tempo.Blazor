// Phase D — core/text-helpers.mjs
// Pure text/run helpers used by the engine. Extracted from the legacy IIFE.
// All functions are side-effect-free and operate only on plain data structures.

import { asArray, asText, textFromRuns } from './helpers.mjs';

// Concatenated text content of a paragraph block, or empty string if the block is not
// a paragraph or has no runs array.
export function blockText(block) {
    return block && block.content && Array.isArray(block.content.runs)
        ? textFromRuns(block.content.runs)
        : '';
}

// True if the block carries an editable runs array. Used by selection/typing code to
// decide whether `block.content.runs` can be modified.
export function isEditableTextBlock(block) {
    return !!(block && block.content && Array.isArray(block.content.runs));
}

// Clamps an offset to the bounds of `text` and snaps off the middle of a surrogate pair.
// `direction === 'end'` rounds outward (past the surrogate low half), anything else rounds
// inward (before the surrogate high half). Mirrors the legacy `clampTextBoundary` exactly.
export function clampTextBoundary(text, index, direction) {
    const source = asText(text);
    const value = Math.max(0, Math.min(source.length, Number(index || 0)));
    if (value > 0
        && value < source.length
        && source.charCodeAt(value - 1) >= 0xD800
        && source.charCodeAt(value - 1) <= 0xDBFF
        && source.charCodeAt(value) >= 0xDC00
        && source.charCodeAt(value) <= 0xDFFF) {
        return direction === 'end' ? value + 1 : value - 1;
    }
    return value;
}

// Clamps a [start, end] range into a non-decreasing pair within `text`, with surrogate-safe
// boundaries on both ends.
export function clampTextRange(text, start, end) {
    const source = asText(text);
    const from = clampTextBoundary(source, Math.min(Number(start || 0), Number(end || 0)), 'start');
    let to = clampTextBoundary(source, Math.max(Number(start || 0), Number(end || 0)), 'end');
    if (to < from) to = from;
    return { start: from, end: to };
}

// Returns the number of columns in a table (max of any row's effective column count
// taking colSpan into account). Always at least 1.
export function tableColumnCount(table) {
    const rows = asArray(table && table.content && table.content.rows);
    if (rows.length === 0) return 1;
    let max = 1;
    for (const row of rows) {
        const cells = asArray(row.cells);
        let sum = 0;
        for (const cell of cells) sum += Math.max(1, Number(cell.colSpan || 1));
        if (sum > max) max = sum;
    }
    return max;
}
