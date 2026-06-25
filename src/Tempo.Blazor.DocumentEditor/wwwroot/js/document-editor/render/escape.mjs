// Phase D — render/escape.mjs
// HTML-attribute-safe escape used by the renderer for any text that goes into innerHTML
// or HTML attribute values. Mirrors the legacy `_escape` exactly.
//
// Quotes are escaped because the same function is used inside double-quoted attribute
// contexts (e.g. `<span title="${escape(text)}">`).

import { asText } from '../core/helpers.mjs';

export function escapeHtml(value) {
    return asText(value)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;');
}
