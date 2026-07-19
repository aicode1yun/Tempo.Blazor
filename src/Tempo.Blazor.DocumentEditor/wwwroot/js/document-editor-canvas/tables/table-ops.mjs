import {
    cellRangeFromSelection,
    findTableCellByBlockId,
    firstEditablePositionInCell,
    lastEditablePositionInCell,
} from './table-selection.mjs';

const TABLE_COMMAND_ALIASES = new Map([
    ['inserttable', 'insertTable'],
    ['deletetable', 'deleteTable'],
    ['toggletableheaderrow', 'toggleHeaderRow'],
    ['toggleheaderrow', 'toggleHeaderRow'],
    ['addtablerow', 'insertRowAfter'],
    ['inserttablerow', 'insertRowAfter'],
    ['inserttablerowafter', 'insertRowAfter'],
    ['inserttablerowbefore', 'insertRowBefore'],
    ['insertrow', 'insertRowAfter'],
    ['insertrowafter', 'insertRowAfter'],
    ['insertrowbefore', 'insertRowBefore'],
    ['addtablecolumn', 'insertColumnAfter'],
    ['inserttablecolumn', 'insertColumnAfter'],
    ['inserttablecolumnafter', 'insertColumnAfter'],
    ['inserttablecolumnbefore', 'insertColumnBefore'],
    ['insertcolumn', 'insertColumnAfter'],
    ['insertcolumnafter', 'insertColumnAfter'],
    ['insertcolumnbefore', 'insertColumnBefore'],
    ['deletetablerow', 'deleteRow'],
    ['deleterow', 'deleteRow'],
    ['deletetablecolumn', 'deleteColumn'],
    ['deletecolumn', 'deleteColumn'],
    ['mergetablecells', 'mergeCells'],
    ['mergecells', 'mergeCells'],
    ['splittablecell', 'splitCell'],
    ['splitcell', 'splitCell'],
    ['resizetablecolumn', 'resizeColumn'],
    ['resizecolumn', 'resizeColumn'],
    ['settablecellformat', 'setCellFormat'],
    ['setcellformat', 'setCellFormat'],
    ['navigatetablecell', 'navigateCell'],
    ['sorttable', 'sortTable'],
    ['settableformula', 'setTableFormula'],
    ['tableformula', 'setTableFormula'],
    ['setcellmargins', 'setCellMargins'],
    ['settablecellmargins', 'setCellMargins'],
    ['setcellborders', 'setCellBorders'],
    ['settablecellborders', 'setCellBorders'],
    ['converttabletotext', 'convertTableToText'],
    ['converttexttotable', 'convertTextToTable'],
]);

export function isTableCommand(commandId) {
    return TABLE_COMMAND_ALIASES.has(compactCommandId(commandId));
}

export function canonicalTableCommandId(commandId) {
    return TABLE_COMMAND_ALIASES.get(compactCommandId(commandId)) || '';
}

export function applyTableCommand(model, selection, commandId, payload = null) {
    const command = canonicalTableCommandId(commandId);
    if (!command) {
        return unchanged(model, selection, command);
    }

    const working = clone(model || {});
    ensureBodyBlocks(working);
    if (command === 'insertTable') {
        const result = insertTable(working, selection, payload);
        if (!result.changed) {
            return unchanged(working, selection, command);
        }

        working.version = Number(working.version || 0) + 1;
        synchronizeSectionsWithBody(working);
        return {
            changed: true,
            model: working,
            selection: result.selection || selection,
            operation: command,
            dirtyBlockIds: result.dirtyBlockIds || [],
            insertedBlockIds: result.insertedBlockIds || [],
            removedBlockIds: [],
            table: result.table || null,
        };
    }

    if (command === 'deleteTable' || command === 'toggleHeaderRow') {
        const result = command === 'deleteTable'
            ? deleteTable(working, selection, payload)
            : toggleHeaderRow(working, selection, payload);
        if (!result.changed) {
            return unchanged(working, selection, command);
        }

        working.version = Number(working.version || 0) + 1;
        synchronizeSectionsWithBody(working);
        return {
            changed: true,
            model: working,
            selection: result.selection || selection,
            operation: command,
            dirtyBlockIds: result.dirtyBlockIds || [],
            insertedBlockIds: result.insertedBlockIds || [],
            removedBlockIds: result.removedBlockIds || [],
            table: result.table || null,
        };
    }

    if (command === 'convertTextToTable') {
        const result = convertTextToTable(working, selection, payload);
        if (!result.changed) {
            return unchanged(working, selection, command);
        }

        working.version = Number(working.version || 0) + 1;
        synchronizeSectionsWithBody(working);
        return {
            changed: true,
            model: working,
            selection: result.selection || selection,
            operation: command,
            dirtyBlockIds: result.dirtyBlockIds || [],
            insertedBlockIds: result.insertedBlockIds || [],
            removedBlockIds: result.removedBlockIds || [],
            table: result.table || null,
        };
    }

    const selectedCell = findSelectedCell(working, selection, payload);
    if (!selectedCell) {
        return unchanged(working, selection, command);
    }

    let result;
    if (command === 'insertRowBefore' || command === 'insertRowAfter') {
        result = insertRow(selectedCell, command === 'insertRowBefore' ? selectedCell.rowIndex : selectedCell.rowIndex + 1);
    } else if (command === 'insertColumnBefore' || command === 'insertColumnAfter') {
        result = insertColumn(selectedCell, command === 'insertColumnBefore' ? selectedCell.cellIndex : selectedCell.cellIndex + 1);
    } else if (command === 'deleteRow') {
        result = deleteRow(selectedCell);
    } else if (command === 'deleteColumn') {
        result = deleteColumn(selectedCell);
    } else if (command === 'mergeCells') {
        result = mergeCells(working, selection, selectedCell);
    } else if (command === 'splitCell') {
        result = splitCell(selectedCell);
    } else if (command === 'resizeColumn') {
        result = resizeColumn(selectedCell, payload);
    } else if (command === 'setCellFormat') {
        result = setCellFormat(selectedCell, payload, working, selection);
    } else if (command === 'navigateCell') {
        result = navigateCell(selectedCell, payload);
    } else if (command === 'sortTable') {
        result = sortTable(selectedCell, payload);
    } else if (command === 'setTableFormula') {
        result = setTableFormula(selectedCell, payload);
    } else if (command === 'setCellMargins') {
        result = setCellMargins(selectedCell, payload, working, selection);
    } else if (command === 'setCellBorders') {
        result = setCellBorders(selectedCell, payload, working, selection);
    } else if (command === 'convertTableToText') {
        result = convertTableToText(working, selectedCell, payload);
    } else {
        result = { changed: false };
    }

    if (!result.changed && !result.selection) {
        return unchanged(working, selection, command);
    }

    if (result.changed) {
        working.version = Number(working.version || 0) + 1;
        synchronizeSectionsWithBody(working);
    }

    return {
        changed: result.changed === true,
        model: working,
        selection: result.selection || selection,
        operation: command,
        dirtyBlockIds: [selectedCell.tableBlock.id],
        insertedBlockIds: result.insertedBlockIds || [],
        removedBlockIds: result.removedBlockIds || [],
        table: {
            tableId: selectedCell.tableBlock.id,
            rowCount: selectedCell.rows.length,
            columnCount: maxColumnCount(selectedCell.rows),
            activeCellId: selectedCell.cell.id || '',
        },
    };
}

