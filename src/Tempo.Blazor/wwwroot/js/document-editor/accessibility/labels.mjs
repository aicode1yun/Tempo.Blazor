// Phase D — accessibility/labels.mjs
// `formatA11yLabel` resolves a localised a11y label template against runtime values.
// Supports the placeholder `{0}` for the page number; missing placeholders fall through
// unchanged so callers can hand in either localised strings or pre-formatted text.

import { asText } from '../core/helpers.mjs';

export function formatA11yLabel(template, pageNumber) {
    const text = asText(template || '');
    if (!text) return '';
    return text.indexOf('{0}') >= 0
        ? text.replace(/\{0\}/g, String(pageNumber || 1))
        : text;
}
