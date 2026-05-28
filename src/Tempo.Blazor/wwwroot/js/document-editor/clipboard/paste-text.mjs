// Phase D — clipboard/paste-text.mjs
// `normalizePasteText` — strip HTML tags from pasted text, collapse common block-level
// elements (`<br>`, `</p><p>`) into newlines, normalise CR/CRLF to LF.
//
// Pure function — accepts any input, returns a plain string.

import { asText } from '../core/helpers.mjs';

export function normalizePasteText(value) {
    return asText(value)
        .replace(/<br\s*\/?>/gi, '\n')
        .replace(/<\/p>\s*<p[^>]*>/gi, '\n')
        .replace(/<[^>]+>/g, '')
        .replace(/\r\n?/g, '\n');
}