export function queryTableCommandState(model, selection) {
    const cell = findTableCellByBlockId(model, selection?.focus?.blockId || selection?.anchor?.blockId);
    const enabled = !!cell;
    return {
        table: enabled
            ? {
                tableId: cell.tableBlock.id || '',
                cellId: cell.cell.id || '',
                rowIndex: cell.rowIndex,
                cellIndex: cell.cellIndex,
                rowCount: cell.rows.length,
                columnCount: maxColumnCount(cell.rows),
                backgroundColor: cell.cell.backgroundColor || '',
                verticalAlignment: verticalAlignmentName(cell.cell.verticalAlignment),
            }
            : null,
        commands: {
            // insertTable creates a NEW table wherever the caret is — it never needs an existing cell.
            inserttable: commandState(true),
            deletetable: commandState(enabled),
            toggletableheaderrow: commandState(enabled, enabled && cell.tableBlock.content?.table?.layout?.headerRow === true),
            addtablerow: commandState(enabled),
            addtablecolumn: commandState(enabled),
            insertrowbefore: commandState(enabled),
            insertrowafter: commandState(enabled),
            insertcolumnbefore: commandState(enabled),
            insertcolumnafter: commandState(enabled),
            deletetablerow: commandState(enabled && cell.rows.length > 1),
            deletetablecolumn: commandState(enabled && maxColumnCount(cell.rows) > 1),
            mergecells: commandState(enabled),
            splitcell: commandState(enabled && ((Number(cell.cell.columnSpan || 1) || 1) > 1 || (Number(cell.cell.rowSpan || 1) || 1) > 1)),
            setcellformat: commandState(enabled),
            sorttable: commandState(enabled),
            settableformula: commandState(enabled),
            setcellmargins: commandState(enabled),
            setcellborders: commandState(enabled),
            converttabletotext: commandState(enabled),
        },
    };
}

function insertRow(selectedCell, rowIndex, selectionCellIndex = selectedCell.cellIndex) {
    const columnCount = maxColumnCount(selectedCell.rows);
    const existingIds = collectTableIds(selectedCell.tableBlock);
    const row = {
        id: uniqueId(`${selectedCell.tableBlock.id}-row-${rowIndex + 1}`, selectedCell.rows.map(item => item.id)),
        cells: Array.from({ length: columnCount }, (_, columnIndex) => createCell(selectedCell.tableBlock.id, rowIndex, columnIndex, existingIds)),
    };
    selectedCell.rows.splice(Math.max(0, Math.min(selectedCell.rows.length, rowIndex)), 0, row);
    normalizeRowCellIds(selectedCell.tableBlock);
    return {
        changed: true,
        selection: collapsedSelection(firstEditablePositionInCell(row.cells[Math.min(Math.max(0, selectionCellIndex), row.cells.length - 1)])),
        insertedBlockIds: row.cells.flatMap(cell => (cell.blocks || []).map(block => block.id)),
    };
}

function insertColumn(selectedCell, columnIndex) {
    const targetIndex = Math.max(0, Math.min(maxColumnCount(selectedCell.rows), columnIndex));
    const insertedBlockIds = [];
    const existingIds = collectTableIds(selectedCell.tableBlock);
    selectedCell.rows.forEach((row, rowIndex) => {
        const cell = createCell(selectedCell.tableBlock.id, rowIndex, targetIndex, existingIds);
        row.cells.splice(Math.max(0, Math.min(row.cells.length, targetIndex)), 0, cell);
        insertedBlockIds.push(...cell.blocks.map(block => block.id));
    });
    normalizeRowCellIds(selectedCell.tableBlock);
    const targetCell = selectedCell.rows[selectedCell.rowIndex]?.cells[targetIndex] || selectedCell.cell;
    return {
        changed: true,
        selection: collapsedSelection(firstEditablePositionInCell(targetCell)),
        insertedBlockIds,
    };
}

