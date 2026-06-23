const handles = new Map();
let painterPromise;

export function normalizeViewerState(state = {}) {
    const pageCount = Math.max(1, Number(state.pageCount || 1) || 1);
    const pageNumber = clamp(Number(state.pageNumber || 1) || 1, 1, pageCount);
    const zoomMode = normalizeZoomMode(state.zoomMode);
    const zoomPercent = clamp(Number(state.zoomPercent || 100) || 100, 25, 400);
    return {
        pageCount,
        pageNumber,
        zoomMode,
        zoomPercent,
        pageWindow: resolvePageWindow(pageNumber, pageCount, state.overscan ?? 2),
    };
}

export function resolvePageWindow(pageNumber, pageCount, overscan = 2) {
    const total = Math.max(1, Number(pageCount || 1) || 1);
    const current = clamp(Number(pageNumber || 1) || 1, 1, total);
    const span = Math.max(0, Number(overscan || 0) || 0);
    return {
        start: Math.max(1, current - span),
        end: Math.min(total, current + span),
    };
}

export function resolveZoomScale(mode, percent, page, viewport) {
    const zoomMode = normalizeZoomMode(mode);
    const pageWidth = Math.max(1, Number(page?.width || page?.Width || 1) || 1);
    const pageHeight = Math.max(1, Number(page?.height || page?.Height || 1) || 1);
    const viewportWidth = Math.max(1, Number(viewport?.width || viewport?.clientWidth || pageWidth) || pageWidth);
    const viewportHeight = Math.max(1, Number(viewport?.height || viewport?.clientHeight || pageHeight) || pageHeight);

    if (zoomMode === 'FitWidth') {
        return clamp(viewportWidth / pageWidth, 0.1, 4);
    }

    if (zoomMode === 'FitPage') {
        return clamp(Math.min(viewportWidth / pageWidth, viewportHeight / pageHeight), 0.1, 4);
    }

    return clamp((Number(percent || 100) || 100) / 100, 0.25, 4);
}

export function createPageCache(limit = 6) {
    const maxEntries = Math.max(1, Number(limit || 1) || 1);
    const entries = new Map();
    return {
        get(key) {
            if (!entries.has(key)) {
                return undefined;
            }

            const value = entries.get(key);
            entries.delete(key);
            entries.set(key, value);
            return value;
        },
        set(key, value) {
            if (entries.has(key)) {
                entries.delete(key);
            }

            entries.set(key, value);
            while (entries.size > maxEntries) {
                entries.delete(entries.keys().next().value);
            }
        },
        has(key) {
            return entries.has(key);
        },
        keys() {
            return Array.from(entries.keys());
        },
        clear() {
            entries.clear();
        },
    };
}

export function mount(root, canvas) {
    const handle = `tm-report-viewer-${handles.size + 1}-${Date.now().toString(36)}`;
    handles.set(handle, {
        root,
        canvas,
        cache: createPageCache(8),
        snapshotId: '',
    });
    return handle;
}

export async function update(handle, snapshotJson, pageNumber = 1, zoomMode = 'Percent', zoomPercent = 100) {
    const state = handles.get(handle);
    if (!state) {
        return null;
    }

    const snapshot = typeof snapshotJson === 'string' ? JSON.parse(snapshotJson) : snapshotJson;
    const pages = Array.isArray(snapshot?.pages || snapshot?.Pages) ? (snapshot.pages || snapshot.Pages) : [];
    const viewerState = normalizeViewerState({ pageNumber, pageCount: pages.length, zoomMode, zoomPercent });
    const page = pages[viewerState.pageNumber - 1] || pages[0];
    if (!page || !state.canvas) {
        return null;
    }

    const snapshotId = snapshot.snapshotId || snapshot.SnapshotId || '';
    if (state.snapshotId !== snapshotId) {
        state.cache.clear();
        state.snapshotId = snapshotId;
    }

    const cacheKey = `${snapshotId}:${viewerState.pageNumber}`;
    const cached = state.cache.get(cacheKey);
    const singlePageSnapshot = cached?.snapshot || {
        schemaVersion: snapshot.schemaVersion || snapshot.SchemaVersion || 1,
        snapshotId,
        pages: [page],
    };
    state.cache.set(cacheKey, { snapshot: singlePageSnapshot });

    const painter = await getPainter();
    const summary = await painter.paintReportingSnapshot(state.canvas, singlePageSnapshot, {
        pixelRatio: globalThis.devicePixelRatio || 1,
    });
    const viewport = state.root?.querySelector?.('.tm-report-viewer__surface') || state.root || {};
    const scale = resolveZoomScale(viewerState.zoomMode, viewerState.zoomPercent, page, viewport);
    state.canvas.style.transformOrigin = 'top center';
    state.canvas.style.transform = `scale(${scale})`;
    state.canvas.style.marginBottom = `${Math.max(0, summary.height * (scale - 1))}px`;

    return {
        ...summary,
        pageNumber: viewerState.pageNumber,
        pageCount: viewerState.pageCount,
        zoomScale: scale,
        cachedPages: state.cache.keys(),
        virtualWindow: viewerState.pageWindow,
    };
}

export function dispose(handle) {
    handles.delete(handle);
}

export function downloadFile(fileName, contentType, base64) {
    const blob = base64ToBlob(base64, contentType);
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName || 'report.pdf';
    anchor.style.display = 'none';
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();
    URL.revokeObjectURL(url);
}

export function printPdf(contentType, base64) {
    const blob = base64ToBlob(base64, contentType || 'application/pdf');
    const url = URL.createObjectURL(blob);
    const frame = document.createElement('iframe');
    frame.style.position = 'fixed';
    frame.style.inlineSize = '0';
    frame.style.blockSize = '0';
    frame.style.border = '0';
    frame.src = url;
    frame.onload = () => {
        frame.contentWindow?.focus();
        frame.contentWindow?.print();
        setTimeout(() => {
            URL.revokeObjectURL(url);
            frame.remove();
        }, 1000);
    };
    document.body.appendChild(frame);
}

async function getPainter() {
    if (!painterPromise) {
        const url = new URL('/_content/Tempo.Blazor/js/reporting/reporting-painter.bundle.js', document.baseURI).href;
        painterPromise = import(url);
    }

    return painterPromise;
}

function normalizeZoomMode(mode) {
    return mode === 'FitWidth' || mode === 'FitPage' ? mode : 'Percent';
}

function clamp(value, min, max) {
    return Math.min(max, Math.max(min, value));
}

function base64ToBlob(base64, contentType = 'application/octet-stream') {
    const binary = atob(base64 || '');
    const bytes = new Uint8Array(binary.length);
    for (let index = 0; index < binary.length; index++) {
        bytes[index] = binary.charCodeAt(index);
    }

    return new Blob([bytes], { type: contentType });
}
