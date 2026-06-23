const POINTS_TO_CSS_PIXELS = 96 / 72;
const CSS_PIXELS_TO_POINTS = 72 / 96;
const DEFAULT_TAB_WIDTH_POINTS = 36;
const TAB_STOP_EPSILON = 0.25;

export function normalizeTabStops(paragraphProperties = {}) {
    const sourceStops = Array.isArray(paragraphProperties.tabStops)
        ? paragraphProperties.tabStops
        : Array.isArray(paragraphProperties.TabStops) ? paragraphProperties.TabStops : [];
    const defaultTabWidth = positiveNumber(
        paragraphProperties.defaultTabWidth ?? paragraphProperties.DefaultTabWidth,
        DEFAULT_TAB_WIDTH_POINTS);
    const tabStops = sourceStops
        .map((stop, index) => normalizeTabStop(stop, index))
        .filter(Boolean)
        .sort((left, right) => left.position - right.position);

    return {
        defaultTabWidth,
        tabStops,
    };
}

export function nextTabStop(positionPoints, tabModel) {
    const current = Math.max(0, Number(positionPoints) || 0);
    const explicit = (tabModel?.tabStops || [])
        .find(stop => stop.position > current + TAB_STOP_EPSILON);
    if (explicit) {
        return {
            ...explicit,
            explicit: true,
        };
    }

    const interval = positiveNumber(tabModel?.defaultTabWidth, DEFAULT_TAB_WIDTH_POINTS);
    const position = Math.max(interval, Math.ceil((current + TAB_STOP_EPSILON) / interval) * interval);
    return {
        position,
        alignment: 'left',
        leader: 'none',
        explicit: false,
    };
}

export function applyTabStopsToParagraphLayout(layout, sourceBlock, options = {}) {
    if (!layout || !Array.isArray(layout.lines)) {
        return layout;
    }

    const tabModel = normalizeTabStops(sourceBlock?.paragraphProperties || sourceBlock?.ParagraphProperties || {});
    const leaders = [];
    const metrics = options.metrics || null;

    for (const line of layout.lines) {
        const segments = Array.isArray(line.segments) ? line.segments : [];
        if (!segments.some(segment => segment?.type === 'tab')) {
            continue;
        }

        line.tabLeaders = Array.isArray(line.tabLeaders) ? line.tabLeaders : [];
        const baseX = lineBaseX(line);
        for (let index = 0; index < segments.length; index += 1) {
            const tab = segments[index];
            if (tab?.type !== 'tab' || !tab.rect) {
                continue;
            }

            const tabX = Number(tab.rect.x || 0) || 0;
            const currentPositionPoints = Math.max(0, (tabX - baseX) * CSS_PIXELS_TO_POINTS);
            const stop = nextTabStop(currentPositionPoints, tabModel);
            const targetX = baseX + stop.position * POINTS_TO_CSS_PIXELS;
            const group = followingTabGroup(segments, index + 1);
            const groupMetrics = measureGroup(group, metrics);
            const groupLeft = groupMetrics.left ?? (tabX + Math.max(0, Number(tab.rect.width || 0) || 0));
            const groupWidth = groupMetrics.width;
            const alignedGroupStart = alignedStartForStop(stop, targetX, groupMetrics);
            const delta = alignedGroupStart - groupLeft;

            shiftSegments(group.segments, delta);
            shiftFollowingSegments(segments, group.endIndex + 1, delta);
            shiftCaretStops(layout, line, tab, delta, alignedGroupStart);

            tab.rect.width = Math.max(0, alignedGroupStart - tabX);
            tab.tabStop = stop;
            tab.text = '';
            tab.kind = 'tab';

            if (stop.alignment === 'bar') {
                const bar = createTabLeader('bar', stop, line, tab, targetX, targetX, groupMetrics);
                leaders.push(bar);
                line.tabLeaders.push(bar);
            } else if (stop.leader !== 'none' && alignedGroupStart - tabX > 4) {
                const leader = createTabLeader(stop.leader, stop, line, tab, tabX + 2, alignedGroupStart - 2, groupMetrics);
                leaders.push(leader);
                line.tabLeaders.push(leader);
            }
        }

        refreshLineRect(line);
    }

    layout.tabLeaders = leaders;
    return layout;
}

function normalizeTabStop(stop, index) {
    const position = Number(stop?.position ?? stop?.Position);
    if (!Number.isFinite(position) || position < 0) {
        return null;
    }

    return {
        id: String(stop?.id ?? stop?.Id ?? `tab-${index}`),
        position,
        alignment: normalizeAlignment(stop?.alignment ?? stop?.Alignment),
        leader: normalizeLeader(stop?.leader ?? stop?.Leader),
    };
}

function normalizeAlignment(value) {
    if (typeof value === 'number') {
        return ['left', 'center', 'right', 'decimal', 'bar'][Math.max(0, Math.min(4, Math.trunc(value)))] || 'left';
    }

    const normalized = String(value || '').replace(/[\s_-]/g, '').toLowerCase();
    if (normalized === 'center' || normalized === 'centre' || normalized === 'middle') return 'center';
    if (normalized === 'right' || normalized === 'end') return 'right';
    if (normalized === 'decimal') return 'decimal';
    if (normalized === 'bar') return 'bar';
    return 'left';
}

