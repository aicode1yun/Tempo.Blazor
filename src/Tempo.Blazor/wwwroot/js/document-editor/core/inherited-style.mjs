// Phase D — core/inherited-style.mjs
// `findInheritedTextColor(block, offset)` — given a paragraph block and a caret
// offset, walks the runs to find the text color that would apply at that offset.
// Returns the run's own color if present, otherwise the most recent left-of-offset
// color (the inherited fallback). Used by the toolbar's textColor swatch to show
// what color new typing would adopt.

import { asArray, asText } from './helpers.mjs';

export function findInheritedTextColor(block, offset) {
    let cursor = 0;
    let fallback = null;
    const runs = asArray(block && block.content && block.content.runs);
    for (let i = 0; i < runs.length; i++) {
        const run = runs[i];
        const text = asText(run.text);
        const end = cursor + text.length;
        const color = run.style && (run.style.color || run.style.Color) || null;
        if (color) fallback = color;
        if (offset >= cursor && offset <= end) return color || fallback;
        cursor = end;
    }
    return fallback;
}
