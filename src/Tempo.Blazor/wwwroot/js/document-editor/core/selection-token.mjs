// Phase D — core/selection-token.mjs
// Pure-function support set for the stable-selection-token pipeline.
//
// Selection tokens are opaque JSON strings the JS engine hands to C# (and back),
// representing a selection plus a model fingerprint. They survive remote-op rebases
// when the fingerprint still matches, and are rejected as "stale" otherwise.
//
// This module exports the cheap reader helpers and the region normaliser. The
// boundary/data builders (`createSelectionTokenBoundary`, `createStableSelectionTokenData`,
// `withStableSelectionToken`) live in a follow-up factory so they can stay decoupled
// from `_findBlock` / `_findLimitForBlock`.

import { asText, clone, sortObject } from './helpers.mjs';

export function normalizeSelectionTokenRegion(value, selection) {
    const snapshot = selection || {};
    const raw = asText(value
        || snapshot.region || snapshot.Region
        || snapshot.activeRegion || snapshot.ActiveRegion
        || 'Body').trim();
    const lower = raw.toLowerCase();
    if (snapshot.activeTableCellId || snapshot.ActiveTableCellId
        || snapshot.cellId || snapshot.CellId
        || lower === 'tablecell' || lower === 'table-cell') {
        return 'tableCell';
    }
    if (lower === 'header' || lower === 'headers') return 'header';
    if (lower === 'footer' || lower === 'footers') return 'footer';
    if (lower === 'caption') return 'caption';
    if (lower === 'image' || lower === 'object') return 'image';
    return 'body';
}

export function readSelectionTokenValue(value) {
    if (!value || typeof value !== 'object') return null;
    return value.selectionToken
        || value.SelectionToken
        || value.stableSelectionToken
        || value.StableSelectionToken
        || value.token
        || value.Token
        || null;
}

export function parseSelectionTokenData(value) {
    if (!value) return null;
    if (typeof value === 'object') return sortObject(clone(value));
    if (typeof value !== 'string') return null;
    try {
        return sortObject(JSON.parse(value));
    } catch {
        return null;
    }
}

export function readSelectionTokenData(value) {
    if (!value || typeof value !== 'object') return null;
    return parseSelectionTokenData(readSelectionTokenValue(value))
        || parseSelectionTokenData(value.selectionTokenData || value.SelectionTokenData)
        || null;
}
