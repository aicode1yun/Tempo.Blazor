// Phase D — core/word-boundary.mjs
// Word-boundary navigation used by Ctrl+Left / Ctrl+Right caret movement and
// word-wise selection extension.
//
// `previousWordBoundary(text, offset)` — index of the start of the word at or
//   before `offset`. Skips trailing whitespace, then walks back over the word body.
// `nextWordBoundary(text, offset)` — index just past the word at or after `offset`
//   (word body then trailing whitespace), matching the common editor convention
//   where the caret lands at the start of the following word.

export function previousWordBoundary(text, offset) {
    let index = Math.max(0, Number(offset || 0) - 1);
    while (index > 0 && /\s/.test(text[index])) index--;
    while (index > 0 && !/\s/.test(text[index - 1])) index--;
    return index;
}

export function nextWordBoundary(text, offset) {
    let index = Math.min(text.length, Number(offset || 0));
    while (index < text.length && !/\s/.test(text[index])) index++;
    while (index < text.length && /\s/.test(text[index])) index++;
    return index;
}
