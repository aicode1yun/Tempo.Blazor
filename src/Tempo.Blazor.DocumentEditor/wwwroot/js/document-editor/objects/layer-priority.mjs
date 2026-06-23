// Phase D — objects/layer-priority.mjs
// `drawingLayerForWrapMode(wrapMode)` — maps a wrap mode to its render layer
// (`behind-text` / `in-front-of-text` / `object`).
// `hitTestLayerPriority(layerName, wrapMode)` — z-priority used by the object
// hit-test pipeline. Higher value wins. InFrontOfText=30, BehindText=0, else=10.

import { normalizeWrapModeName } from './wrap-modes.mjs';

export function drawingLayerForWrapMode(wrapMode) {
    const mode = normalizeWrapModeName(wrapMode);
    if (mode === 'BehindText') return 'behind-text';
    if (mode === 'InFrontOfText') return 'in-front-of-text';
    return 'object';
}

export function hitTestLayerPriority(layerName, wrapMode) {
    const layer = String(layerName || '').toLowerCase();
    const mode = normalizeWrapModeName(wrapMode);
    if (layer === 'infrontoftext'
        || layer === 'in-front-of-text'
        || mode === 'InFrontOfText') return 30;
    if (layer === 'behindtext'
        || layer === 'behind-text'
        || mode === 'BehindText') return 0;
    return 10;
}
