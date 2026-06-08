import { createTablePaginationState, ensureTableRowPage, tableHeaderRows } from './table-pagination.mjs';
import { resolveTableCellStyle, tableCellPadding, tableCellSpacing, tableRepeatsHeaderRows } from './table-styles.mjs';

const DEFAULT_CELL_PADDING = 8;
const DEFAULT_ROW_HEIGHT = 34;
const DEFAULT_BORDER_COLOR = '#94a3b8';

export function layoutCanvasTable(context = {}) {
    const block = context.block || {};
    const table = block?.content?.table || {};
    const rows = normalizeRows(table.rows || []);
    const page = context.page || { body: { x: 0, width: 600 } };
    const body = page.body || {};
    const tableWidth = resolveTableWidth(table.layout, body.width);
    const tableX = resolveTableX(table.layout, body.x, body.width, tableWidth);
    const tableY = Number(context.y || 0) || 0;
    const columnCount = Math.max(1, maxColumnCount(rows));
    const columnWidths = resolveColumnWidths(rows, columnCount, tableWidth);
    const cellSpacing = tableCellSpacing(table);
    const measuredRows = [];
    const cellLayouts = [];
    const nestedBlocks = [];
    let cursorY = tableY;

    for (let rowIndex = 0; rowIndex < rows.length; rowIndex += 1) {
        const row = rows[rowIndex];
        const measuredCells = [];
        let cursorColumn = 0;
        let rowHeight = DEFAULT_ROW_HEIGHT;

        for (let cellIndex = 0; cellIndex < row.cells.length; cellIndex += 1) {
            const cell = row.cells[cellIndex];
            if (cell.merge?.isOrigin === false || cell.merge?.IsOrigin === false) {
                continue;
            }

            const columnIndex = Math.min(columnCount - 1, cursorColumn);
            const columnSpan = Math.max(1, Number(cell.columnSpan ?? cell.ColumnSpan ?? 1) || 1);
            const width = sumColumns(columnWidths, columnIndex, columnSpan);
            const padding = tableCellPadding(table, cell, DEFAULT_CELL_PADDING);
            const contentWidth = Math.max(1, width - padding * 2);
            const measured = measureCellContent({
                ...context,
                tableId: block.id || '',
                rowId: row.id || '',
                cell,
                rowIndex,
                cellIndex,
                columnIndex,
                columnSpan,
                x: tableX + sumColumns(columnWidths, 0, columnIndex),
                y: cursorY,
                width,
                padding,
                contentWidth,
            });
            rowHeight = Math.max(rowHeight, measured.contentHeight + padding * 2);
            measuredCells.push(measured);
            cursorColumn += columnSpan;
        }

        measuredRows.push({ row, rowIndex, measuredCells, height: rowHeight });
        cursorY += rowHeight;
    }

    const pagination = createTablePaginationState({ page, y: tableY });
    const repeatedHeaders = tableRepeatsHeaderRows(table) ? tableHeaderRows(measuredRows, table) : [];
    const firstPageIndex = Number(page.index || 0) || 0;
    for (const rowLayout of measuredRows) {
        const moved = ensureTableRowPage(pagination, rowLayout.height, context, {
            repeatHeader: repeatedHeaders.length > 0 && rowLayout.rowIndex > 0,
        });
        if (moved.repeatedHeader) {
            for (const headerRow of repeatedHeaders) {
                placeMeasuredRow(headerRow, pagination.cursorY, pagination.page, true);
                pagination.cursorY += headerRow.height + cellSpacing;
            }
        }

        placeMeasuredRow(rowLayout, pagination.cursorY, pagination.page, false);
        pagination.cursorY += rowLayout.height + cellSpacing;
    }

    function placeMeasuredRow(rowLayout, rowY, rowPage, repeatedHeader) {
        for (const measured of rowLayout.measuredCells) {
            const rect = {
                x: measured.x,
                y: rowY,
                width: measured.width,
                height: rowLayout.height,
            };
            const contentHeight = Math.max(1, measured.contentHeight);
            const offsetY = verticalContentOffset(measured.cell, rowLayout.height, contentHeight, measured.padding);
            const contentRect = {
                x: rect.x + measured.padding,
                y: rect.y + measured.padding + offsetY,
                width: Math.max(1, rect.width - measured.padding * 2),
                height: Math.max(1, rect.height - measured.padding * 2),
            };
            const rowPageIndex = Number(rowPage?.index || 0) || 0;
            const shiftedBlocks = shiftMeasuredBlocks(measured.blocks, contentRect.x, contentRect.y, rowPageIndex);
            nestedBlocks.push(...shiftedBlocks);
            const style = resolveTableCellStyle(table, measured.cell, rowLayout.rowIndex, measured.columnIndex, rows.length);
            cellLayouts.push({
                id: `${block.id || 'table'}-${measured.cell.id || `${rowLayout.rowIndex}-${measured.cellIndex}`}`,
                tableId: block.id || '',
                rowId: rowLayout.row.id || '',
                cellId: measured.cell.id || '',
                rowIndex: rowLayout.rowIndex,
                cellIndex: measured.cellIndex,
                columnIndex: measured.columnIndex,
                columnSpan: measured.columnSpan,
                rowSpan: Math.max(1, Number(measured.cell.rowSpan ?? measured.cell.RowSpan ?? 1) || 1),
                pageIndex: rowPageIndex,
                rect,
                contentRect,
                backgroundColor: style.backgroundColor,
                borderColor: style.borderColor,
                isHeader: style.isHeader,
                isTotal: style.isTotal,
                bandedRow: style.bandedRow,
                bandedColumn: style.bandedColumn,
                isRepeatedHeader: repeatedHeader === true,
                verticalAlignment: verticalAlignmentName(measured.cell.verticalAlignment ?? measured.cell.VerticalAlignment),
                blockIds: shiftedBlocks.map(item => String(item.blockId || item.id || '')).filter(Boolean),
            });
        }
    }

    const endY = pagination.cursorY;
    const tableHeight = Math.max(DEFAULT_ROW_HEIGHT, endY - tableY);
    return {
        id: `${block.id || `block-${context.sequence || 0}`}-table`,
        blockId: block.id || '',
        type: 'table',
        pageIndex: firstPageIndex,
        lastPageIndex: pagination.pageIndex,
        sequence: Number(context.sequence || 0) || 0,
        rect: { x: tableX, y: tableY, width: tableWidth, height: pagination.splitCount > 0 ? Math.max(DEFAULT_ROW_HEIGHT, (page.body?.y || tableY) + (page.body?.height || tableHeight) - tableY) : tableHeight },
        endY,
        lines: [],
        segments: [],
        caretStops: [],
        table: {
            tableId: block.id || '',
            rowCount: rows.length,
            columnCount,
            columnWidths,
            splitCount: pagination.splitCount,
            repeatedHeaderRows: repeatedHeaders.length,
            cells: cellLayouts,
        },
        nestedBlocks,
    };
}