function normalizeLeader(value) {
    if (typeof value === 'number') {
        return ['none', 'dots', 'dash', 'underline'][Math.max(0, Math.min(3, Math.trunc(value)))] || 'none';
    }

    const normalized = String(value || '').replace(/[\s_-]/g, '').toLowerCase();
    if (normalized === 'dot' || normalized === 'dots' || normalized === 'dotted') return 'dots';
    if (normalized === 'dash' || normalized === 'dashes' || normalized === 'dashed') return 'dash';
    if (normalized === 'underline' || normalized === 'line') return 'underline';
    return 'none';
}

function followingTabGroup(segments, startIndex) {
    const groupSegments = [];
    let endIndex = startIndex - 1;
    for (let index = startIndex; index < segments.length; index += 1) {
        const segment = segments[index];
        if (segment?.type === 'tab') {
            break;
        }

        groupSegments.push(segment);
        endIndex = index;
    }

    return {
        segments: groupSegments,
        endIndex,
    };
}

function measureGroup(group, metrics) {
    const segments = group?.segments || [];
    const rects = segments.map(segment => segment?.rect).filter(Boolean);
    if (rects.length === 0) {
        return {
            segments,
            left: null,
            width: 0,
            decimalOffset: 0,
            baselineStyle: {},
        };
    }

    const left = Math.min(...rects.map(rect => Number(rect.x || 0) || 0));
    const right = Math.max(...rects.map(rect => (Number(rect.x || 0) || 0) + Math.max(0, Number(rect.width || 0) || 0)));
    return {
        segments,
        left,
        width: Math.max(0, right - left),
        decimalOffset: decimalOffset(segments, left, metrics, Math.max(0, right - left)),
        baselineStyle: segments.find(segment => segment?.style)?.style || {},
    };
}

function decimalOffset(segments, groupLeft, metrics, fallbackWidth) {
    for (const segment of segments) {
        const text = String(segment?.text || '');
        const index = Math.max(text.indexOf('.'), text.indexOf(','));
        if (index < 0 || !segment?.rect) {
            continue;
        }

        const x = Number(segment.rect.x || 0) || 0;
        const prefix = text.slice(0, index);
        const measured = metrics && typeof metrics.measureText === 'function'
            ? Number(metrics.measureText(prefix, segment.style || {})?.width)
            : NaN;
        const prefixWidth = Number.isFinite(measured)
            ? measured
            : Math.max(0, Number(segment.rect.width || 0) || 0) * (prefix.length / Math.max(1, text.length));
        return Math.max(0, x + prefixWidth - groupLeft);
    }

    return fallbackWidth;
}

function alignedStartForStop(stop, targetX, groupMetrics) {
    if (stop.alignment === 'right') {
        return targetX - groupMetrics.width;
    }

    if (stop.alignment === 'center') {
        return targetX - groupMetrics.width / 2;
    }

    if (stop.alignment === 'decimal') {
        return targetX - groupMetrics.decimalOffset;
    }

    return targetX;
}

function shiftSegments(segments, delta) {
    if (!delta) {
        return;
    }

    for (const segment of segments || []) {
        if (segment?.rect) {
            segment.rect.x += delta;
        }
    }
}

function shiftFollowingSegments(segments, startIndex, delta) {
    if (!delta) {
        return;
    }

    for (let index = Math.max(0, startIndex); index < segments.length; index += 1) {
        if (segments[index]?.rect) {
            segments[index].rect.x += delta;
        }
    }
}

function shiftCaretStops(layout, line, tab, delta, tabAdvanceX) {
    const lineId = String(line?.id || '');
    const tabEnd = Number(tab?.end ?? tab?.start ?? 0) || 0;
    for (const stop of layout?.caretStops || []) {
        if (lineId && String(stop?.lineId || '') !== lineId) {
            continue;
        }

        if (!stop?.rect) {
            continue;
        }

        const offset = Number(stop.offset || 0) || 0;
        if (offset === tabEnd) {
            stop.rect.x = tabAdvanceX;
        } else if (offset > tabEnd && delta) {
            stop.rect.x += delta;
        }
    }
}

function createTabLeader(kind, stop, line, tab, x1, x2, groupMetrics) {
    const height = Math.max(1, Number(tab?.rect?.height || line?.rect?.height || 16) || 16);
    const y = Number(tab?.rect?.y ?? line?.rect?.y ?? 0) || 0;
    return {
        id: `${line?.id || 'line'}-${tab?.id || tab?.start || 'tab'}-${kind}`,
        type: kind === 'bar' ? 'bar' : 'leader',
        leader: kind,
        alignment: stop.alignment,
        pageIndex: Number(line?.pageIndex || 0) || 0,
        blockId: line?.blockId || tab?.blockId || '',
        x: Math.min(x1, x2),
        y,
        width: Math.max(0, Math.abs(x2 - x1)),
        height,
        baseline: Number(line?.baseline || 0) || (y + height * 0.78),
        style: groupMetrics.baselineStyle || {},
    };
}

function refreshLineRect(line) {
    const rects = (line.segments || []).map(segment => segment?.rect).filter(Boolean);
    if (!line.rect || rects.length === 0) {
        return;
    }

    const right = Math.max(...rects.map(rect => (Number(rect.x || 0) || 0) + Math.max(0, Number(rect.width || 0) || 0)));
    line.rect.width = Math.max(line.rect.width || 0, right - (Number(line.rect.x || 0) || 0));
    line.width = line.rect.width;
}

function lineBaseX(line) {
    const interval = Array.isArray(line?.availableIntervals) ? line.availableIntervals[0] : null;
    return Number(interval?.x ?? line?.rect?.x ?? 0) || 0;
}

function positiveNumber(value, fallback) {
    const parsed = Number(value);
    return Number.isFinite(parsed) && parsed > 0 ? parsed : fallback;
}
