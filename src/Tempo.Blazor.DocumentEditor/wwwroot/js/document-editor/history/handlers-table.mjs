// Phase D — history/handlers-table.mjs
// `createTableHandlers({findBlockContainer, findCell, importBlock, stableId, clone})`
// factory → `applyInsertTable(model, op, differ)` (inserts a Table block after the
// target block container) + `applyUpdateTableCell(model, op, differ)` (replaces a
// cell's `blocks` array via importBlock).

import { asArray, clone, stableId } from '../core/helpers.mjs';

export function createTableHandlers(options) {
    const opts = options || {};
    const required = ['findBlockContainer', 'findCell', 'importBlock'];
    for (const key of required) {
        if (typeof opts[key] !== 'function') {
            throw new TypeError(
                `createTableHandlers requires options.${key} (function)`);
        }
    }
    const { findBlockContainer, findCell, importBlock } = opts;
    const importBlockNormaliser = opts.normalizeTarget
        || ((t) => Object.assign({}, t || {}));

    function applyInsertTable(model, op, differ) {
        const target = importBlockNormaliser(op.target || op.Target);
        const container = findBlockContainer(model, target.blockId);
        if (!container || !container.block) {
            return {
                ok: false,
                errors: [{
                    code: 'missing-target-block',
                    path: 'operation.target.blockId',
                    blockId: target.blockId,
                }],
            };
        }
        const rows = Number(op.rows || op.Rows || 2);
        const columns = Number(op.columns || op.Columns || 2);
        const tableId = op.tableId || op.TableId
            || op.blockId || op.BlockId
            || stableId('table-block', Date.now());
        const table = { Style: clone(op.style || op.Style || {}), Rows: [] };
        for (let r = 0; r < rows; r++) {
            const row = { Id: tableId + '-row-' + r, Cells: [] };
            for (let c = 0; c < columns; c++) {
                const cellId = tableId + '-r' + r + '-c' + c;
                row.Cells.push({
                    Id: cellId,
                    Blocks: [{
                        Id: cellId + '-p',
                        Type: 'Paragraph',
                        Content: { Inlines: [{ Id: cellId + '-r', Text: '' }] },
                    }],
                });
            }
            table.Rows.push(row);
        }
        const block = importBlock({
            Id: tableId, Type: 'Table', Content: table,
        }, 'insert-table');
        container.blocks.splice(container.index + 1, 0, block);
        differ.record({
            objectChange: { blockId: block.id, type: 'insert-table' },
            invalidatedLayoutScopes: [container.block.id, block.id],
        });
        return {
            ok: true,
            invalidatedLayoutScopes: [container.block.id, block.id],
            nextSelection: {
                region: 'Body',
                blockId: block.id,
                offset: 0,
                isCollapsed: true,
            },
            insertedBlockId: block.id,
        };
    }

    function applyUpdateTableCell(model, op, differ) {
        const cellId = op.cellId || op.CellId;
        const cell = findCell(model, cellId);
        if (!cell) {
            return {
                ok: false,
                errors: [{ code: 'missing-table-cell', cellId: cellId }],
            };
        }
        if (Array.isArray(op.blocks || op.Blocks)) {
            cell.blocks = asArray(op.blocks || op.Blocks).map(function (block, index) {
                return importBlock(block, cellId + '-updated-' + index);
            });
        }
        differ.record({
            attributeChange: { cellId: cellId, attributeName: 'blocks' },
            invalidatedLayoutScopes: [cellId],
        });
        return {
            ok: true,
            invalidatedLayoutScopes: [cellId],
            nextSelection: {
                region: 'Body',
                blockId: cell.blocks[0] && cell.blocks[0].id,
                offset: 0,
                isCollapsed: true,
            },
        };
    }

    return Object.freeze({
        applyInsertTable,
        applyUpdateTableCell,
    });
}