export function hitTestTableCell(tableLayout, pageIndex, x, y) {
    const cells = Array.isArray(tableLayout?.table?.cells)
        ? tableLayout.table.cells
        : Array.isArray(tableLayout?.cells)
            ? tableLayout.cells
            : [];
    return cells.find(cell => Number(cell.pageIndex || 0) === Number(pageIndex || 0)
        && containsPoint(cell.rect, x, y)) || null;
}

function measureCellContent(context) {
    const cell = context.cell || {};
    const blocks = normalizeCellBlocks(cell, context);
    const measuredBlocks = [];
    let cursorY = 0;

    for (let index = 0; index < blocks.length; index += 1) {
        const block = blocks[index];
        const normalized = context.normalizeTextBlock(context.model, block, context.metrics);
        const raw = context.layoutEngine.layoutParagraph(normalized, {
            x: 0,
            y: cursorY,
            width: context.contentWidth,
            minReadableWidth: 24,
            lineGap: 0,
            resolveAvailableIntervals(atY, lineHeight) {
                const interval = {
                    x: 0,
                    y: Math.max(0, Number(atY || 0) || 0),
                    width: context.contentWidth,
                    height: Math.max(1, Number(lineHeight || 18) || 18),
                    pageIndex: Number(context.page?.index || 0) || 0,
                };
                return {
                    moved: false,
                    movedToY: interval.y,
                    intervals: [interval],
                    availableIntervals: [interval],
                    pageIndex: interval.pageIndex,
                };
            },
        });
        const layout = ensureCellParagraphLayout(raw, normalized, block, context, cursorY);
        const fragments = fragmentCellParagraphLayout(layout, block, context, index);
        measuredBlocks.push(...fragments);
        const lastLine = layout.lines[layout.lines.length - 1] || null;
        cursorY = lastLine?.rect ? lastLine.rect.y + lastLine.rect.height + 4 : cursorY + 20;
    }

    return {
        ...context,
        blocks: measuredBlocks,
        contentHeight: Math.max(18, cursorY),
    };
}

