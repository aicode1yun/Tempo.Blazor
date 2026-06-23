// Phase D — render/run-text.mjs
// Resolve display text for inline runs at render time. Handles field-run substitutions
// (`pageNumber` / `pageCount` etc.) so the renderer never has to know about that
// mapping itself.

import { asArray, asText } from '../core/helpers.mjs';

// For a single inline run, return the string that should be drawn on screen. For
// non-field runs this is just `run.text`. For field runs of kind `PageNumber` /
// `PageCount` (and aliases) the value is the current page number / total page count.
export function resolveInlineRunDisplayText(run, pageNumber, totalPages) {
    const fieldType = String((run && (run.fieldType || run.FieldType)) || '').toLowerCase();
    if (run && run.kind === 'field') {
        if (fieldType.indexOf('pagenumber') >= 0
            || fieldType.indexOf('page-number') >= 0
            || fieldType === 'page') {
            return String(pageNumber || 1);
        }
        if (fieldType.indexOf('pagecount') >= 0
            || fieldType.indexOf('page-count') >= 0
            || fieldType.indexOf('numpages') >= 0) {
            return String(totalPages || 1);
        }
    }
    return asText(run && run.text);
}

// Concatenate the display text of all runs in a paragraph for the given page.
export function textFromRunsForRender(runs, pageNumber, totalPages) {
    return asArray(runs)
        .map(run => resolveInlineRunDisplayText(run, pageNumber, totalPages))
        .join('');
}
