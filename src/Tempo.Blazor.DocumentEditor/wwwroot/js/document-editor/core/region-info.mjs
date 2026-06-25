// Phase D — core/region-info.mjs
// `findRegionInfoForBlock(model, blockId)` — locate which region (Body/Header/Footer/
// TableCell) a block lives in, plus headerFooterId/tableId/cellId/columnIndex when
// applicable. Used by `nextSelectionForOperation` and the autosave/render path to
// produce well-formed selection records.
//
// `operationRegionInfo(model, op, blockId, fallback)` — same but enriches with hints
// from the operation's selection/target/range payload.
//
// `nextSelectionForOperation(model, op, blockId, offset, fallback)` — produce the
// collapsed-caret selection record that should follow an operation's commit.
//
// Pure functions.

import { asArray, asText, sortObject } from './helpers.mjs';
import { normalizeTextExclusionColumnIndex } from './normalize-target.mjs';
import { createSelectionSnapshot } from './selection-snapshot.mjs';

const DEFAULT_INFO = Object.freeze({
    region: 'Body', headerFooterId: null, cellId: null, tableId: null, columnIndex: null,
});

function emptyInfo() {
    return { region: 'Body', headerFooterId: null, cellId: null, tableId: null, columnIndex: null };
}

export function findRegionInfoForBlock(model, blockId) {
    const id = asText(blockId);
    if (!id) return emptyInfo();
    if (asArray(model && model.body && model.body.blocks).some(b => b && b.id === id)) {
        return emptyInfo();
    }
    const headers = asArray(model && model.headers);
    for (let h = 0; h < headers.length; h++) {
        const header = headers[h];
        if (asArray(header && header.blocks).some(b => b && b.id === id)) {
            return { region: 'Header', headerFooterId: header.id || null,
                cellId: null, tableId: null, columnIndex: null };
        }
    }
    const footers = asArray(model && model.footers);
    for (let f = 0; f < footers.length; f++) {
        const footer = footers[f];
        if (asArray(footer && footer.blocks).some(b => b && b.id === id)) {
            return { region: 'Footer', headerFooterId: footer.id || null,
                cellId: null, tableId: null, columnIndex: null };
        }
    }
    let found = null;
    function scanTableBlocks(blocks, owner) {
        asArray(blocks).forEach(block => {
            if (!block || block.type !== 'table') return;
            asArray(block.content && block.content.rows).forEach(row => {
                asArray(row.cells).forEach((cell, columnIndex) => {
                    if (asArray(cell.blocks).some(child => child && child.id === id)) {
                        found = {
                            region: 'TableCell',
                            headerFooterId: (owner && owner.headerFooterId) || null,
                            cellId: cell.id || null,
                            tableId: block.id || null,
                            columnIndex,
                        };
                    }
                    scanTableBlocks(cell.blocks, owner);
                });
            });
        });
    }
    scanTableBlocks(model && model.body && model.body.blocks, { region: 'Body' });
    asArray(model && model.headers).forEach(header => {
        scanTableBlocks(header && header.blocks, {
            region: 'Header',
            headerFooterId: (header && header.id) || null,
        });
    });
    asArray(model && model.footers).forEach(footer => {
        scanTableBlocks(footer && footer.blocks, {
            region: 'Footer',
            headerFooterId: (footer && footer.id) || null,
        });
    });
    return found || emptyInfo();
}

export function operationRegionInfo(model, op, blockId, fallback) {
    const source = (op && (op.beforeSelection || op.BeforeSelection
        || op.selection || op.Selection
        || op.target || op.Target
        || op.range || op.Range)) || {};
    const info = findRegionInfoForBlock(model, blockId);
    const snapshot = createSelectionSnapshot(source || {});
    const sourceRegion = asText(source.region || source.Region || snapshot.region || '');
    const sourceHeaderFooterId = source.headerFooterId || source.HeaderFooterId
        || snapshot.headerFooterId || null;
    const sourceCellId = source.cellId || source.CellId || snapshot.cellId || null;
    const sourceTableId = source.tableId || source.TableId || snapshot.tableId || null;
    const sourceColumnIndex = normalizeTextExclusionColumnIndex(
        source.columnIndex ?? source.ColumnIndex ?? snapshot.columnIndex);

    if (snapshot.blockId === blockId || !snapshot.blockId
        || source.blockId === blockId || source.BlockId === blockId) {
        if (sourceRegion && sourceRegion !== 'Body') info.region = sourceRegion;
        if (sourceHeaderFooterId) info.headerFooterId = sourceHeaderFooterId;
        if (sourceCellId) info.cellId = sourceCellId;
        if (sourceTableId) info.tableId = sourceTableId;
        if (sourceColumnIndex !== null) info.columnIndex = sourceColumnIndex;
    }
    if (fallback && fallback.region && (!info.region || info.region === 'Body')) {
        info.region = fallback.region;
    }
    if (fallback && fallback.headerFooterId && !info.headerFooterId) {
        info.headerFooterId = fallback.headerFooterId;
    }
    if (fallback && fallback.tableId && !info.tableId) info.tableId = fallback.tableId;
    if (fallback && fallback.cellId && !info.cellId) info.cellId = fallback.cellId;
    const fallbackColumnIndex = fallback
        ? normalizeTextExclusionColumnIndex(fallback.columnIndex ?? fallback.ColumnIndex)
        : null;
    if (fallbackColumnIndex !== null
        && (info.columnIndex === null || info.columnIndex === undefined)) {
        info.columnIndex = fallbackColumnIndex;
    }
    return info;
}

export function nextSelectionForOperation(model, op, blockId, offset, fallback) {
    const info = operationRegionInfo(model, op, blockId, fallback);
    return sortObject({
        region: info.region || 'Body',
        blockId: asText(blockId),
        offset: Math.max(0, Number(offset || 0) || 0),
        isCollapsed: true,
        headerFooterId: info.headerFooterId || null,
        cellId: info.cellId || null,
        tableId: info.tableId || null,
        columnIndex: info.columnIndex ?? null,
    });
}
