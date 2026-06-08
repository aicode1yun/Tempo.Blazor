import { createCanvasRunStyle, pointsToCssPixels } from './canvas-text-style.mjs';

export function createLineNumberingState() {
    return {
        documentCounter: 0,
        pageCounters: new Map(),
        sectionCounters: new Map(),
    };
}

export function lineNumbersForFragment(fragment, section, page, state, model, metrics) {
    const config = normalizeLineNumbering(section);
    if (!config.enabled) {
        return [];
    }

    const labels = [];
    const lines = Array.isArray(fragment?.lines) ? fragment.lines : [];
    for (const line of lines) {
        const ordinal = nextOrdinal(config, section, line, state);
        if (!shouldRenderOrdinal(ordinal, config)) {
            continue;
        }

        const text = String(ordinal);
        const style = lineNumberStyle(model, fragment);
        const measured = measureText(metrics, text, style);
        const frame = frameForLine(page, line);
        labels.push({
            id: `${fragment.blockId || 'block'}-line-number-${line.id || labels.length}`,
            blockId: fragment.blockId || '',
            sectionId: section?.id || fragment.sectionId || '',
            pageIndex: Number(line.pageIndex ?? fragment.pageIndex ?? 0) || 0,
            columnIndex: Number(line.columnIndex ?? 0) || 0,
            text,
            x: Math.max(2, frame.x - config.distanceFromText - measured.width),
            y: Number(line.rect?.y || 0) || 0,
            baseline: Number(line.baseline || 0) || (Number(line.rect?.y || 0) + Number(line.rect?.height || 16) * 0.78),
            width: measured.width,
            height: Math.max(1, Number(line.rect?.height || measured.lineHeight || 16) || 16),
            style,
        });
    }

    return labels;
}

function normalizeLineNumbering(section) {
    const properties = section?.properties || section?.Properties || {};
    const source = properties.lineNumbering || properties.LineNumbering || {};
    return {
        enabled: source.enabled === true || source.Enabled === true,
        startAt: integer(source.startAt ?? source.StartAt, 1),
        increment: Math.max(1, integer(source.increment ?? source.Increment, 1)),
        distanceFromText: Math.max(0, pointsToCssPixels(source.distanceFromText ?? source.DistanceFromText ?? 18)),
        restart: normalizeRestart(source.restart ?? source.Restart),
    };
}

function nextOrdinal(config, section, line, state) {
    const pageIndex = Number(line.pageIndex || 0) || 0;
    const sectionId = String(section?.id || '');
    const key = config.restart === 'page'
        ? `page:${pageIndex}`
        : config.restart === 'section'
            ? `section:${sectionId}`
            : 'document';
    const map = config.restart === 'page'
        ? state.pageCounters
        : config.restart === 'section'
            ? state.sectionCounters
            : null;
    const current = map ? (map.get(key) || 0) : state.documentCounter;
    const next = current + 1;
    if (map) {
        map.set(key, next);
    } else {
        state.documentCounter = next;
    }

    return config.startAt + (next - 1) * config.increment;
}

function shouldRenderOrdinal(ordinal, config) {
    if (config.increment <= 1) {
        return true;
    }

    return (ordinal - config.startAt) % config.increment === 0;
}

function lineNumberStyle(model, fragment) {
    const style = createCanvasRunStyle(model, { type: 'paragraph', content: { type: 'paragraph' } }, { marks: [] });
    return {
        ...style,
        fontSize: Math.max(9, Number(style.fontSize || 14) * 0.78),
        color: '#64748b',
        fontWeight: '400',
    };
}

function measureText(metrics, text, style) {
    if (metrics?.measureText) {
        return metrics.measureText(text, style);
    }

    if (metrics?.measureRun) {
        return metrics.measureRun({ text, ...style });
    }

    return {
        width: Math.max(1, text.length * Math.max(6, Number(style.fontSize || 12) * 0.55)),
        lineHeight: Math.max(12, Number(style.fontSize || 12) * 1.2),
    };
}

function frameForLine(page, line) {
    const columnIndex = Number(line.columnIndex ?? 0) || 0;
    const columns = Array.isArray(page?.columns) && page.columns.length > 0 ? page.columns : [];
    return columns[columnIndex] || page?.body || { x: 0 };
}

function normalizeRestart(value) {
    const normalized = String(value || 'continuous').replace(/[\s_-]/g, '').toLowerCase();
    if (normalized === 'page' || normalized === 'perpage') {
        return 'page';
    }

    if (normalized === 'section' || normalized === 'persection') {
        return 'section';
    }

    return 'continuous';
}

function integer(value, fallback) {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? Math.trunc(parsed) : fallback;
}
