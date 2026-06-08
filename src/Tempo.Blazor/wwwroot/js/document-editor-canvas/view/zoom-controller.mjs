const MIN_ZOOM_SCALE = 0.25;
const MAX_ZOOM_SCALE = 4;
const DEFAULT_ZOOM_SCALE = 1;
const DEFAULT_STEP = 0.1;

export const ZOOM_PRESETS = Object.freeze({
    CUSTOM: 'custom',
    FIT_PAGE: 'fitPage',
    FIT_WIDTH: 'fitWidth',
    MULTIPLE_PAGES: 'multiplePages',
});

export function normalizeZoomState(value = {}) {
    const preset = normalizeZoomPreset(value.preset || value.zoomPreset || ZOOM_PRESETS.CUSTOM);
    const scale = clampZoomScale(value.scale ?? percentToScale(value.percent ?? value.zoomPercent ?? 100));
    return {
        preset,
        scale,
        percent: scaleToPercent(scale),
    };
}

export function applyZoomCommand(currentState = {}, commandId, argument = null, metrics = {}) {
    const normalized = normalizeZoomCommand(commandId);
    const current = normalizeZoomState(currentState);
    const viewportMetrics = normalizeZoomMetrics(metrics);
    let next;

    switch (normalized) {
        case 'setzoom':
        case 'customzoom':
            next = {
                preset: ZOOM_PRESETS.CUSTOM,
                scale: clampZoomScale(argumentScale(argument)),
            };
            break;
        case 'zoomin':
            next = {
                preset: ZOOM_PRESETS.CUSTOM,
                scale: clampZoomScale(current.scale + stepFromArgument(argument)),
            };
            break;
        case 'zoomout':
            next = {
                preset: ZOOM_PRESETS.CUSTOM,
                scale: clampZoomScale(current.scale - stepFromArgument(argument)),
            };
            break;
        case 'fitpage':
            next = {
                preset: ZOOM_PRESETS.FIT_PAGE,
                scale: computePresetScale(ZOOM_PRESETS.FIT_PAGE, viewportMetrics),
            };
            break;
        case 'fitwidth':
        case 'zoompagewidth':
            next = {
                preset: ZOOM_PRESETS.FIT_WIDTH,
                scale: computePresetScale(ZOOM_PRESETS.FIT_WIDTH, viewportMetrics),
            };
            break;
        case 'multiplepages':
        case 'twopages':
            next = {
                preset: ZOOM_PRESETS.MULTIPLE_PAGES,
                scale: computePresetScale(ZOOM_PRESETS.MULTIPLE_PAGES, viewportMetrics),
            };
            break;
        case 'ctrlwheelzoom':
        case 'pinchzoom':
            next = {
                preset: ZOOM_PRESETS.CUSTOM,
                scale: clampZoomScale(current.scale * zoomFactorFromGesture(argument, normalized)),
            };
            break;
        default:
            return {
                handled: false,
                changed: false,
                state: current,
            };
    }

    next.scale = snapZoomScale(next.scale);
    next.percent = scaleToPercent(next.scale);
    const changed = next.preset !== current.preset || Math.abs(next.scale - current.scale) > 0.0001;
    return {
        handled: true,
        changed,
        viewChanged: changed,
        state: next,
        zoom: next,
    };
}

export function isZoomCommand(commandId) {
    return [
        'setzoom',
        'customzoom',
        'zoomin',
        'zoomout',
        'fitpage',
        'fitwidth',
        'zoompagewidth',
        'multiplepages',
        'twopages',
        'ctrlwheelzoom',
        'pinchzoom',
    ].includes(normalizeZoomCommand(commandId));
}

