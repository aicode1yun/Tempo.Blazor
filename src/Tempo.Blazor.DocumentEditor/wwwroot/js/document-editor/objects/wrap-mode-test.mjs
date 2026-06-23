// Phase D — objects/wrap-mode-test.mjs
// Pure helpers used by JS-side scenario tests to round-trip wrap-mode / wrap-side /
// horizontal-position values into both numeric (`value`) and CSS-friendly (`css`)
// forms. Mirrors the legacy IIFE's `testWrapMode` / `testWrapSide` / `testHorizontalPosition`.

import { asText } from '../core/helpers.mjs';
import {
    normalizeWrapModeName,
    normalizeWrapSideName,
    wrapSideToValue,
} from './wrap-modes.mjs';
import {
    wrapModeToValue,
    wrapModeToCssName,
} from './wrap-mode-value.mjs';

export function testWrapMode(value) {
    const mode = normalizeWrapModeName(value);
    return { value: wrapModeToValue(mode), css: wrapModeToCssName(mode) };
}

export function testWrapSide(value) {
    const side = normalizeWrapSideName(value);
    return {
        value: wrapSideToValue(side),
        name: side,
        css: side === 'BothSides'
            ? 'both-sides'
            : side.replace(/([a-z])([A-Z])/g, '$1-$2').toLowerCase(),
    };
}

export function testHorizontalPosition(value) {
    if (value === null || value === undefined || value === '') return null;
    if (typeof value === 'number') {
        if (value === 0) return { value: 0, css: 'left' };
        if (value === 1) return { value: 1, css: 'center' };
        if (value === 2) return { value: 2, css: 'right' };
        return null;
    }
    const key = asText(value).toLowerCase();
    if (key === 'left' || key === 'start') return { value: 0, css: 'left' };
    if (key === 'center' || key === 'middle') return { value: 1, css: 'center' };
    if (key === 'right' || key === 'end') return { value: 2, css: 'right' };
    return null;
}
