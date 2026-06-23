import { pointsToCssPixels } from './canvas-text-style.mjs';

const DEFAULT_COLUMN_SPACING_PT = 36;

export function createColumnFlow(page, section = {}) {
    const columns = buildColumns(page, section);
    const config = readColumns(section);
    return {
        columns,
        separatorLine: columns.length > 1 && booleanValue(config.separatorLine ?? config.SeparatorLine, false),
        balanced: columns.length > 1 && shouldBalanceColumns(section),
    };
}

export function shouldBalanceColumns(section = {}) {
    const config = readColumns(section);
    const value = config.balance ?? config.Balance ?? config.balanced ?? config.Balanced ?? config.newspaperBalance ?? config.NewspaperBalance;
    return value === true || String(value || '').toLowerCase() === 'true' || String(value || '').toLowerCase() === 'newspaper';
}

export function nextColumnFrame(page, columnIndex = 0) {
    const columns = Array.isArray(page?.columns) && page.columns.length > 0 ? page.columns : [page?.body].filter(Boolean);
    const nextIndex = Number(columnIndex || 0) + 1;
    if (nextIndex < columns.length) {
        return {
            pageIndex: Number(page?.index || 0) || 0,
            columnIndex: nextIndex,
            frame: columns[nextIndex],
        };
    }

    return null;
}

export function columnFrame(page, columnIndex = 0) {
    const columns = Array.isArray(page?.columns) && page.columns.length > 0 ? page.columns : [page?.body].filter(Boolean);
    return columns[Math.max(0, Math.min(columns.length - 1, Number(columnIndex || 0) || 0))] || page?.body || { x: 0, y: 0, width: 1, height: 1 };
}

export function createColumnBreakLayout(block, page, columnIndex, sequence) {
    const frame = columnFrame(page, columnIndex);
    return {
        id: `${block.id || `block-${sequence}`}-column-break`,
        blockId: block.id || '',
        type: 'columnBreak',
        pageIndex: Number(page?.index || 0) || 0,
        columnIndex: Number(columnIndex || 0) || 0,
        sectionId: block?.sectionId || page?.sectionId || '',
        rect: { x: frame.x, y: frame.y, width: frame.width, height: 0 },
        lines: [],
        segments: [],
        sequence,
    };
}

export function balanceParagraphColumns(layout, pages, section = {}) {
    if (!layout || !shouldBalanceColumns(section) || !Array.isArray(layout.lines) || layout.lines.length < 2) {
        return layout;
    }

    const pageLookup = typeof pages === 'function'
        ? pages
        : pageIndex => Array.isArray(pages) ? pages[pageIndex] : null;
    const groups = new Map();
    for (const line of layout.lines) {
        const pageIndex = Number(line?.pageIndex || 0) || 0;
        const page = pageLookup(pageIndex);
        if (!page || !Array.isArray(page.columns) || page.columns.length < 2) {
            continue;
        }

        const key = String(pageIndex);
        if (!groups.has(key)) {
            groups.set(key, { page, lines: [] });
        }

        groups.get(key).lines.push(line);
    }

    for (const group of groups.values()) {
        balancePageLines(layout, group.page, group.lines);
    }

    return layout;
}