function shiftMeasuredBlocks(blocks, x, y, pageIndex) {
    return (blocks || []).map(block => {
        const shifted = clone(block);
        shifted.pageIndex = Number(pageIndex ?? shifted.pageIndex ?? 0) || 0;
        shifted.rect.x += x;
        shifted.rect.y += y;
        shifted.lines = (shifted.lines || []).map(line => shiftLine(line, x, y, shifted.pageIndex));
        shifted.segments = (shifted.segments || []).map(segment => shiftSegment(segment, x, y, shifted.pageIndex));
        shifted.caretStops = (shifted.caretStops || []).map(stop => ({
            ...stop,
            pageIndex: shifted.pageIndex,
            rect: {
                ...stop.rect,
                x: (Number(stop.rect?.x || 0) || 0) + x,
                y: (Number(stop.rect?.y || 0) || 0) + y,
            },
        }));
        return shifted;
    });
}

function shiftLine(line, x, y, pageIndex) {
    return {
        ...line,
        pageIndex,
        rect: {
            ...line.rect,
            x: (Number(line.rect?.x || 0) || 0) + x,
            y: (Number(line.rect?.y || 0) || 0) + y,
        },
        baseline: (Number(line.baseline || 0) || 0) + y,
        availableIntervals: (line.availableIntervals || []).map(interval => ({
            ...interval,
            x: (Number(interval.x || 0) || 0) + x,
            y: (Number(interval.y || 0) || 0) + y,
        })),
        segments: (line.segments || []).map(segment => shiftSegment(segment, x, y, pageIndex)),
    };
}

function shiftSegment(segment, x, y, pageIndex) {
    return {
        ...segment,
        pageIndex,
        rect: {
            ...segment.rect,
            x: (Number(segment.rect?.x || 0) || 0) + x,
            y: (Number(segment.rect?.y || 0) || 0) + y,
        },
    };
}

function ensureCellParagraphLayout(layout, normalizedBlock, sourceBlock, context, y) {
    const blockId = normalizedBlock?.id || sourceBlock?.id || `${context.cell.id || 'cell'}-p`;
    const pageIndex = Number(context.page?.index || 0) || 0;
    const raw = layout && typeof layout === 'object' ? layout : {};
    raw.lines = Array.isArray(raw.lines) ? raw.lines : [];
    raw.segments = Array.isArray(raw.segments) ? raw.segments : [];
    raw.caretStops = Array.isArray(raw.caretStops) ? raw.caretStops : [];
    if (raw.lines.length === 0) {
        raw.lines.push(emptyCellLine(blockId, context.contentWidth, y, pageIndex));
    }

    raw.blockId = blockId;
    raw.lines = raw.lines.map(line => normalizeCellLine(line, blockId, pageIndex));
    raw.segments = raw.segments.map(segment => ({
        ...segment,
        blockId: segment.blockId || blockId,
        pageIndex,
    }));
    const textLength = (normalizedBlock?.content?.runs || []).map(run => String(run?.text || '')).join('').length;
    if (!raw.caretStops.some(stop => String(stop.blockId || '') === blockId && Number(stop.offset || 0) === textLength)) {
        const lastLine = raw.lines[raw.lines.length - 1];
        const lastSegment = (lastLine.segments || [])[lastLine.segments.length - 1];
        raw.caretStops.push({
            blockId,
            offset: textLength,
            lineId: lastLine.id,
            pageIndex,
            affinity: 'after',
            rect: {
                x: lastSegment?.rect ? lastSegment.rect.x + Math.max(0, Number(lastSegment.rect.width || 0) || 0) : lastLine.rect.x,
                y: lastLine.rect.y,
                width: 1,
                height: Math.max(1, Number(lastLine.rect.height || 18) || 18),
            },
        });
    }

    return raw;
}

