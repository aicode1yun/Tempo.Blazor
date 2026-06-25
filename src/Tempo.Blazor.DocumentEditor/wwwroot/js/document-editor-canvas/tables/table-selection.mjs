export function findTableCellByBlockId(model, blockId) {
    const id = String(blockId || '');
    if (!id) {
        return null;
    }

    for (const tableEntry of tableEntries(model)) {
        for (let rowIndex = 0; rowIndex < tableEntry.rows.length; rowIndex += 1) {
            const row = tableEntry.rows[rowIndex];
            for (let cellIndex = 0; cellIndex < row.cells.length; cellIndex += 1) {
                const cell = row.cells[cellIndex];
                const blocks = Array.isArray(cell?.blocks) ? cell.blocks : [];
                if (blocks.some(block => containsBlock(block, id))) {
                    return { ...tableEntry, row, cell, rowIndex, cellIndex };
                }
            }
        }
    }

    return null;
}

export function firstEditablePositionInCell(cell) {
    const block = firstEditableBlock(cell);
    return block ? { blockId: block.id, offset: 0 } : null;
}

export function lastEditablePositionInCell(cell) {
    const block = lastEditableBlock(cell);
    return block ? { blockId: block.id, offset: blockText(block).length } : null;
}

export function tableEntries(model) {
    const blocks = Array.isArray(model?.body?.blocks) ? model.body.blocks : [];
    return blocks
        .map((block, blockIndex) => ({ block, blockIndex }))
        .filter(entry => String(entry.block?.type || entry.block?.content?.type || '').toLowerCase() === 'table'
            && Array.isArray(entry.block?.content?.table?.rows))
        .map(entry => ({
            tableBlock: entry.block,
            tableIndex: entry.blockIndex,
            table: entry.block.content.table,
            rows: normalizeRows(entry.block.content.table.rows),
        }));
}

export function cellRangeFromSelection(model, selection) {
    const anchor = findTableCellByBlockId(model, selection?.anchor?.blockId);
    const focus = findTableCellByBlockId(model, selection?.focus?.blockId);
    if (!anchor || !focus || anchor.tableBlock.id !== focus.tableBlock.id) {
        return anchor ? { table: anchor, startRow: anchor.rowIndex, endRow: anchor.rowIndex, startCell: anchor.cellIndex, endCell: anchor.cellIndex } : null;
    }

    return {
        table: anchor,
        startRow: Math.min(anchor.rowIndex, focus.rowIndex),
        endRow: Math.max(anchor.rowIndex, focus.rowIndex),
        startCell: Math.min(anchor.cellIndex, focus.cellIndex),
        endCell: Math.max(anchor.cellIndex, focus.cellIndex),
    };
}

function normalizeRows(rows) {
    if (!Array.isArray(rows)) {
        return [];
    }

    rows.forEach((row, rowIndex) => {
        row.id = row.id || `row-${rowIndex + 1}`;
        row.cells = Array.isArray(row.cells) ? row.cells : [];
    });
    return rows;
}

function containsBlock(block, blockId) {
    if (String(block?.id || '') === blockId) {
        return true;
    }

    const rows = block?.content?.table?.rows;
    if (!Array.isArray(rows)) {
        return false;
    }

    return rows.some(row => (row.cells || []).some(cell => (cell.blocks || []).some(child => containsBlock(child, blockId))));
}

function firstEditableBlock(cell) {
    return (Array.isArray(cell?.blocks) ? cell.blocks : []).find(isEditableBlock) || null;
}

function lastEditableBlock(cell) {
    const blocks = (Array.isArray(cell?.blocks) ? cell.blocks : []).filter(isEditableBlock);
    return blocks[blocks.length - 1] || null;
}

function isEditableBlock(block) {
    const type = String(block?.type || block?.content?.type || '').toLowerCase();
    return type === 'paragraph' || type === 'heading' || type === 'list' || type === 'quote';
}

function blockText(block) {
    return (block?.content?.runs || []).map(run => {
        if (String(run?.type || 'text') === 'field') {
            return String(run?.field?.displayText || run?.field?.fallbackText || '');
        }

        return String(run?.text || '');
    }).join('');
}