function balancePageLines(layout, page, lines) {
    const columns = Array.isArray(page?.columns) ? page.columns : [];
    if (columns.length < 2 || !Array.isArray(lines) || lines.length < columns.length + 1) {
        return;
    }

    const orderedLines = lines
        .slice()
        .sort((left, right) => (Number(left?.rect?.y || 0) || 0) - (Number(right?.rect?.y || 0) || 0));
    const maxLineHeight = Math.max(1, ...orderedLines.map(line => Math.max(1, Number(line?.rect?.height || 0) || 1)));
    const startY = Math.min(...orderedLines.map(line => Number(line?.rect?.y || 0) || 0));
    const columnBottom = Math.min(...columns.map(column => (Number(column?.y || 0) || 0) + (Number(column?.height || 0) || 0)).filter(Number.isFinite));
    const capacity = Math.max(1, Math.floor(Math.max(1, columnBottom - startY) / maxLineHeight));
    if (orderedLines.length > capacity * columns.length) {
        return;
    }

    const desired = balancedColumnCounts(orderedLines.length, columns.length);
    const currentCounts = countLinesByColumn(orderedLines, columns.length);
    const currentSpread = Math.max(...currentCounts) - Math.min(...currentCounts);
    const desiredSpread = Math.max(...desired) - Math.min(...desired);
    if (currentSpread <= desiredSpread && currentCounts.every((count, index) => count === desired[index])) {
        return;
    }

    const lineById = new Map(orderedLines.map(line => [String(line?.id || ''), line]));
    const segmentLineIds = new Map();
    for (const line of orderedLines) {
        for (const segment of line?.segments || []) {
            if (segment?.id) {
                segmentLineIds.set(String(segment.id), String(line.id || ''));
            }
        }
    }

    let lineOffset = 0;
    for (let columnIndex = 0; columnIndex < columns.length; columnIndex += 1) {
        const column = columns[columnIndex];
        const count = desired[columnIndex] || 0;
        for (let index = 0; index < count; index += 1) {
            const line = orderedLines[lineOffset + index];
            if (!line?.rect) {
                continue;
            }

            moveLineToColumn(line, column, columns, columnIndex, index, maxLineHeight, startY, layout, lineById, segmentLineIds);
        }

        lineOffset += count;
    }

    layout.lines.sort(compareLineVisualOrder);
}

function moveLineToColumn(line, column, columns, columnIndex, lineIndex, lineHeight, startY, layout, lineById, segmentLineIds) {
    const oldRect = { ...(line.rect || {}) };
    const oldX = Number(oldRect.x || 0) || 0;
    const oldY = Number(oldRect.y || 0) || 0;
    const oldColumnIndex = Math.max(0, Math.min(columns.length - 1, Number(line?.columnIndex || 0) || 0));
    const oldColumn = columns[oldColumnIndex] || columns[0] || { x: 0 };
    const lineIndent = Math.max(0, oldX - (Number(oldColumn.x || 0) || 0));
    const newX = (Number(column.x || 0) || 0) + lineIndent;
    const newY = (Number(startY || column.y || 0) || 0) + lineIndex * lineHeight;
    const dx = newX - oldX;
    const dy = newY - oldY;
    const translatedSegments = new Set();
    const translatedLeaders = new Set();

    line.columnIndex = columnIndex;
    line.rect = {
        ...oldRect,
        x: newX,
        y: newY,
        width: Math.max(1, (Number(column.width || oldRect.width || 1) || 1) - lineIndent),
        height: Math.max(1, Number(oldRect.height || lineHeight) || lineHeight),
    };
    line.availableIntervals = [{
        x: newX,
        y: newY,
        width: line.rect.width,
        height: line.rect.height,
        pageIndex: Number(line.pageIndex || 0) || 0,
        columnIndex,
    }];

    for (const segment of line.segments || []) {
        translateRect(segment?.rect, dx, dy);
        segment.columnIndex = columnIndex;
        translatedSegments.add(segment);
    }

    for (const leader of line.tabLeaders || []) {
        translateRect(leader, dx, dy);
        leader.columnIndex = columnIndex;
        translatedLeaders.add(leader);
    }

    for (const segment of layout?.segments || []) {
        if (!translatedSegments.has(segment) && segmentLineIds.get(String(segment?.id || '')) === String(line.id || '')) {
            translateRect(segment?.rect, dx, dy);
            segment.columnIndex = columnIndex;
        }
    }

    for (const stop of layout?.caretStops || []) {
        if (String(stop?.lineId || '') === String(line.id || '')) {
            translateRect(stop?.rect, dx, dy);
            stop.columnIndex = columnIndex;
        }
    }

    for (const leader of layout?.tabLeaders || []) {
        const owner = lineById.get(String(leader?.lineId || ''));
        if (!translatedLeaders.has(leader) && owner === line) {
            translateRect(leader, dx, dy);
            leader.columnIndex = columnIndex;
        }
    }
}

function balancedColumnCounts(lineCount, columnCount) {
    const base = Math.floor(lineCount / columnCount);
    const remainder = lineCount % columnCount;
    return Array.from({ length: columnCount }, (_, index) => base + (index < remainder ? 1 : 0));
}

function countLinesByColumn(lines, columnCount) {
    const counts = Array.from({ length: columnCount }, () => 0);
    for (const line of lines) {
        const index = Math.max(0, Math.min(columnCount - 1, Number(line?.columnIndex || 0) || 0));
        counts[index] += 1;
    }

    return counts;
}