function emptyCellLine(blockId, width, y, pageIndex) {
    return {
        id: `${blockId}-empty-cell-line`,
        blockId,
        pageIndex,
        rect: { x: 0, y, width: Math.max(1, Number(width || 1) || 1), height: 18 },
        baseline: y + 14,
        availableIntervals: [{ x: 0, y, width: Math.max(1, Number(width || 1) || 1), height: 18, pageIndex }],
        segments: [],
    };
}

function normalizeCellLine(line, blockId, pageIndex) {
    return {
        ...line,
        id: line.id || `${blockId}-line-${pageIndex}`,
        blockId: line.blockId || blockId,
        pageIndex,
        rect: line.rect || { x: 0, y: 0, width: 1, height: 18 },
        availableIntervals: line.availableIntervals || [{ x: line.rect?.x || 0, y: line.rect?.y || 0, width: line.rect?.width || 1, height: line.rect?.height || 18, pageIndex }],
        segments: (line.segments || []).map(segment => ({ ...segment, blockId: segment.blockId || blockId, pageIndex })),
    };
}

function fragmentCellParagraphLayout(layout, sourceBlock, context, index) {
    const blockId = layout.blockId || sourceBlock?.id || `${context.cell.id || 'cell'}-block-${index}`;
    const pageIndex = Number(context.page?.index || 0) || 0;
    const rect = layout.lines.reduce((current, line) => unionRect(current, line.rect), null)
        || { x: 0, y: 0, width: context.contentWidth, height: 18 };
    return [{
        id: `${blockId}-cell-fragment-${pageIndex}`,
        blockId,
        type: 'paragraph',
        sourceType: sourceBlock?.type || sourceBlock?.content?.type || 'paragraph',
        pageIndex,
        sequence: Number(context.sequence || 0) + index / 100,
        rect,
        lines: layout.lines,
        segments: layout.segments,
        caretStops: layout.caretStops,
        cell: {
            tableId: context.tableId,
            rowId: context.rowId,
            cellId: context.cell.id || '',
            rowIndex: context.rowIndex,
            cellIndex: context.cellIndex,
            columnIndex: context.columnIndex,
        },
    }];
}

function normalizeRows(rows) {
    return (Array.isArray(rows) ? rows : []).map((row, rowIndex) => ({
        ...row,
        id: row?.id || row?.Id || `row-${rowIndex + 1}`,
        cells: Array.isArray(row?.cells) ? row.cells : Array.isArray(row?.Cells) ? row.Cells : [],
    })).filter(row => row.cells.length > 0);
}

function normalizeCellBlocks(cell, context) {
    const source = Array.isArray(cell.blocks) ? cell.blocks : Array.isArray(cell.Blocks) ? cell.Blocks : [];
    if (source.length > 0) {
        return source.map((block, index) => ({
            ...block,
            id: block.id || block.Id || `${cell.id || cell.Id || 'cell'}-p-${index + 1}`,
            type: block.type || block.Type || block.content?.type || 'paragraph',
            content: normalizeBlockContent(block.content || block.Content, block.type || block.Type || 'paragraph'),
        }));
    }

    return [{
        id: `${cell.id || cell.Id || `table-${context.rowIndex}-${context.cellIndex}`}-p`,
        type: 'paragraph',
        order: 1,
        paragraphProperties: {},
        content: {
            type: 'paragraph',
            runs: [{ id: `${cell.id || cell.Id || 'cell'}-empty-run`, type: 'text', text: '', marks: [] }],
        },
    }];
}

function normalizeBlockContent(content, fallbackType) {
    const value = content && typeof content === 'object' ? { ...content } : {};
    value.type = value.type || value.Type || fallbackType || 'paragraph';
    value.runs = Array.isArray(value.runs) ? value.runs : Array.isArray(value.Runs) ? value.Runs : [];
    return value;
}

function maxColumnCount(rows) {
    return rows.reduce((max, row) => Math.max(max, row.cells.reduce((sum, cell) => {
        if (cell.merge?.isOrigin === false || cell.merge?.IsOrigin === false) {
            return sum;
        }

        return sum + Math.max(1, Number(cell.columnSpan ?? cell.ColumnSpan ?? 1) || 1);
    }, 0)), 1);
}

