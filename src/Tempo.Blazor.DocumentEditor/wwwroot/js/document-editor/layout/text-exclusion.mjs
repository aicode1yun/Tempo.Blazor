// Phase D — layout/text-exclusion.mjs
// Text-exclusion scope helpers — used by the floating-object layout engine to identify
// which page region / table cell / column an exclusion zone applies to.

import { asText, sortObject } from '../core/helpers.mjs';
import { normalizeTextExclusionColumnIndex } from '../core/normalize-target.mjs';
import { normalizeAnchorRegionName } from '../objects/anchor-region.mjs';

// Page index always coerces to a non-negative integer (default 0).
export function normalizeTextExclusionPageIndex(object) {
    const value = object && (
        object.pageIndex ?? object.PageIndex
        ?? object.anchorPageIndex ?? object.AnchorPageIndex);
    const number = Number(value);
    return Number.isFinite(number) && number >= 0 ? Math.floor(number) : 0;
}

// Stable scope key — used as a Map key when grouping exclusion zones per page-region.
// Format: `pageIndex|region|headerFooterId|tableId|cellId[|columnIndex]`.
export function createTextExclusionScopeKey(pageIndex, region, headerFooterId, tableId, cellId, columnIndex) {
    const parts = [
        String(Math.max(0, Number(pageIndex || 0) || 0)),
        normalizeAnchorRegionName(region),
        asText(headerFooterId || ''),
        asText(tableId || ''),
        asText(cellId || ''),
    ];
    const normalizedColumn = normalizeTextExclusionColumnIndex(columnIndex);
    if (normalizedColumn !== null) parts.push(String(normalizedColumn));
    return parts.join('|');
}

// Normalize an exclusion-scope record. Auto-generates `scopeKey` when caller didn't
// provide one. The output is `sortObject`-stable so equal scopes serialize identically.
export function readTextExclusionScope(value) {
    const source = value || {};
    const pageIndex = normalizeTextExclusionPageIndex(source);
    const region = normalizeAnchorRegionName(
        source.region || source.Region
        || source.anchorRegion || source.AnchorRegion || 'Body');
    const headerFooterId = source.headerFooterId || source.HeaderFooterId
        || source.anchorHeaderFooterId || source.AnchorHeaderFooterId || null;
    const tableId = source.tableId || source.TableId
        || source.anchorTableId || source.AnchorTableId || null;
    const cellId = source.cellId || source.CellId
        || source.anchorCellId || source.AnchorCellId || null;
    const columnIndex = normalizeTextExclusionColumnIndex(
        source.columnIndex ?? source.ColumnIndex
        ?? source.anchorColumnIndex ?? source.AnchorColumnIndex);
    return sortObject({
        pageIndex,
        region,
        headerFooterId,
        tableId,
        cellId,
        columnIndex,
        scopeKey: asText(source.scopeKey || source.ScopeKey
            || createTextExclusionScopeKey(pageIndex, region, headerFooterId, tableId, cellId, columnIndex)),
    });
}