function deleteRow(selectedCell) {
    if (selectedCell.rows.length <= 1) {
        return { changed: false };
    }

    const removed = selectedCell.rows.splice(selectedCell.rowIndex, 1)[0];
    const targetRow = selectedCell.rows[Math.min(selectedCell.rowIndex, selectedCell.rows.length - 1)];
    const targetCell = targetRow.cells[Math.min(selectedCell.cellIndex, targetRow.cells.length - 1)];
    normalizeRowCellIds(selectedCell.tableBlock);
    return {
        changed: true,
        selection: collapsedSelection(firstEditablePositionInCell(targetCell)),
        removedBlockIds: (removed?.cells || []).flatMap(cell => (cell.blocks || []).map(block => block.id)),
    };
}

function deleteColumn(selectedCell) {
    const columnCount = maxColumnCount(selectedCell.rows);
    if (columnCount <= 1) {
        return { changed: false };
    }

    const removedBlockIds = [];
    selectedCell.rows.forEach(row => {
        const removed = row.cells.splice(Math.min(selectedCell.cellIndex, row.cells.length - 1), 1)[0];
        removedBlockIds.push(...((removed?.blocks || []).map(block => block.id)));
    });
    normalizeRowCellIds(selectedCell.tableBlock);
    const targetCell = selectedCell.rows[selectedCell.rowIndex]?.cells[Math.min(selectedCell.cellIndex, columnCount - 2)] || selectedCell.rows[0]?.cells[0];
    return {
        changed: true,
        selection: collapsedSelection(firstEditablePositionInCell(targetCell)),
        removedBlockIds,
    };
}

function mergeCells(model, selection, selectedCell) {
    const range = cellRangeFromSelection(model, selection);
    if (!range || range.table.tableBlock.id !== selectedCell.tableBlock.id) {
        return { changed: false };
    }

    const origin = selectedCell.rows[range.startRow]?.cells[range.startCell];
    if (!origin) {
        return { changed: false };
    }

    const rowSpan = range.endRow - range.startRow + 1;
    const columnSpan = range.endCell - range.startCell + 1;
    if (rowSpan === 1 && columnSpan === 1) {
        return { changed: false };
    }

    origin.rowSpan = rowSpan;
    origin.columnSpan = columnSpan;
    origin.merge = { isOrigin: true, originCellId: null };
    for (let rowIndex = range.startRow; rowIndex <= range.endRow; rowIndex += 1) {
        for (let cellIndex = range.startCell; cellIndex <= range.endCell; cellIndex += 1) {
            const cell = selectedCell.rows[rowIndex]?.cells[cellIndex];
            if (!cell || cell === origin) {
                continue;
            }

            cell.merge = { isOrigin: false, originCellId: origin.id };
            cell.rowSpan = 1;
            cell.columnSpan = 1;
        }
    }

    return {
        changed: true,
        selection: collapsedSelection(firstEditablePositionInCell(origin)),
    };
}

function splitCell(selectedCell) {
    const originId = selectedCell.cell.merge?.originCellId || selectedCell.cell.id;
    let changed = false;
    for (const row of selectedCell.rows) {
        for (const cell of row.cells) {
            if (cell.id === originId || cell.merge?.originCellId === originId) {
                cell.rowSpan = 1;
                cell.columnSpan = 1;
                cell.merge = { isOrigin: true, originCellId: null };
                changed = true;
            }
        }
    }

    return {
        changed,
        selection: collapsedSelection(firstEditablePositionInCell(selectedCell.cell)),
    };
}

function resizeColumn(selectedCell, payload) {
    const width = Math.max(32, Math.min(720, Number(payload?.width ?? payload?.Width ?? payload?.value ?? 0) || 0));
    if (!width) {
        return { changed: false };
    }

    let changed = false;
    for (const row of selectedCell.rows) {
        const cell = row.cells[selectedCell.cellIndex];
        if (cell && Number(cell.width || 0) !== width) {
            cell.width = width;
            changed = true;
        }
    }

    return { changed, selection: collapsedSelection(firstEditablePositionInCell(selectedCell.cell)) };
}

function setCellFormat(selectedCell, payload, model, selection) {
    const nextBackground = payload?.backgroundColor ?? payload?.BackgroundColor ?? payload?.background ?? payload?.fill;
    const nextVertical = payload?.verticalAlignment ?? payload?.VerticalAlignment ?? payload?.vAlign;
    const nextAlignment = payload?.alignment ?? payload?.Alignment;
    let changed = false;
    const targetCells = selectedCells(selectedCell, model, selection);

    for (const cell of targetCells) {
        if (typeof nextBackground === 'string' && nextBackground && cell.backgroundColor !== nextBackground) {
            cell.backgroundColor = nextBackground;
            changed = true;
        }

        if (nextVertical != null) {
            const vertical = verticalAlignmentName(nextVertical);
            const verticalValue = verticalAlignmentValue(vertical);
            if (verticalAlignmentValue(cell.verticalAlignment) !== verticalValue) {
                cell.verticalAlignment = verticalValue;
                changed = true;
            }
        }

        if (nextAlignment != null) {
            const alignment = alignmentValue(nextAlignment);
            for (const block of cell.blocks || []) {
                block.paragraphProperties = block.paragraphProperties && typeof block.paragraphProperties === 'object' ? block.paragraphProperties : {};
                if (block.paragraphProperties.alignment !== alignment) {
                    block.paragraphProperties.alignment = alignment;
                    changed = true;
                }
            }
        }
    }

    return { changed, selection: collapsedSelection(firstEditablePositionInCell(selectedCell.cell)) };
}

