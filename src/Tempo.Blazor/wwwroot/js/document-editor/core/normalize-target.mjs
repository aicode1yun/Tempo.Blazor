// Phase D — core/normalize-target.mjs
// Pure normalisers for target/range coordinates used throughout the engine. Accepts both
// camelCase (JS-native) and PascalCase (C#-serialised) keys so the engine can consume
// either shape after a JSON round-trip.

import { asText } from './helpers.mjs';

// Accepts null, undefined, '', or a finite non-negative number. Anything else (NaN,
// negative, non-numeric) becomes null. Used as the table-cell-column index disambiguator
// for text-exclusion zones around floating objects.
export function normalizeTextExclusionColumnIndex(value) {
    if (value === null || value === undefined || value === '') return null;
    const number = Number(value);
    return Number.isFinite(number) && number >= 0 ? Math.floor(number) : null;
}

export function normalizeTarget(value) {
    const target = value || {};
    return {
        blockId: asText(target.blockId || target.BlockId),
        objectId: asText(
            target.objectId || target.ObjectId
            || target.activeObjectId || target.ActiveObjectId),
        offset: Number(target.offset ?? target.Offset ?? 0),
        region: target.region || target.Region || null,
        headerFooterId: target.headerFooterId || target.HeaderFooterId || null,
        tableId: target.tableId || target.TableId
            || target.activeTableId || target.ActiveTableId || null,
        cellId: target.cellId || target.CellId
            || target.activeTableCellId || target.ActiveTableCellId || null,
        columnIndex: normalizeTextExclusionColumnIndex(target.columnIndex ?? target.ColumnIndex),
        affinity: (target.affinity === 'before' || target.Affinity === 'before') ? 'before' : 'after',
        virtualCaret: target.virtualCaret === true || target.VirtualCaret === true,
        layoutIntervalId: target.layoutIntervalId || target.LayoutIntervalId || null,
        visualHintLineId: target.visualHintLineId || target.VisualHintLineId || null,
    };
}

export function normalizeRange(value) {
    const range = value || {};
    const start = Number(range.start ?? range.Start ?? 0);
    const end = Number(range.end ?? range.End ?? start);
    return {
        blockId: asText(range.blockId || range.BlockId),
        start: Math.min(start, end),
        end: Math.max(start, end),
        region: range.region || range.Region || null,
        headerFooterId: range.headerFooterId || range.HeaderFooterId || null,
        tableId: range.tableId || range.TableId
            || range.activeTableId || range.ActiveTableId || null,
        cellId: range.cellId || range.CellId
            || range.activeTableCellId || range.ActiveTableCellId || null,
        columnIndex: normalizeTextExclusionColumnIndex(range.columnIndex ?? range.ColumnIndex),
    };
}
