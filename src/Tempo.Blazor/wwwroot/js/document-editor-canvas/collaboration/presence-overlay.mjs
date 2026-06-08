import { asArray, asText, sortObject } from '../../document-editor/core/helpers.mjs';
import { caretRectForPosition } from '../selection/selection-controller.mjs';

export function createCanvasPresenceOverlay(options = {}) {
    const doc = options.document || globalThis.document;
    const root = doc.createElement('div');
    root.className = 'tm-document-canvas-presence';
    root.setAttribute('data-testid', 'document-canvas-presence-overlay');
    root.setAttribute('aria-hidden', 'true');
    const cursorsBySession = new Map();
    let lastSnapshot = [];

    function mount(host) {
        if (host && root.parentNode !== host) {
            host.appendChild(root);
        }

        return api;
    }

    function update(cursors = [], render = {}, model = {}) {
        const selectionLayout = render?.selectionLayout || render?.displayList?.layout || {};
        const pages = render?.displayList?.pages || render?.layout?.pages || [];
        const nextKeys = new Set();
        lastSnapshot = [];

        for (const cursor of asArray(cursors)) {
            const sessionId = asText(cursor.sessionId || cursor.clientId);
            const blockId = asText(cursor.blockId);
            const offset = cursor.offset == null ? null : Number(cursor.offset);
            if (!sessionId || !blockId || offset == null || !Number.isFinite(offset) || offset < 0) {
                continue;
            }

            const caret = caretRectForPosition(selectionLayout, { blockId, offset });
            const pageElement = pageElementForIndex(root, caret?.pageIndex);
            if (!caret?.rect || !pageElement) {
                continue;
            }

            const color = normalizedColor(cursor.color, sessionId);
            const item = ensureCursor(sessionId);
            const scale = Number(pageElement.getAttribute?.('data-canvas-page-zoom-scale') || 1) || 1;
            item.style.setProperty('--tm-document-canvas-presence-color', color);
            item.style.left = `${pageElement.offsetLeft + Number(caret.rect.x || 0) * scale}px`;
            item.style.top = `${pageElement.offsetTop + Number(caret.rect.y || 0) * scale}px`;
            item.style.height = `${Math.max(12, Number(caret.rect.height || 16) * scale)}px`;
            item.dataset.sessionId = sessionId;
            item.dataset.blockId = blockId;
            item.dataset.offset = String(offset);

            const label = item.querySelector?.('.tm-document-canvas-presence__label');
            if (label) {
                label.textContent = asText(cursor.displayName || cursor.clientId || sessionId);
            }

            nextKeys.add(sessionId);
            lastSnapshot.push(sortObject({
                sessionId,
                blockId,
                offset,
                pageIndex: caret.pageIndex,
                x: Number(caret.rect.x || 0) * scale,
                y: Number(caret.rect.y || 0) * scale,
                color,
            }));
        }

        for (const [sessionId, element] of cursorsBySession) {
            if (!nextKeys.has(sessionId)) {
                element.remove?.();
                cursorsBySession.delete(sessionId);
            }
        }

        root.setAttribute('data-canvas-presence-count', String(cursorsBySession.size));
        return snapshot();
    }

    function ensureCursor(sessionId) {
        if (cursorsBySession.has(sessionId)) {
            return cursorsBySession.get(sessionId);
        }

        const element = doc.createElement('div');
        element.className = 'tm-document-canvas-presence__cursor';
        element.setAttribute('data-testid', 'document-canvas-remote-caret');
        element.style.position = 'absolute';
        element.style.width = '2px';
        element.style.minHeight = '14px';
        const label = doc.createElement('span');
        label.className = 'tm-document-canvas-presence__label';
        label.style.position = 'absolute';
        label.style.bottom = 'calc(100% + var(--tm-space-1, 4px))';
        label.style.left = '0';
        label.style.maxWidth = '14rem';
        label.style.whiteSpace = 'nowrap';
        element.appendChild(label);
        root.appendChild(element);
        cursorsBySession.set(sessionId, element);
        return element;
    }

    function snapshot() {
        return sortObject({
            cursorCount: cursorsBySession.size,
            cursors: lastSnapshot.slice(),
        });
    }

    function destroy() {
        root.remove?.();
        cursorsBySession.clear();
        lastSnapshot = [];
    }

    const api = Object.freeze({
        root,
        mount,
        update,
        snapshot,
        destroy,
    });
    return api;
}

function pageElementForIndex(root, pageIndex) {
    const index = String(Number(pageIndex || 0) || 0);
    return root.parentNode?.querySelector?.(`[data-testid="document-canvas-page"][data-page-index="${index}"]`) || null;
}

function normalizedColor(color, key) {
    const raw = asText(color).trim();
    if (/^#[0-9a-f]{3}([0-9a-f]{3})?$/i.test(raw)
        || /^rgb(a)?\(/i.test(raw)
        || /^hsl(a)?\(/i.test(raw)) {
        return raw;
    }

    const palette = [
        'var(--tm-color-primary)',
        'var(--tm-color-success)',
        'var(--tm-color-warning)',
        'var(--tm-color-danger)',
        'var(--tm-color-info)',
    ];
    let hash = 0;
    for (const char of asText(key)) {
        hash = ((hash << 5) - hash + char.charCodeAt(0)) | 0;
    }

    return palette[Math.abs(hash) % palette.length];
}