function navigateCell(selectedCell, payload) {
    const rawDirection = payload?.direction ?? payload?.Direction ?? '';
    const direction = String(rawDirection).toLowerCase() === 'previous' || String(rawDirection).toLowerCase() === 'backward' || payload?.shift === true || payload?.Shift === true ? -1 : 1;
    const cells = selectedCell.rows.flatMap(row => row.cells.filter(cell => cell.merge?.isOrigin !== false));
    const currentIndex = cells.findIndex(cell => cell.id === selectedCell.cell.id);
    if (currentIndex < 0) {
        return { changed: false };
    }

    let nextIndex = currentIndex + direction;
    if (nextIndex < 0) {
        nextIndex = cells.length - 1;
    } else if (nextIndex >= cells.length) {
        return insertRow(selectedCell, selectedCell.rows.length, 0);
    }

    const target = cells[nextIndex] || selectedCell.cell;
    const position = direction < 0 ? lastEditablePositionInCell(target) : firstEditablePositionInCell(target);
    return { changed: false, selection: collapsedSelection(position) };
}

function sortTable(selectedCell, payload) {
    const layout = selectedCell.table?.layout || selectedCell.table?.Layout || {};
    const totalRow = layout.totalRow === true || layout.TotalRow === true ? selectedCell.rows[selectedCell.rows.length - 1] : null;
    const headerRows = selectedCell.rows.filter(row => row.cells.some(cell => cell.isHeader === true || cell.IsHeader === true));
    const bodyRows = selectedCell.rows.filter(row => !headerRows.includes(row) && row !== totalRow);
    if (bodyRows.length < 2) {
        return { changed: false };
    }

    const columnIndex = Math.max(0, Math.min(maxColumnCount(selectedCell.rows) - 1, Math.trunc(Number(payload?.columnIndex ?? payload?.ColumnIndex ?? selectedCell.cellIndex) || 0)));
    const direction = String(payload?.direction ?? payload?.Direction ?? 'ascending').toLowerCase();
    const multiplier = direction === 'desc' || direction === 'descending' ? -1 : 1;
    bodyRows.sort((left, right) => compareCellText(left.cells[columnIndex], right.cells[columnIndex]) * multiplier);
    selectedCell.tableBlock.content.table.rows = totalRow ? [...headerRows, ...bodyRows, totalRow] : [...headerRows, ...bodyRows];
    normalizeRowCellIds(selectedCell.tableBlock);
    return { changed: true, selection: collapsedSelection(firstEditablePositionInCell(selectedCell.cell)) };
}

function setTableFormula(selectedCell, payload) {
    const formula = String(payload?.formula ?? payload?.Formula ?? 'SUM').trim().toUpperCase();
    const columnIndex = Math.max(0, Math.min(maxColumnCount(selectedCell.rows) - 1, Math.trunc(Number(payload?.columnIndex ?? payload?.ColumnIndex ?? selectedCell.cellIndex) || 0)));
    const values = selectedCell.rows
        .filter((_, index) => index !== selectedCell.rowIndex)
        .map(row => numericCellValue(row.cells[columnIndex]))
        .filter(Number.isFinite);
    if (values.length === 0) {
        return { changed: false };
    }

    const result = formula === 'AVERAGE'
        ? values.reduce((sum, value) => sum + value, 0) / values.length
        : values.reduce((sum, value) => sum + value, 0);
    const text = formatFormulaResult(result);
    setCellText(selectedCell.cell, text);
    selectedCell.cell.formula = formula;
    selectedCell.cell.Formula = formula;
    return { changed: true, selection: collapsedSelection(lastEditablePositionInCell(selectedCell.cell)) };
}

function setCellMargins(selectedCell, payload, model, selection) {
    const padding = Number(payload?.padding ?? payload?.Padding ?? payload?.value ?? payload?.Value);
    const margins = payload?.margins || payload?.Margins || null;
    const targetCells = selectedCells(selectedCell, model, selection);
    let changed = false;
    for (const cell of targetCells) {
        const nextPadding = Number.isFinite(padding)
            ? Math.max(0, Math.min(96, padding))
            : averageMargins(margins);
        if (Number.isFinite(nextPadding) && Number(cell.padding ?? cell.Padding ?? -1) !== nextPadding) {
            cell.padding = nextPadding;
            changed = true;
        }
    }

    return { changed, selection: collapsedSelection(firstEditablePositionInCell(selectedCell.cell)) };
}

function setCellBorders(selectedCell, payload, model, selection) {
    const targetCells = selectedCells(selectedCell, model, selection);
    const borders = payload?.borders || payload?.Borders || payload || {};
    let changed = false;
    for (const cell of targetCells) {
        cell.borders = cell.borders && typeof cell.borders === 'object' ? cell.borders : {};
        for (const side of ['top', 'right', 'bottom', 'left']) {
            const value = borders[side] ?? borders[side[0].toUpperCase() + side.slice(1)];
            if (typeof value === 'string' && value && cell.borders[side] !== value) {
                cell.borders[side] = value;
                changed = true;
            }
        }
    }

    return { changed, selection: collapsedSelection(firstEditablePositionInCell(selectedCell.cell)) };
}

