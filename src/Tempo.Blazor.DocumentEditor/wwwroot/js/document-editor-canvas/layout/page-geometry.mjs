export const DEFAULT_PAGE_SETUP = Object.freeze({
    width: 794,
    height: 1123,
    marginTop: 72,
    marginRight: 72,
    marginBottom: 72,
    marginLeft: 72,
    headerDistanceFromTop: 48,
    footerDistanceFromBottom: 48,
    sizeName: 'A4',
    landscape: false,
});

export function createLayoutService(options = {}) {
    const pageSurfaceStrategy = 'canvas-per-visible-page';
    const defaultPageSettings = normalizePageSettings(options.pageSettings || DEFAULT_PAGE_SETUP);

    function layout(model, viewport = null) {
        const pageSettings = normalizePageSettings(model?.pageSettings || defaultPageSettings);
        const pages = [
            createPageLayout(0, pageSettings),
        ];
        const visiblePageIndices = computeVisiblePageIndices(pages, viewport);

        return {
            pageSurfaceStrategy,
            pageSettings,
            pages,
            visiblePageIndices,
        };
    }

    return {
        layout,
        pageSurfaceStrategy,
    };
}

export function normalizePageSettings(input) {
    const source = input && typeof input === 'object' ? input : {};
    return {
        width: positiveNumber(source.width, DEFAULT_PAGE_SETUP.width),
        height: positiveNumber(source.height, DEFAULT_PAGE_SETUP.height),
        marginTop: nonNegativeNumber(source.marginTop, DEFAULT_PAGE_SETUP.marginTop),
        marginRight: nonNegativeNumber(source.marginRight, DEFAULT_PAGE_SETUP.marginRight),
        marginBottom: nonNegativeNumber(source.marginBottom, DEFAULT_PAGE_SETUP.marginBottom),
        marginLeft: nonNegativeNumber(source.marginLeft, DEFAULT_PAGE_SETUP.marginLeft),
        headerDistanceFromTop: nonNegativeNumber(source.headerDistanceFromTop, DEFAULT_PAGE_SETUP.headerDistanceFromTop),
        footerDistanceFromBottom: nonNegativeNumber(source.footerDistanceFromBottom, DEFAULT_PAGE_SETUP.footerDistanceFromBottom),
        sizeName: typeof source.sizeName === 'string' && source.sizeName.trim() ? source.sizeName.trim() : DEFAULT_PAGE_SETUP.sizeName,
        landscape: source.landscape === true,
    };
}

function createPageLayout(index, pageSettings) {
    return {
        index,
        x: 0,
        y: 0,
        width: pageSettings.width,
        height: pageSettings.height,
        body: {
            x: pageSettings.marginLeft,
            y: pageSettings.marginTop,
            width: Math.max(1, pageSettings.width - pageSettings.marginLeft - pageSettings.marginRight),
            height: Math.max(1, pageSettings.height - pageSettings.marginTop - pageSettings.marginBottom),
        },
        header: {
            x: pageSettings.marginLeft,
            y: Math.max(8, pageSettings.headerDistanceFromTop - 18),
            width: Math.max(1, pageSettings.width - pageSettings.marginLeft - pageSettings.marginRight),
            height: Math.max(24, pageSettings.marginTop - Math.max(8, pageSettings.headerDistanceFromTop - 18) - 8),
        },
        footer: {
            x: pageSettings.marginLeft,
            y: Math.min(
                pageSettings.height - 32,
                pageSettings.height - pageSettings.footerDistanceFromBottom - 18),
            width: Math.max(1, pageSettings.width - pageSettings.marginLeft - pageSettings.marginRight),
            height: Math.max(24, pageSettings.footerDistanceFromBottom + 10),
        },
    };
}

function computeVisiblePageIndices(pages, viewport) {
    if (!viewport) {
        return pages.map(page => page.index);
    }

    const scrollTop = Number(viewport.scrollTop || 0);
    const height = Number(viewport.height || 0);
    const overscanPages = Number(viewport.overscanPages || 0);
    const top = Math.max(0, scrollTop - overscanPages * DEFAULT_PAGE_SETUP.height);
    const bottom = scrollTop + Math.max(1, height) + overscanPages * DEFAULT_PAGE_SETUP.height;
    return pages
        .filter(page => page.y + page.height >= top && page.y <= bottom)
        .map(page => page.index);
}

function positiveNumber(value, fallback) {
    const parsed = Number(value);
    return Number.isFinite(parsed) && parsed > 0 ? parsed : fallback;
}

function nonNegativeNumber(value, fallback) {
    const parsed = Number(value);
    return Number.isFinite(parsed) && parsed >= 0 ? parsed : fallback;
}
