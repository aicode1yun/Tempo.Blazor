const DEFAULT_HEADER_BACKGROUND = 'rgba(226, 232, 240, 0.84)';
const DEFAULT_TOTAL_BACKGROUND = 'rgba(241, 245, 249, 0.94)';
const DEFAULT_BANDED_ROW_BACKGROUND = 'rgba(248, 250, 252, 0.92)';
const DEFAULT_BANDED_COLUMN_BACKGROUND = 'rgba(239, 246, 255, 0.62)';
const DEFAULT_CELL_BACKGROUND = 'rgba(255, 255, 255, 0.96)';
const DEFAULT_BORDER_COLOR = '#94a3b8';

export function resolveTableCellStyle(table, cell, rowIndex, columnIndex, rowCount) {
    const layout = table?.layout || table?.Layout || {};
    const palette = layout.style || layout.Style || {};
    const isHeader = cell?.isHeader === true || cell?.IsHeader === true || (layout.headerRow === true || layout.HeaderRow === true) && rowIndex === 0;
    const isTotal = (layout.totalRow === true || layout.TotalRow === true) && rowIndex === rowCount - 1;
    const bandedRows = layout.bandedRows === true || layout.BandedRows === true;
    const bandedColumns = layout.bandedColumns === true || layout.BandedColumns === true;
    const explicitBackground = cell?.backgroundColor ?? cell?.BackgroundColor;

    let backgroundColor = explicitBackground
        ?? palette.cellBackgroundColor
        ?? palette.CellBackgroundColor
        ?? layout.backgroundColor
        ?? layout.BackgroundColor
        ?? DEFAULT_CELL_BACKGROUND;

    if (!explicitBackground && isHeader) {
        backgroundColor = palette.headerBackgroundColor ?? palette.HeaderBackgroundColor ?? DEFAULT_HEADER_BACKGROUND;
    } else if (!explicitBackground && isTotal) {
        backgroundColor = palette.totalBackgroundColor ?? palette.TotalBackgroundColor ?? DEFAULT_TOTAL_BACKGROUND;
    } else if (!explicitBackground && bandedRows && rowIndex % 2 === 1) {
        backgroundColor = palette.bandedRowBackgroundColor ?? palette.BandedRowBackgroundColor ?? DEFAULT_BANDED_ROW_BACKGROUND;
    } else if (!explicitBackground && bandedColumns && columnIndex % 2 === 1) {
        backgroundColor = palette.bandedColumnBackgroundColor ?? palette.BandedColumnBackgroundColor ?? DEFAULT_BANDED_COLUMN_BACKGROUND;
    }

    return {
        backgroundColor,
        borderColor: resolveBorderColor(cell, table, palette),
        isHeader,
        isTotal,
        bandedRow: bandedRows && rowIndex % 2 === 1,
        bandedColumn: bandedColumns && columnIndex % 2 === 1,
    };
}

export function tableRepeatsHeaderRows(table) {
    const layout = table?.layout || table?.Layout || {};
    return layout.repeatHeaderRows === true
        || layout.RepeatHeaderRows === true
        || layout.headerRow === true
        || layout.HeaderRow === true;
}

export function tableCellPadding(table, cell, fallback = 8) {
    const layout = table?.layout || table?.Layout || {};
    return Math.max(0, Number(cell?.padding ?? cell?.Padding ?? layout.cellPadding ?? layout.CellPadding ?? fallback) || fallback);
}

export function tableCellSpacing(table) {
    const layout = table?.layout || table?.Layout || {};
    return Math.max(0, Number(layout.cellSpacing ?? layout.CellSpacing ?? 0) || 0);
}

function resolveBorderColor(cell, table, palette) {
    const borders = cell?.borders || cell?.Borders || {};
    const tableBorders = table?.layout?.borders || table?.layout?.Borders || table?.Layout?.Borders || {};
    return firstBorderColor(borders)
        || firstBorderColor(tableBorders)
        || palette.borderColor
        || palette.BorderColor
        || DEFAULT_BORDER_COLOR;
}

function firstBorderColor(borders) {
    for (const side of ['top', 'Top', 'right', 'Right', 'bottom', 'Bottom', 'left', 'Left']) {
        const value = borders?.[side];
        if (typeof value === 'string' && value.trim()) {
            return value.trim();
        }
    }

    return '';
}