function convertTableToText(model, selectedCell, payload) {
    const delimiter = String(payload?.delimiter ?? payload?.Delimiter ?? '\t');
    const rowDelimiter = String(payload?.rowDelimiter ?? payload?.RowDelimiter ?? '\n');
    const text = selectedCell.rows
        .map(row => row.cells.map(cellText).join(delimiter))
        .join(rowDelimiter);
    const index = model.body.blocks.findIndex(block => block === selectedCell.tableBlock || block.id === selectedCell.tableBlock.id);
    if (index < 0) {
        return { changed: false };
    }

    const blockId = `${selectedCell.tableBlock.id || 'table'}-text`;
    const paragraph = createParagraph(blockId, text);
    paragraph.order = selectedCell.tableBlock.order ?? selectedCell.tableIndex ?? 0;
    model.body.blocks.splice(index, 1, paragraph);
    return {
        changed: true,
        selection: collapsedSelection({ blockId, offset: text.length }),
        dirtyBlockIds: [blockId],
        insertedBlockIds: [blockId],
        removedBlockIds: [selectedCell.tableBlock.id || ''],
    };
}

/** Table entry resolved from an explicit {tableId}, an explicit {cellId}, or the selection. */
function resolveTableEntry(model, selection, payload) {
    const tableId = String(payload?.tableId ?? payload?.TableId ?? '');
    if (tableId) {
        const entry = tableEntries(model).find(item => String(item.tableBlock.id || '') === tableId);
        if (entry) {
            return entry;
        }
    }

    const cell = findSelectedCell(model, selection, payload);
    return cell ? { tableBlock: cell.tableBlock, table: cell.tableBlock.content.table, rows: cell.rows } : null;
}

function deleteTable(model, selection, payload) {
    const entry = resolveTableEntry(model, selection, payload);
    if (!entry) {
        return { changed: false };
    }

    const blocks = model.body.blocks;
    const index = blocks.findIndex(block => String(block?.id || '') === String(entry.tableBlock.id || ''));
    if (index < 0) {
        return { changed: false };
    }

    const removedBlockIds = collectAllBlockIds([blocks[index]]);
    blocks.splice(index, 1);

    // Caret Word-style: the block that followed the table, else the previous one; an orphaned
    // body gets a fresh empty paragraph so the document never ends up caret-less.
    let caretTarget = blocks[index] || blocks[index - 1] || null;
    if (!caretTarget) {
        const paragraph = createParagraph(uniqueId('empty-body-paragraph', collectAllBlockIds(blocks)), '');
        paragraph.sectionId = entry.tableBlock.sectionId || model.sections?.[0]?.id || '';
        paragraph.order = Number(entry.tableBlock.order || 10) || 10;
        blocks.push(paragraph);
        caretTarget = paragraph;
    }

    return {
        changed: true,
        selection: collapsedSelection(firstEditablePositionInBlock(caretTarget)),
        dirtyBlockIds: [String(caretTarget.id || '')].filter(Boolean),
        removedBlockIds,
        table: null,
    };
}

function toggleHeaderRow(model, selection, payload) {
    // Pure layout-flag toggle: resolveTableCellStyle styles row 0 and tableRepeatsHeaderRows
    // repeats it across page breaks from layout.headerRow. Cell-level isHeader/backgroundColor
    // overrides intentionally win, and a double toggle is exactly the identity.
    const entry = resolveTableEntry(model, selection, payload);
    if (!entry?.table) {
        return { changed: false };
    }

    entry.table.layout = entry.table.layout || {};
    entry.table.layout.headerRow = entry.table.layout.headerRow !== true;
    return {
        changed: true,
        selection: null,
        dirtyBlockIds: [String(entry.tableBlock.id || '')],
        table: {
            tableId: entry.tableBlock.id || '',
            rowCount: entry.rows.length,
            columnCount: maxColumnCount(entry.rows),
            activeCellId: '',
        },
    };
}

function firstEditablePositionInBlock(block) {
    const firstCell = block?.content?.table?.rows?.[0]?.cells?.[0];
    if (firstCell) {
        return firstEditablePositionInCell(firstCell);
    }

    return { blockId: String(block?.id || ''), offset: 0 };
}

// Cap mirrors the toolbar grid picker (TmDocumentTableGridPicker MaxRows/MaxCols = 10×10);
// the picker is the only UI producing this payload, so anything larger is a malformed call.
const INSERT_TABLE_MAX_DIMENSION = 10;

