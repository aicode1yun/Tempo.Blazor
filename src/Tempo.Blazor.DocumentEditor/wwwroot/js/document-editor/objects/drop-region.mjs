// Phase D — objects/drop-region.mjs
// `normalizeDropRegionName(value, tableCellId?)` — canonicalises a region name for
// image-drop / drag-anchor flows (Body/Header/Footer/TableCell). A `tableCellId`
// forces TableCell regardless of `value`.
// `anchorRegionForNearestTextPosition(position)` — restricts the result to one of
// Body / Header / Footer / TableCell (no Image/Caption/Document).
// `imageAnchorScopeKey(value)` / `imageDropScopeKey(position)` — composite scope
// keys for image anchoring (region + headerFooterId + tableId + cellId + columnIndex).
// `canDropImageInNearestTextScope(object, position, options)` — predicate gating
// image cross-region drops.

import { asText, sortObject } from '../core/helpers.mjs';
import {
    normalizeAnchorRegionName,
} from './anchor-region.mjs';
import {
    normalizeTextExclusionColumnIndex,
} from '../core/normalize-target.mjs';

export function normalizeDropRegionName(value, tableCellId) {
    const raw = asText(value || '').trim().toLowerCase();
    if (tableCellId || raw === 'tablecell' || raw === 'table-cell') return 'TableCell';
    if (raw === 'header' || raw === 'headers') return 'Header';
    if (raw === 'footer' || raw === 'footers') return 'Footer';
    return 'Body';
}

export function anchorRegionForNearestTextPosition(position) {
    const region = normalizeDropRegionName(
        position && position.region,
        position && position.cellId);
    if (region === 'Header' || region === 'Footer' || region === 'TableCell') return region;
    return 'Body';
}

export function imageAnchorScopeKey(value) {
    const source = value || {};
    const region = normalizeAnchorRegionName(
        source.anchorRegion || source.region || source.Region || 'Body');
    return sortObject({
        region: region,
        headerFooterId: asText(
            source.anchorHeaderFooterId
            || source.headerFooterId
            || source.HeaderFooterId
            || ''),
        tableId: asText(source.anchorTableId || source.tableId || source.TableId || ''),
        cellId: asText(source.anchorCellId || source.cellId || source.CellId || ''),
        columnIndex: normalizeTextExclusionColumnIndex(
            source.anchorColumnIndex ?? source.columnIndex ?? source.ColumnIndex),
    });
}

export function imageDropScopeKey(position) {
    const source = position || {};
    const region = anchorRegionForNearestTextPosition(source);
    return sortObject({
        region: region,
        headerFooterId: asText(source.headerFooterId || source.HeaderFooterId || ''),
        tableId: asText(source.tableId || source.TableId || ''),
        cellId: asText(source.cellId || source.CellId || ''),
        columnIndex: normalizeTextExclusionColumnIndex(
            source.columnIndex ?? source.ColumnIndex),
    });
}

export function canDropImageInNearestTextScope(object, position, options) {
    const opts = options || {};
    if (opts.allowCrossRegionDrop === true || opts.AllowCrossRegionDrop === true) {
        return true;
    }
    const source = imageAnchorScopeKey(object);
    const target = imageDropScopeKey(position);
    if (source.region !== target.region) return false;
    if ((source.region === 'Header' || source.region === 'Footer')
        && source.headerFooterId
        && target.headerFooterId
        && source.headerFooterId !== target.headerFooterId) {
        return false;
    }
    if (source.region === 'TableCell'
        && source.cellId
        && target.cellId
        && source.cellId !== target.cellId) {
        return false;
    }
    return true;
}
