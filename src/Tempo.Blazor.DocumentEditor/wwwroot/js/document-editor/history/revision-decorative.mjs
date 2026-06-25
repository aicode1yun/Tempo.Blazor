// Phase D — history/revision-decorative.mjs
// `revisionDecorativeStyle(revision)` — returns the CSS style descriptor used to
// decorate a revision's runs in the editor (color + underline/strike). The payload
// can override the default via `payload.decorativeStyle` (e.g. for custom revision
// kinds). Fallback colors: Insertion=green, Deletion=red, FormatChange=purple.

import { normalizeRevisionType } from '../core/revision-normalize.mjs';

export function revisionDecorativeStyle(revision) {
    const type = normalizeRevisionType(revision && revision.type);
    if (revision && revision.payload && revision.payload.decorativeStyle) {
        return revision.payload.decorativeStyle;
    }
    if (type === 'Insertion') return { color: '#008000', underline: true };
    if (type === 'Deletion') return { color: '#b91c1c', strike: true };
    if (type === 'FormatChange') return { color: '#7c3aed', underline: true };
    return {};
}
