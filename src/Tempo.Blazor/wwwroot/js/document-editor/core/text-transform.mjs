// Text transforms used by the layout/render pipeline. These helpers intentionally
// preserve UTF-16 length so model offsets and caret positions keep mapping 1:1.

export function uppercasePreservingLength(text) {
    return Array.from(String(text || '')).map(char => {
        const upper = char.toUpperCase();
        return upper.length === char.length ? upper : char;
    }).join('');
}
