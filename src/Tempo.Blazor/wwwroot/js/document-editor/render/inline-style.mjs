// Phase D — render/inline-style.mjs
// Safe inline-style / attribute helpers used by the HTML render path.
//
// `isSafeInlineCssColor(value)` — true for hex (#rgb..#rgbaaa), rgb()/rgba(),
//   hsl()/hsla(), and bare CSS keyword colors (letters/digits/dashes, ≤41 chars).
//   Anything else (e.g. `url(...)`, expressions) is rejected to avoid CSS injection.
// `safeInlineColor(value)` — the trimmed color when safe, else `''`.
// `buildInlineStyleAttribute(styles)` — join a `{prop: value}` map into a
//   `prop:value;prop:value` string, skipping null/undefined/empty values.
// `escapeAttribute(value)` — HTML-escape a value for use inside a double-quoted
//   attribute (`&`, `"`, `<`, `>`).

import { asText } from '../core/helpers.mjs';
// Single source of truth for the color allow-list lives in inline-style-sanitise.mjs
// (parity with the legacy monolith). Re-export so existing importers keep working.
import { isSafeInlineCssColor } from './inline-style-sanitise.mjs';
export { isSafeInlineCssColor };

export function safeInlineColor(value) {
    return isSafeInlineCssColor(value) ? asText(value).trim() : '';
}

export function buildInlineStyleAttribute(styles) {
    const entries = Object.keys(styles || {}).filter(function (key) {
        return styles[key] !== null
            && styles[key] !== undefined
            && styles[key] !== '';
    });
    if (!entries.length) return '';
    return entries.map(function (key) {
        return key + ':' + styles[key];
    }).join(';');
}

export function escapeAttribute(value) {
    return asText(value)
        .replace(/&/g, '&amp;')
        .replace(/"/g, '&quot;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;');
}