export function computePresetScale(preset, metrics = {}) {
    const normalized = normalizeZoomMetrics(metrics);
    const availableWidth = Math.max(1, normalized.viewportWidth - normalized.paddingInline);
    const availableHeight = Math.max(1, normalized.viewportHeight - normalized.paddingBlock);

    if (preset === ZOOM_PRESETS.FIT_WIDTH) {
        return snapZoomScale(clampZoomScale(availableWidth / normalized.pageWidth));
    }

    if (preset === ZOOM_PRESETS.FIT_PAGE) {
        return snapZoomScale(clampZoomScale(Math.min(
            availableWidth / normalized.pageWidth,
            availableHeight / normalized.pageHeight,
        )));
    }

    if (preset === ZOOM_PRESETS.MULTIPLE_PAGES) {
        const twoPageWidth = normalized.pageWidth * 2 + normalized.pageGap;
        return snapZoomScale(clampZoomScale(Math.min(
            availableWidth / twoPageWidth,
            availableHeight / normalized.pageHeight,
        )));
    }

    return DEFAULT_ZOOM_SCALE;
}

export function zoomedLength(value, zoomState = {}) {
    const scale = normalizeZoomState(zoomState).scale;
    return Math.max(0, (Number(value) || 0) * scale);
}

export function scaleToPercent(scale) {
    return Math.round(clampZoomScale(scale) * 100);
}

export function percentToScale(percent) {
    return clampZoomScale((Number(percent) || 100) / 100);
}

export function snapZoomScale(scale) {
    return Math.round(clampZoomScale(scale) * 1000) / 1000;
}

export function normalizeZoomPreset(value) {
    const normalized = String(value || '').replace(/[\s_-]/g, '').toLowerCase();
    if (normalized === 'fitpage' || normalized === 'page') {
        return ZOOM_PRESETS.FIT_PAGE;
    }

    if (normalized === 'fitwidth' || normalized === 'pagewidth' || normalized === 'width') {
        return ZOOM_PRESETS.FIT_WIDTH;
    }

    if (normalized === 'multiplepages' || normalized === 'twopages' || normalized === 'multipage') {
        return ZOOM_PRESETS.MULTIPLE_PAGES;
    }

    return ZOOM_PRESETS.CUSTOM;
}

export function normalizeZoomMetrics(metrics = {}) {
    const page = Array.isArray(metrics?.pages) && metrics.pages.length > 0 ? metrics.pages[0] : null;
    return {
        pageWidth: Math.max(1, Number(metrics.pageWidth ?? page?.width ?? 794) || 794),
        pageHeight: Math.max(1, Number(metrics.pageHeight ?? page?.height ?? 1123) || 1123),
        viewportWidth: Math.max(1, Number(metrics.viewportWidth ?? globalThis.innerWidth ?? 1280) || 1280),
        viewportHeight: Math.max(1, Number(metrics.viewportHeight ?? globalThis.innerHeight ?? 900) || 900),
        pageGap: Math.max(0, Number(metrics.pageGap ?? 24) || 0),
        paddingInline: Math.max(0, Number(metrics.paddingInline ?? 48) || 0),
        paddingBlock: Math.max(0, Number(metrics.paddingBlock ?? 48) || 0),
    };
}

function normalizeZoomCommand(commandId) {
    return String(commandId || '').replace(/[\s_-]/g, '').toLowerCase();
}

function argumentScale(argument) {
    if (typeof argument === 'number') {
        return percentToScale(argument);
    }

    return clampZoomScale(argument?.scale ?? percentToScale(argument?.percent ?? argument?.zoomPercent ?? 100));
}

function stepFromArgument(argument) {
    if (typeof argument === 'number') {
        return Math.max(0.01, Math.min(1, argument));
    }

    return Math.max(0.01, Math.min(1, Number(argument?.step || DEFAULT_STEP) || DEFAULT_STEP));
}

function zoomFactorFromGesture(argument, commandId) {
    const explicitScale = Number(argument?.scaleDelta ?? argument?.factor ?? 0);
    if (explicitScale > 0) {
        return Math.max(0.1, Math.min(10, explicitScale));
    }

    const delta = Number(argument?.deltaY ?? argument?.wheelDelta ?? 0) || 0;
    if (delta === 0 && commandId === 'pinchzoom') {
        return Math.max(0.1, Math.min(10, Number(argument?.pinchScale || 1) || 1));
    }

    return delta < 0 ? 1.1 : 0.9;
}

function clampZoomScale(scale) {
    return Math.max(MIN_ZOOM_SCALE, Math.min(MAX_ZOOM_SCALE, Number(scale) || DEFAULT_ZOOM_SCALE));
}
