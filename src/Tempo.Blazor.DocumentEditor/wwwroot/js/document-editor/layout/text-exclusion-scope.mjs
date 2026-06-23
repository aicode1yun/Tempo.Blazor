// Phase D — layout/text-exclusion-scope.mjs
// `createTextExclusionScopeDescriptor(options)` — builds a scope descriptor used
// to filter text exclusions by page/region/header-footer/table/cell/column. If no
// scope field is set, returns `{enabled: false, scopeKey: ''}` (matches everything).
// Explicit `scopeKey` enables strict-key match.
//
// `textExclusionMatchesScope(exclusion, descriptor)` — predicate. When descriptor
// has `strictScopeKey=true`, only matching scopeKey wins; otherwise per-field equality.

import { asText, sortObject } from '../core/helpers.mjs';
import { readTextExclusionScope } from './text-exclusion.mjs';
import { normalizeAnchorRegionName } from '../objects/anchor-region.mjs';

const SCOPE_FIELDS = [
    'pageIndex', 'PageIndex',
    'region', 'Region',
    'headerFooterId', 'HeaderFooterId',
    'tableId', 'TableId',
    'cellId', 'CellId',
    'columnIndex', 'ColumnIndex',
];

export function createTextExclusionScopeDescriptor(options) {
    const opts = options || {};
    const hasExplicitScopeKey = asText(opts.scopeKey || opts.ScopeKey || '') !== '';
    const hasScopeField = hasExplicitScopeKey
        || SCOPE_FIELDS.some(function (field) {
            return Object.prototype.hasOwnProperty.call(opts, field);
        });
    if (!hasScopeField) return { enabled: false, scopeKey: '' };
    const scope = readTextExclusionScope(opts);
    if (hasExplicitScopeKey) {
        scope.scopeKey = asText(opts.scopeKey || opts.ScopeKey || '');
        scope.strictScopeKey = true;
    }
    scope.enabled = true;
    return sortObject(scope);
}

export function textExclusionMatchesScope(exclusion, descriptor) {
    const scope = descriptor || {};
    if (scope.enabled !== true) return true;
    if (!exclusion) return false;
    const candidate = readTextExclusionScope(exclusion);
    if (candidate.scopeKey && scope.scopeKey && candidate.scopeKey === scope.scopeKey) {
        return true;
    }
    if (scope.strictScopeKey === true) return false;
    if (Number(candidate.pageIndex || 0) !== Number(scope.pageIndex || 0)) return false;
    if (normalizeAnchorRegionName(candidate.region)
        !== normalizeAnchorRegionName(scope.region)) return false;
    if (asText(candidate.headerFooterId || '')
        !== asText(scope.headerFooterId || '')) return false;
    if (asText(candidate.tableId || '') !== asText(scope.tableId || '')) return false;
    if (asText(candidate.cellId || '') !== asText(scope.cellId || '')) return false;
    if (candidate.columnIndex !== null
        && scope.columnIndex !== null
        && candidate.columnIndex !== scope.columnIndex) return false;
    return true;
}
