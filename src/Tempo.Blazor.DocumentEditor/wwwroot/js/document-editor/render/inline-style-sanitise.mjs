// Phase D — render/inline-style-sanitise.mjs
// Allow-list sanitisers for inline `style` attributes so user-supplied marks
// never inject arbitrary CSS into rendered HTML.
//
// `isSafeInlineCssColor(value)` — accepts `#hex` (3–8 chars), `rgb()/rgba()/
//   hsl()/hsla()` with numeric/comma/percent payload, or a single CSS keyword
//   token (`[a-z][a-z0-9-]{0,31}`). Trims input; empty is rejected.
// `isSafeInlineFontFamily(value)` — short list of common font-family characters
//   (word chars, quotes, commas, periods, spaces, hyphens) up to 160 chars.
// `normalizeInlineFontSize(value)` — bare numbers become `<n>pt`; values with a
//   recognised unit (px/pt/rem/em/%) pass through; anything else is rejected
//   (empty string).
// `createRenderInlineTextHtml({escapeHtml})` → `renderInlineTextHtml(text)` —
//   escapes the text and converts `\n` into `<br data-inline-break="true">`
//   sequences; the trailing newline emits an extra `<br data-caret-placeholder>`
//   so the caret has a landing target after the final break.

import { asText } from '../core/helpers.mjs';

export function isSafeInlineCssColor(value) {
    const text = asText(value).trim();
    if (!text) return false;
    if (/^#[0-9a-f]{3,8}$/i.test(text)) return true;
    if (/^(rgb|rgba|hsl|hsla)\([0-9.,%\s-]+\)$/i.test(text)) return true;
    return /^[a-z][a-z0-9-]{0,31}$/i.test(text);
}

export function isSafeInlineFontFamily(value) {
    const text = asText(value).trim();
    return !!text && /^[\w\s"',.-]{1,160}$/.test(text);
}

export function normalizeInlineFontSize(value) {
    const text = asText(value).trim();
    if (!text) return '';
    if (/^\d+(\.\d+)?$/.test(text)) return text + 'pt';
    if (/^\d+(\.\d+)?(px|pt|rem|em|%)$/i.test(text)) return text;
    return '';
}

export function createRenderInlineTextHtml(options) {
    const opts = options || {};
    if (typeof opts.escapeHtml !== 'function') {
        throw new TypeError(
            'createRenderInlineTextHtml requires options.escapeHtml (function)');
    }
    const { escapeHtml } = opts;
    return function renderInlineTextHtml(text) {
        const source = asText(text);
        if (source.indexOf('\n') < 0) return escapeHtml(source);
        const html = [];
        let segmentStart = 0;
        for (let index = 0; index < source.length; index++) {
            if (source[index] !== '\n') continue;
            if (index > segmentStart) {
                html.push(escapeHtml(source.slice(segmentStart, index)));
            }
            html.push('<br data-inline-break="true">');
            if (index === source.length - 1) {
                html.push('<br data-caret-placeholder="true" aria-hidden="true">');
            }
            segmentStart = index + 1;
        }
        if (segmentStart < source.length) {
            html.push(escapeHtml(source.slice(segmentStart)));
        }
        return html.join('');
    };
}
