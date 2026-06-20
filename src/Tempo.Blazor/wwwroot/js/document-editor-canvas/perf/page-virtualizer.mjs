const DEFAULT_PAGE_GAP = 24;
const DEFAULT_PADDING_BLOCK = 24;

export function createPageVirtualizer(options = {}) {
    let bufferPages = Math.max(0, Number(options.bufferPages ?? 1) || 0);
    let enabled = options.enabled !== false;
    let lastPlan = emptyPlan();

    function plan(pages, viewport = {}) {
        const normalizedPages = normalizePages(pages, options);
        if (!enabled || normalizedPages.length === 0) {
            lastPlan = fullPlan(normalizedPages);
            return lastPlan;
        }

        const visibleRange = visibleIndexes(normalizedPages, viewport, bufferPages, options);
        const visibleSet = new Set(visibleRange);
        const visiblePages = normalizedPages.filter(page => visibleSet.has(page.index));
        const firstVisible = visiblePages[0] || normalizedPages[0];
        const lastVisible = visiblePages[visiblePages.length - 1] || firstVisible;
        const totalHeight = totalDocumentHeight(normalizedPages, options);
        const topSpacerHeight = Math.max(0, firstVisible.top - DEFAULT_PADDING_BLOCK);
        const bottomSpacerHeight = Math.max(0, totalHeight - lastVisible.bottom - DEFAULT_PADDING_BLOCK);

        lastPlan = {
            enabled: true,
            pageCount: normalizedPages.length,
            visiblePageIndexes: visiblePages.map(page => page.index),
            pages: visiblePages.map(page => page.source),
            topSpacerHeight,
            bottomSpacerHeight,
            totalHeight,
            bufferPages,
            progressive: visiblePages.length < normalizedPages.length,
        };
        return lastPlan;
    }

    function setBufferPages(value) {
        bufferPages = Math.max(0, Number(value) || 0);
        return bufferPages;
    }

    function setEnabled(value) {
        enabled = value !== false;
        return enabled;
    }

    function snapshot() {
        return { ...lastPlan, visiblePageIndexes: [...lastPlan.visiblePageIndexes] };
    }

    return {
        plan,
        setBufferPages,
        setEnabled,
        snapshot,
    };
}

export function visibleIndexes(pages, viewport = {}, bufferPages = 1, options = {}) {
    const normalized = normalizePages(pages, options);
    if (normalized.length === 0) {
        return [];
    }

    const height = Math.max(1, Number(viewport.height || viewport.viewportHeight || options.viewportHeight || 900) || 900);
    const scrollTop = Math.max(0, Number(viewport.scrollTop || viewport.viewportTop || 0) || 0);
    const averageHeight = averagePageStride(normalized, options);
    const top = Math.max(0, scrollTop - Math.max(0, bufferPages) * averageHeight);
    const bottom = scrollTop + height + Math.max(0, bufferPages) * averageHeight;
    const indexes = normalized
        .filter(page => page.bottom >= top && page.top <= bottom)
        .map(page => page.index);

    return indexes.length > 0 ? indexes : [normalized[0].index];
}

function normalizePages(pages, options = {}) {
    const gap = Math.max(0, Number(options.pageGap ?? DEFAULT_PAGE_GAP) || DEFAULT_PAGE_GAP);
    const padding = Math.max(0, Number(options.paddingBlock ?? DEFAULT_PADDING_BLOCK) || DEFAULT_PADDING_BLOCK);
    let cursor = padding;
    return (Array.isArray(pages) ? pages : []).map((page, ordinal) => {
        const height = Math.max(1, Number(page?.height || options.pageHeight || 1123) || 1123);
        const top = Number.isFinite(Number(page?.y)) ? Number(page.y) + padding : cursor;
        const normalized = {
            index: Number(page?.index ?? ordinal) || 0,
            top,
            bottom: top + height,
            height,
            source: page,
        };
        cursor = normalized.bottom + gap;
        return normalized;
    });
}

function fullPlan(pages) {
    return {
        enabled: false,
        pageCount: pages.length,
        visiblePageIndexes: pages.map(page => page.index),
        pages: pages.map(page => page.source),
        topSpacerHeight: 0,
        bottomSpacerHeight: 0,
        totalHeight: totalDocumentHeight(pages),
        bufferPages: 0,
        progressive: false,
    };
}

function emptyPlan() {
    return fullPlan([]);
}

function averagePageStride(pages, options = {}) {
    if (pages.length === 0) {
        return Math.max(1, Number(options.pageHeight || 1123) || 1123);
    }

    const gap = Math.max(0, Number(options.pageGap ?? DEFAULT_PAGE_GAP) || DEFAULT_PAGE_GAP);
    return pages.reduce((total, page) => total + page.height + gap, 0) / pages.length;
}

function totalDocumentHeight(pages, options = {}) {
    if (pages.length === 0) {
        return 0;
    }

    const padding = Math.max(0, Number(options.paddingBlock ?? DEFAULT_PADDING_BLOCK) || DEFAULT_PADDING_BLOCK);
    return pages[pages.length - 1].bottom + padding;
}
