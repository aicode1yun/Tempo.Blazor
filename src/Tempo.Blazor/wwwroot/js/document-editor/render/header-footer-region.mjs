// Phase D — render/header-footer-region.mjs
// `normalizeHeaderFooterScope(scope)` — canonicalises a region scope name into
//   one of `'FirstPage'` / `'EvenPage'` / `'Primary'` based on case-insensitive
//   substring match. Unknown values fall back to `'Primary'`.
// `resolveHeaderFooterRegion(model, type, pageNumber)` — picks the header or
//   footer region that applies to a given page number, preferring scope-specific
//   regions (`FirstPage` for page 1, `EvenPage` for even pages, `Primary` otherwise)
//   and falling back to the `Primary` region or the first available one.

import { asArray } from '../core/helpers.mjs';

export function normalizeHeaderFooterScope(scope) {
    const value = String(scope || '').toLowerCase();
    if (value.indexOf('first') >= 0) return 'FirstPage';
    if (value.indexOf('even') >= 0) return 'EvenPage';
    return 'Primary';
}

export function resolveHeaderFooterRegion(model, type, pageNumber) {
    const list = type === 'footer'
        ? asArray(model && model.footers)
        : asArray(model && model.headers);
    if (!list.length) return null;
    const desiredScope = pageNumber === 1
        ? 'FirstPage'
        : (pageNumber % 2 === 0 ? 'EvenPage' : 'Primary');
    const scoped = list.find(function (region) {
        return normalizeHeaderFooterScope(region.scope) === desiredScope;
    });
    if (scoped) return scoped;
    return list.find(function (region) {
        return normalizeHeaderFooterScope(region.scope) === 'Primary';
    }) || list[0] || null;
}