function insertTable(model, selection, payload) {
    const rows = clampTableDimension(payload?.rows ?? payload?.Rows, 2);
    const columns = clampTableDimension(payload?.columns ?? payload?.Columns, 2);
    const appendToBodyEnd = payload?.appendToBodyEnd === true || payload?.AppendToBodyEnd === true;

    const blocks = model.body.blocks;
    const caretBlockId = String(selection?.focus?.blockId || selection?.anchor?.blockId || '');
    // The caret may sit inside a table cell paragraph — the new table always lands at the top
    // level, after the body block that CONTAINS the caret (Word behaviour, never nested).
    let anchorIndex = blocks.length - 1;
    if (!appendToBodyEnd) {
        const containing = topLevelBlockIndexContaining(blocks, caretBlockId);
        anchorIndex = containing >= 0 ? containing : blocks.length - 1;
    }

    const anchor = blocks[anchorIndex] || null;
    const insertIndex = Math.min(blocks.length, anchorIndex + 1);
    const next = blocks[insertIndex] || null;
    const order = next
        ? (Number(anchor?.order || 0) + Number(next.order || 0)) / 2
        : Number(anchor?.order || 0) + 10;

    const existingIds = collectAllBlockIds(blocks);
    const tableId = uniqueId('inserted-table', existingIds);
    existingIds.push(tableId);
    const columnWidth = defaultInsertedColumnWidth(model, columns);
    const tableRows = Array.from({ length: rows }, (_, rowIndex) => ({
        id: `${tableId}-row-${rowIndex + 1}`,
        cells: Array.from({ length: columns }, (_, columnIndex) =>
            createInsertedCell(tableId, rowIndex, columnIndex, columnWidth, existingIds)),
    }));

    const tableBlock = {
        id: tableId,
        sectionId: anchor?.sectionId || model.sections?.[0]?.id || '',
        type: 'table',
        order,
        paragraphProperties: {},
        content: {
            type: 'table',
            table: {
                layout: { cellPadding: 8 },
                rows: tableRows,
            },
        },
        preserve: {},
    };

    blocks.splice(insertIndex, 0, tableBlock);
    return {
        changed: true,
        selection: collapsedSelection(firstEditablePositionInCell(tableRows[0].cells[0])),
        dirtyBlockIds: [tableBlock.id],
        insertedBlockIds: tableRows.flatMap(row => row.cells.flatMap(cell => cell.blocks.map(block => block.id))),
        table: {
            tableId,
            rowCount: rows,
            columnCount: columns,
            activeCellId: tableRows[0].cells[0].id,
        },
    };
}

function clampTableDimension(value, fallback) {
    const parsed = Number(value);
    const base = Number.isFinite(parsed) ? Math.trunc(parsed) : fallback;
    return Math.max(1, Math.min(INSERT_TABLE_MAX_DIMENSION, base));
}

/** Even split of the printable content width (page width minus horizontal margins). */
function defaultInsertedColumnWidth(model, columns) {
    const settings = model?.pageSettings || {};
    const pageWidth = Number(settings.width) > 0 ? Number(settings.width) : 794;
    const marginLeft = Number.isFinite(Number(settings.marginLeft)) ? Number(settings.marginLeft) : 72;
    const marginRight = Number.isFinite(Number(settings.marginRight)) ? Number(settings.marginRight) : 72;
    const contentWidth = Math.max(120, pageWidth - marginLeft - marginRight);
    return Math.floor(contentWidth / Math.max(1, columns));
}

/** Index of the top-level body block that is or (recursively, via table cells) contains blockId. */
function topLevelBlockIndexContaining(blocks, blockId) {
    if (!blockId) {
        return -1;
    }

    const containsBlock = block => {
        if (String(block?.id || '') === blockId) {
            return true;
        }

        const rows = block?.content?.table?.rows;
        return Array.isArray(rows) && rows.some(row =>
            (row?.cells || []).some(cell => (cell?.blocks || []).some(containsBlock)));
    };
    return blocks.findIndex(containsBlock);
}

function collectAllBlockIds(blocks) {
    const ids = [];
    const visit = block => {
        if (block?.id) {
            ids.push(String(block.id));
        }

        for (const row of block?.content?.table?.rows || []) {
            for (const cell of row?.cells || []) {
                if (cell?.id) {
                    ids.push(String(cell.id));
                }

                for (const nested of cell?.blocks || []) {
                    visit(nested);
                }
            }
        }
    };
    for (const block of blocks) {
        visit(block);
    }

    return ids;
}

/** Plain (non-header) cell for a freshly inserted table — Word inserts unstyled grids. */
function createInsertedCell(tableId, rowIndex, columnIndex, width, existingIds) {
    const id = uniqueId(`${tableId}-r${rowIndex + 1}c${columnIndex + 1}`, existingIds);
    existingIds.push(id, `${id}-p`, `${id}-p-run`);
    return {
        id,
        columnSpan: 1,
        rowSpan: 1,
        isHeader: false,
        merge: { isOrigin: true, originCellId: null },
        width,
        backgroundColor: null,
        borders: {},
        verticalAlignment: 0,
        padding: 8,
        blocks: [createParagraph(`${id}-p`, '')],
        preserve: {},
    };
}

function convertTextToTable(model, selection, payload) {
    const blockId = selection?.focus?.blockId || selection?.anchor?.blockId || '';
    const blockIndex = model.body.blocks.findIndex(block => String(block?.id || '') === String(blockId));
    const block = model.body.blocks[blockIndex];
    if (!block || String(block.type || block.content?.type || '').toLowerCase() === 'table') {
        return { changed: false };
    }

    const text = blockText(block);
    if (!text.trim()) {
        return { changed: false };
    }

    const delimiter = String(payload?.delimiter ?? payload?.Delimiter ?? (text.includes('\t') ? '\t' : ','));
    const rows = text.split(/\r?\n/).filter(row => row.length > 0).map((rowText, rowIndex) => ({
        id: `${block.id}-table-row-${rowIndex + 1}`,
        cells: rowText.split(delimiter).map((value, cellIndex) => createCellFromText(`${block.id}-table-r${rowIndex + 1}c${cellIndex + 1}`, value.trim(), rowIndex === 0)),
    }));
    if (rows.length === 0 || rows[0].cells.length === 0) {
        return { changed: false };
    }

    const tableBlock = {
        id: `${block.id}-table`,
        type: 'table',
        order: block.order ?? blockIndex,
        paragraphProperties: {},
        content: {
            type: 'table',
            table: {
                layout: { cellPadding: 8, repeatHeaderRows: true, headerRow: true, bandedRows: true },
                rows,
            },
        },
        preserve: {},
    };
    model.body.blocks.splice(blockIndex, 1, tableBlock);
    return {
        changed: true,
        selection: collapsedSelection(firstEditablePositionInCell(rows[0].cells[0])),
        dirtyBlockIds: [tableBlock.id],
        insertedBlockIds: rows.flatMap(row => row.cells.flatMap(cell => (cell.blocks || []).map(item => item.id))),
        removedBlockIds: [block.id],
        table: { tableId: tableBlock.id, rowCount: rows.length, columnCount: rows[0].cells.length, activeCellId: rows[0].cells[0].id },
    };
}

