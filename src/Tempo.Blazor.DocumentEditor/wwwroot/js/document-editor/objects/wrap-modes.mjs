// Phase D — objects/wrap-modes.mjs
// Wrap mode + wrap side enums for inline objects (images, drawings).
// Numeric keys mirror the C# enum ordinal values produced by the abstractions layer.
// String aliases mirror the legacy IIFE's tolerant input parsing (e.g. 'wrap' → 'Square',
// 'breaktext' → 'TopBottom') so the bundled engine and the legacy monolith accept the
// exact same set of inputs.

export const WrapModeNames = Object.freeze({
    0: 'Inline',
    1: 'Square',
    2: 'Tight',
    3: 'Through',
    4: 'TopBottom',
    5: 'BehindText',
    6: 'InFrontOfText',
});

export const WrapSideNames = Object.freeze({
    0: 'BothSides',
    1: 'Left',
    2: 'Right',
    3: 'Largest',
});

export function normalizeWrapModeName(value) {
    if (value === undefined || value === null || value === '') return 'Inline';
    if (typeof value === 'object' && value.value !== undefined) return normalizeWrapModeName(value.value);
    if (typeof value === 'number') return WrapModeNames[value] || 'Inline';
    const raw = String(value).replace(/\s+/g, '').replace(/-/g, '').toLowerCase();
    if (raw === '0' || raw === 'inline' || raw === 'inlined') return 'Inline';
    if (raw === '1' || raw === 'square' || raw === 'wrap') return 'Square';
    if (raw === '2' || raw === 'tight') return 'Tight';
    if (raw === '3' || raw === 'through') return 'Through';
    if (raw === '4' || raw === 'topbottom' || raw === 'topandbottom' || raw === 'breaktext') return 'TopBottom';
    if (raw === '5' || raw === 'behindtext' || raw === 'behind') return 'BehindText';
    if (raw === '6' || raw === 'infrontoftext' || raw === 'front') return 'InFrontOfText';
    return 'Inline';
}

export function normalizeWrapSideName(value) {
    if (value === undefined || value === null || value === '') return 'BothSides';
    if (typeof value === 'object' && value.value !== undefined) return normalizeWrapSideName(value.value);
    if (typeof value === 'number') return WrapSideNames[value] || 'BothSides';
    const raw = String(value).replace(/[\s_.:-]+/g, '').toLowerCase();
    if (raw === '0' || raw === 'both' || raw === 'bothside' || raw === 'bothsides') return 'BothSides';
    if (raw === '1' || raw === 'left' || raw === 'leftside') return 'Left';
    if (raw === '2' || raw === 'right' || raw === 'rightside') return 'Right';
    if (raw === '3' || raw === 'largest' || raw === 'larger' || raw === 'largestside') return 'Largest';
    return 'BothSides';
}

// Inverse of normalizeWrapSideName — returns the numeric ordinal of the side name.
export function wrapSideToValue(value) {
    const side = normalizeWrapSideName(value);
    if (side === 'Left') return 1;
    if (side === 'Right') return 2;
    if (side === 'Largest') return 3;
    return 0;
}
