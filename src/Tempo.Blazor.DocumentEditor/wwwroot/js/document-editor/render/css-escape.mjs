// Phase D — render/css-escape.mjs
// `cssEscape(value)` — DOM-friendly identifier escaper for `querySelector` strings.
// Prefers `CSS.escape` (browser-native) when available, falls back to a basic escape
// suitable for embedding in attribute-value selectors (`[data-foo="<value>"]`).

import { asText } from '../core/helpers.mjs';

export function cssEscape(value) {
    if (typeof globalThis !== 'undefined'
        && globalThis.CSS
        && typeof globalThis.CSS.escape === 'function') {
        return globalThis.CSS.escape(value);
    }
    return asText(value).replace(/"/g, '\\"');
}
