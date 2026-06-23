// Phase D — objects/drawing-kind.mjs
// Normalize/export helpers for the DrawingKind enum (currently only 'Image' is defined
// in the C# enum, but the wire format accepts numeric/string variants from imports).
//
// Pure functions extracted from the legacy IIFE.

import { asText } from '../core/helpers.mjs';

export function normalizeDrawingKindName(value) {
    if (value === undefined || value === null || value === '') return 'Image';
    if (typeof value === 'number') return value === 0 ? 'Image' : String(value);
    const raw = String(value).replace(/\s+/g, '').replace(/-/g, '').toLowerCase();
    if (raw === '0' || raw === 'image' || raw === 'picture') return 'Image';
    return asText(value) || 'Image';
}

// Inverse — wire format is always the numeric ordinal (currently 0 = Image). The legacy
// implementation always returned 0; keeping that behaviour but documenting it explicitly
// so future drawing kinds (e.g. shape, chart) only have to extend this switch.
export function exportDrawingKind(value) {
    const raw = String(value || '').replace(/\s+/g, '').replace(/-/g, '').toLowerCase();
    if (value === 0 || raw === '0' || raw === 'image' || raw === 'picture') return 0;
    return 0;
}