function translateRect(rect, dx, dy) {
    if (!rect) {
        return;
    }

    rect.x = (Number(rect.x || 0) || 0) + dx;
    rect.y = (Number(rect.y || 0) || 0) + dy;
}

function compareLineVisualOrder(left, right) {
    const leftPage = Number(left?.pageIndex || 0) || 0;
    const rightPage = Number(right?.pageIndex || 0) || 0;
    if (leftPage !== rightPage) {
        return leftPage - rightPage;
    }

    const leftColumn = Number(left?.columnIndex || 0) || 0;
    const rightColumn = Number(right?.columnIndex || 0) || 0;
    if (leftColumn !== rightColumn) {
        return leftColumn - rightColumn;
    }

    return (Number(left?.rect?.y || 0) || 0) - (Number(right?.rect?.y || 0) || 0);
}

function buildColumns(page, section) {
    const body = page?.body || { x: 0, y: 0, width: 1, height: 1 };
    const config = readColumns(section);
    const items = Array.isArray(config.items) ? config.items : (Array.isArray(config.Items) ? config.Items : []);
    const preset = String(config.preset || config.Preset || '').toLowerCase();
    const count = normalizeCount(config.count ?? config.Count, preset, items.length);
    const spacing = Math.max(0, pointsToCssPixels(config.spacing ?? config.Spacing ?? DEFAULT_COLUMN_SPACING_PT));

    if (count <= 1) {
        return [createColumn(0, body.x, body.y, body.width, body.height, spacing)];
    }

    if (items.length === count && preset === 'custom') {
        return customColumns(body, items, spacing);
    }

    if (preset === 'left' || preset === 'right') {
        return asymmetricColumns(body, spacing, preset);
    }

    const width = Math.max(1, (body.width - spacing * (count - 1)) / count);
    return Array.from({ length: count }, (_, index) =>
        createColumn(index, body.x + index * (width + spacing), body.y, width, body.height, spacing));
}

function customColumns(body, items, defaultSpacing) {
    let x = body.x;
    return items.map((item, index) => {
        const spacingAfter = Math.max(0, pointsToCssPixels(item?.spacingAfter ?? item?.SpacingAfter ?? defaultSpacing));
        const remainingColumns = Math.max(1, items.length - index);
        const fallbackWidth = Math.max(1, (body.x + body.width - x - defaultSpacing * (remainingColumns - 1)) / remainingColumns);
        const width = Math.min(Math.max(1, pointsToCssPixels(item?.width ?? item?.Width ?? fallbackWidth)), Math.max(1, body.x + body.width - x));
        const column = createColumn(index, x, body.y, width, body.height, spacingAfter);
        x += width + spacingAfter;
        return column;
    });
}

function asymmetricColumns(body, spacing, preset) {
    const narrow = Math.max(1, (body.width - spacing) * 0.36);
    const wide = Math.max(1, body.width - spacing - narrow);
    if (preset === 'left') {
        return [
            createColumn(0, body.x, body.y, narrow, body.height, spacing),
            createColumn(1, body.x + narrow + spacing, body.y, wide, body.height, spacing),
        ];
    }

    return [
        createColumn(0, body.x, body.y, wide, body.height, spacing),
        createColumn(1, body.x + wide + spacing, body.y, narrow, body.height, spacing),
    ];
}

function createColumn(index, x, y, width, height, spacingAfter) {
    return {
        index,
        x,
        y,
        width: Math.max(1, width),
        height: Math.max(1, height),
        bottom: y + Math.max(1, height),
        spacingAfter,
    };
}

function readColumns(section) {
    const properties = section?.properties || section?.Properties || {};
    return properties.columns || properties.Columns || {};
}

function normalizeCount(value, preset, customCount) {
    if (preset === 'two' || preset === 'left' || preset === 'right') {
        return 2;
    }

    if (preset === 'three') {
        return 3;
    }

    if (preset === 'custom' && customCount > 0) {
        return Math.min(8, Math.max(1, customCount));
    }

    const parsed = Number(value);
    return Number.isFinite(parsed) ? Math.min(8, Math.max(1, Math.trunc(parsed))) : 1;
}

function booleanValue(value, fallback) {
    if (value === true || value === false) {
        return value;
    }

    return fallback;
}