function findSelectedCell(model, selection, payload) {
    if (payload?.cellId || payload?.CellId) {
        const cellId = String(payload.cellId || payload.CellId);
        for (const entry of tableEntries(model)) {
            for (let rowIndex = 0; rowIndex < entry.rows.length; rowIndex += 1) {
                const row = entry.rows[rowIndex];
                const cellIndex = row.cells.findIndex(cell => String(cell?.id || '') === cellId);
                if (cellIndex >= 0) {
                    return { ...entry, row, cell: row.cells[cellIndex], rowIndex, cellIndex };
                }
            }
        }
    }

    return findTableCellByBlockId(model, selection?.focus?.blockId || selection?.anchor?.blockId);
}

function selectedCells(selectedCell, model, selection) {
    const range = cellRangeFromSelection(model, selection);
    if (!range || range.table.tableBlock.id !== selectedCell.tableBlock.id) {
        return [selectedCell.cell];
    }

    const cells = [];
    for (let rowIndex = range.startRow; rowIndex <= range.endRow; rowIndex += 1) {
        const row = selectedCell.rows[rowIndex];
        for (let cellIndex = range.startCell; cellIndex <= range.endCell; cellIndex += 1) {
            const cell = row?.cells?.[cellIndex];
            if (cell) {
                cells.push(cell);
            }
        }
    }

    return cells.length > 0 ? cells : [selectedCell.cell];
}

function normalizeRowCellIds(tableBlock) {
    const rows = tableBlock?.content?.table?.rows || [];
    rows.forEach((row, rowIndex) => {
        row.id = row.id || `${tableBlock.id}-row-${rowIndex + 1}`;
        row.cells = Array.isArray(row.cells) ? row.cells : [];
        row.cells.forEach((cell, cellIndex) => {
            cell.id = cell.id || `${tableBlock.id}-r${rowIndex + 1}c${cellIndex + 1}`;
            cell.blocks = Array.isArray(cell.blocks) && cell.blocks.length > 0 ? cell.blocks : [createParagraph(`${cell.id}-p`, '')];
        });
    });
}

function createCell(tableId, rowIndex, cellIndex, existingIds = []) {
    const id = uniqueId(`${tableId}-r${rowIndex + 1}c${cellIndex + 1}`, existingIds);
    existingIds.push(id, `${id}-p`, `${id}-p-run`);
    return {
        id,
        columnSpan: 1,
        rowSpan: 1,
        isHeader: rowIndex === 0,
        merge: { isOrigin: true, originCellId: null },
        width: null,
        backgroundColor: rowIndex === 0 ? 'rgba(226, 232, 240, 0.84)' : null,
        borders: {},
        verticalAlignment: 0,
        padding: 8,
        blocks: [createParagraph(`${id}-p`, '')],
        preserve: {},
    };
}

function createParagraph(id, text) {
    return {
        id,
        type: 'paragraph',
        order: 1,
        paragraphProperties: { alignment: 0, lineSpacing: 1.1 },
        content: {
            type: 'paragraph',
            runs: [{ id: `${id}-run`, type: 'text', text: String(text || ''), marks: [] }],
        },
        preserve: {},
    };
}

function createCellFromText(id, text, isHeader) {
    const cell = createCell(id.replace(/-r\d+c\d+$/i, ''), 0, 0, [id]);
    cell.id = id;
    cell.isHeader = isHeader === true;
    cell.backgroundColor = cell.isHeader ? 'rgba(226, 232, 240, 0.84)' : null;
    cell.blocks = [createParagraph(`${id}-p`, text)];
    return cell;
}

function blockText(block) {
    return (block?.content?.runs || block?.Content?.Runs || [])
        .map(run => String(run?.text ?? run?.Text ?? ''))
        .join('');
}

function cellText(cell) {
    return (cell?.blocks || cell?.Blocks || [])
        .map(blockText)
        .join('\n');
}

function setCellText(cell, text) {
    cell.blocks = Array.isArray(cell.blocks) && cell.blocks.length > 0 ? cell.blocks : [createParagraph(`${cell.id || 'cell'}-p`, '')];
    const block = cell.blocks[0];
    block.content = block.content && typeof block.content === 'object' ? block.content : { type: 'paragraph', runs: [] };
    block.content.runs = Array.isArray(block.content.runs) && block.content.runs.length > 0
        ? block.content.runs
        : [{ id: `${block.id || cell.id || 'cell'}-run`, type: 'text', text: '', marks: [] }];
    block.content.runs[0].text = text;
}