function resolveColumnWidths(rows, columnCount, tableWidth) {
    const explicit = new Array(columnCount).fill(0);
    for (const row of rows) {
        let columnIndex = 0;
        for (const cell of row.cells) {
            if (cell.merge?.isOrigin === false || cell.merge?.IsOrigin === false) {
                continue;
            }

            const span = Math.max(1, Number(cell.columnSpan ?? cell.ColumnSpan ?? 1) || 1);
            const width = Number(cell.width ?? cell.Width ?? 0) || 0;
            if (width > 0 && span === 1 && columnIndex < explicit.length) {
                explicit[columnIndex] = Math.max(explicit[columnIndex], width);
            }

            columnIndex += span;
        }
    }

    const explicitTotal = explicit.reduce((sum, value) => sum + value, 0);
    const missing = explicit.filter(value => value <= 0).length;
    const fallback = Math.max(32, (tableWidth - explicitTotal) / Math.max(1, missing));
    const widths = explicit.map(value => value > 0 ? value : fallback);
    const total = widths.reduce((sum, value) => sum + value, 0);
    if (total <= 0) {
        return new Array(columnCount).fill(tableWidth / columnCount);
    }

    return widths.map(value => value * tableWidth / total);
}

function resolveTableWidth(layout, bodyWidth) {
    const width = Number(layout?.width ?? layout?.Width ?? 0) || 0;
    return Math.max(80, Math.min(Math.max(80, Number(bodyWidth || 600) || 600), width > 0 ? width : Number(bodyWidth || 600) || 600));
}

function resolveTableX(layout, bodyX, bodyWidth, tableWidth) {
    const alignment = String(layout?.alignment ?? layout?.Alignment ?? 'left').toLowerCase();
    if (alignment === '1' || alignment === 'center' || alignment === 'middle') {
        return Number(bodyX || 0) + Math.max(0, (Number(bodyWidth || tableWidth) - tableWidth) / 2);
    }

    if (alignment === '2' || alignment === 'right' || alignment === 'end') {
        return Number(bodyX || 0) + Math.max(0, Number(bodyWidth || tableWidth) - tableWidth);
    }

    return Number(bodyX || 0) || 0;
}

function sumColumns(widths, start, span) {
    return widths.slice(Math.max(0, start), Math.max(0, start) + Math.max(0, span)).reduce((sum, value) => sum + value, 0);
}

function resolveBorderColor(cell, table) {
    const borders = cell.borders || cell.Borders || table.layout?.borders || table.layout?.Borders || {};
    return borders.top || borders.Top || borders.right || borders.Right || borders.bottom || borders.Bottom || borders.left || borders.Left || DEFAULT_BORDER_COLOR;
}

function verticalContentOffset(cell, rowHeight, contentHeight, padding) {
    const available = Math.max(0, rowHeight - padding * 2 - contentHeight);
    const alignment = verticalAlignmentName(cell.verticalAlignment ?? cell.VerticalAlignment);
    if (alignment === 'middle') {
        return available / 2;
    }

    if (alignment === 'bottom') {
        return available;
    }

    return 0;
}

function verticalAlignmentName(value) {
    if (typeof value === 'number') {
        return ['top', 'middle', 'bottom'][Math.max(0, Math.min(2, Math.trunc(value)))] || 'top';
    }

    const normalized = String(value || '').toLowerCase();
    if (normalized === '1' || normalized === 'center') return 'middle';
    if (normalized === '2' || normalized === 'end') return 'bottom';
    if (normalized === 'middle' || normalized === 'bottom') return normalized;
    return 'top';
}

function unionRect(current, rect) {
    if (!rect) {
        return current;
    }

    if (!current) {
        return { x: rect.x, y: rect.y, width: rect.width, height: rect.height };
    }

    const left = Math.min(current.x, rect.x);
    const top = Math.min(current.y, rect.y);
    const right = Math.max(current.x + current.width, rect.x + rect.width);
    const bottom = Math.max(current.y + current.height, rect.y + rect.height);
    return { x: left, y: top, width: right - left, height: bottom - top };
}

function containsPoint(rect, x, y) {
    const left = Number(rect?.x || 0) || 0;
    const top = Number(rect?.y || 0) || 0;
    const width = Math.max(1, Number(rect?.width || 0) || 0);
    const height = Math.max(1, Number(rect?.height || 0) || 0);
    return Number(x || 0) >= left && Number(x || 0) <= left + width && Number(y || 0) >= top && Number(y || 0) <= top + height;
}

function clone(value) {
    if (typeof structuredClone === 'function') {
        return structuredClone(value);
    }

    return JSON.parse(JSON.stringify(value ?? null));
}
