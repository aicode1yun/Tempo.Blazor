// Phase D — objects/wrap-mode-value.mjs
// `wrapModeToValue` — string/object → numeric ordinal for wire format.
// `wrapModeToCssName` — string → kebab-case CSS class name.
// `wrapModeCreatesTextExclusion` — true for modes that punch a hole in the text flow.

import { normalizeWrapModeName } from './wrap-modes.mjs';

export function wrapModeToValue(value) {
    const source = value && typeof value === 'object'
        ? (value.Mode ?? value.mode ?? value.Value ?? value.value)
        : value;
    const mode = normalizeWrapModeName(source);
    return mode === 'Square' ? 1
        : mode === 'Tight' ? 2
            : mode === 'Through' ? 3
                : mode === 'TopBottom' ? 4
                    : mode === 'BehindText' ? 5
                        : mode === 'InFrontOfText' ? 6
                            : 0;
}

export function wrapModeToCssName(value) {
    const mode = normalizeWrapModeName(value);
    return mode === 'TopBottom'
        ? 'top-bottom'
        : mode === 'BehindText'
            ? 'behind-text'
            : mode === 'InFrontOfText'
                ? 'in-front-of-text'
                : mode.replace(/([a-z])([A-Z])/g, '$1-$2').toLowerCase();
}

export function wrapModeCreatesTextExclusion(wrapMode) {
    const mode = normalizeWrapModeName(wrapMode);
    return mode === 'Square' || mode === 'Tight' || mode === 'Through' || mode === 'TopBottom';
}