function compareCellText(left, right) {
    const leftText = cellText(left);
    const rightText = cellText(right);
    const leftNumber = Number(leftText.replace(/\s/g, '').replace(',', '.'));
    const rightNumber = Number(rightText.replace(/\s/g, '').replace(',', '.'));
    if (Number.isFinite(leftNumber) && Number.isFinite(rightNumber)) {
        return leftNumber - rightNumber;
    }

    return leftText.localeCompare(rightText, undefined, { numeric: true, sensitivity: 'base' });
}

function numericCellValue(cell) {
    const text = cellText(cell).replace(/\s/g, '').replace(',', '.');
    const value = Number(text);
    return Number.isFinite(value) ? value : Number.NaN;
}

function formatFormulaResult(value) {
    return Number.isInteger(value) ? String(value) : String(Math.round(value * 100) / 100);
}

function averageMargins(margins) {
    if (!margins || typeof margins !== 'object') {
        return Number.NaN;
    }

    const values = ['top', 'right', 'bottom', 'left']
        .map(side => Number(margins[side] ?? margins[side[0].toUpperCase() + side.slice(1)]))
        .filter(Number.isFinite);
    return values.length > 0 ? values.reduce((sum, value) => sum + value, 0) / values.length : Number.NaN;
}

function commandState(enabled) {
    return {
        disabled: !enabled,
        active: false,
        mixed: false,
        value: null,
        state: enabled ? 'inactive' : 'disabled',
    };
}

function maxColumnCount(rows) {
    return rows.reduce((max, row) => Math.max(max, Array.isArray(row?.cells) ? row.cells.length : 0), 0);
}

function tableEntries(model) {
    const blocks = Array.isArray(model?.body?.blocks) ? model.body.blocks : [];
    return blocks
        .map((block, tableIndex) => ({ tableBlock: block, tableIndex }))
        .filter(entry => String(entry.tableBlock?.type || entry.tableBlock?.content?.type || '').toLowerCase() === 'table'
            && Array.isArray(entry.tableBlock?.content?.table?.rows))
        .map(entry => ({
            ...entry,
            table: entry.tableBlock.content.table,
            rows: entry.tableBlock.content.table.rows,
        }));
}

function collapsedSelection(position) {
    const safe = position || { blockId: '', offset: 0 };
    return { anchor: { ...safe }, focus: { ...safe } };
}

function alignmentValue(value) {
    if (typeof value === 'number') {
        return Math.max(0, Math.min(3, Math.trunc(value)));
    }

    const normalized = String(value || '').toLowerCase();
    if (normalized === 'center' || normalized === 'middle') return 1;
    if (normalized === 'right' || normalized === 'end') return 2;
    if (normalized === 'justify') return 3;
    return 0;
}

function verticalAlignmentName(value) {
    if (typeof value === 'number') {
        return ['top', 'middle', 'bottom'][Math.max(0, Math.min(2, Math.trunc(value)))] || 'top';
    }

    const normalized = String(value || '').toLowerCase();
    if (normalized === '1' || normalized === 'center') return 'middle';
    if (normalized === '2' || normalized === 'end') return 'bottom';
    return normalized === 'middle' || normalized === 'bottom' ? normalized : 'top';
}

function verticalAlignmentValue(value) {
    if (typeof value === 'number') {
        return Math.max(0, Math.min(2, Math.trunc(value)));
    }

    const normalized = String(value || '').toLowerCase();
    if (normalized === '1' || normalized === 'middle' || normalized === 'center') return 1;
    if (normalized === '2' || normalized === 'bottom' || normalized === 'end') return 2;
    return 0;
}

function uniqueId(base, existingValues) {
    const existing = new Set((existingValues || []).map(String));
    if (!existing.has(base)) {
        return base;
    }

    let index = 2;
    while (existing.has(`${base}-${index}`)) {
        index += 1;
    }

    return `${base}-${index}`;
}

function collectTableIds(tableBlock) {
    const ids = [tableBlock?.id || ''];
    for (const row of tableBlock?.content?.table?.rows || []) {
        ids.push(row?.id || '');
        for (const cell of row?.cells || []) {
            ids.push(cell?.id || '');
            for (const block of cell?.blocks || []) {
                ids.push(block?.id || '');
                for (const run of block?.content?.runs || []) {
                    ids.push(run?.id || '');
                }
            }
        }
    }

    return ids.filter(Boolean).map(String);
}

function unchanged(model, selection, operation) {
    return {
        changed: false,
        model,
        selection,
        operation,
        dirtyBlockIds: [],
    };
}

function ensureBodyBlocks(model) {
    if (!model.body || typeof model.body !== 'object') {
        model.body = { blocks: [] };
    }

    if (!Array.isArray(model.body.blocks)) {
        model.body.blocks = [];
    }
}

function synchronizeSectionsWithBody(model) {
    if (!Array.isArray(model.sections) || model.sections.length === 0) {
        return;
    }

    const blocks = model.body.blocks;
    for (const section of model.sections) {
        const sectionId = String(section?.id || '');
        const matching = blocks.filter(block => String(block.sectionId || '') === sectionId);
        section.blocks = matching.length > 0 ? matching : section.blocks;
    }
}

function compactCommandId(commandId) {
    return String(commandId == null ? '' : commandId).replace(/[\s_-]/g, '').toLowerCase();
}

function clone(value) {
    if (typeof structuredClone === 'function') {
        return structuredClone(value);
    }

    return JSON.parse(JSON.stringify(value ?? null));
}
