export function createCanvasSearchOverlay(options = {}) {
    const doc = options.document || globalThis.document;
    const canvasStack = options.canvasStack;

    function update(searchState = {}, render = null) {
        clear();
        const matches = Array.isArray(searchState?.matches) ? searchState.matches : [];
        if (matches.length === 0 || !canvasStack?.pages) {
            return snapshot();
        }

        const activeIndex = Math.max(0, Number(searchState.activeIndex || 0) || 0);
        const textRects = Array.isArray(render?.selectionLayout?.textRects)
            ? render.selectionLayout.textRects
            : Array.isArray(render?.displayList?.layout?.textRects)
                ? render.displayList.layout.textRects
                : [];
        let rectCount = 0;
        for (const match of matches) {
            const fragments = rectsForMatch(textRects, match);
            for (const fragment of fragments) {
                appendHighlight(fragment, match.index === activeIndex);
                rectCount += 1;
            }
        }

        canvasStack.root?.setAttribute?.('data-canvas-search-match-count', String(matches.length));
        canvasStack.root?.setAttribute?.('data-canvas-search-highlight-count', String(rectCount));
        canvasStack.root?.setAttribute?.('data-canvas-search-active-index', String(activeIndex));
        return snapshot();
    }

    function clear() {
        if (!canvasStack?.pages) {
            return;
        }

        for (const page of canvasStack.pages.values()) {
            const layer = findLayer(page.pageElement);
            if (layer) {
                layer.replaceChildren?.();
                while (layer.children?.length) {
                    layer.children[0].remove?.();
                }
            }
        }

        canvasStack.root?.setAttribute?.('data-canvas-search-match-count', '0');
        canvasStack.root?.setAttribute?.('data-canvas-search-highlight-count', '0');
    }

    function appendHighlight(fragment, active) {
        const page = canvasStack.pages.get(String(fragment.pageIndex));
        if (!page?.pageElement) {
            return;
        }

        const layer = ensureLayer(page.pageElement);
        const element = doc.createElement('div');
        element.className = active
            ? 'tm-document-canvas-search-highlight tm-document-canvas-search-highlight--active'
            : 'tm-document-canvas-search-highlight';
        element.setAttribute('data-testid', active ? 'document-canvas-search-active' : 'document-canvas-search-match');
        element.setAttribute('data-canvas-search-highlight', active ? 'active' : 'match');
        element.style.position = 'absolute';
        element.style.left = `${fragment.x}px`;
        element.style.top = `${fragment.y}px`;
        element.style.width = `${Math.max(1, fragment.width)}px`;
        element.style.height = `${Math.max(1, fragment.height)}px`;
        element.style.borderRadius = '3px';
        element.style.background = active
            ? 'color-mix(in srgb, var(--tm-color-warning, #f59e0b) 48%, transparent)'
            : 'color-mix(in srgb, var(--tm-color-warning, #f59e0b) 28%, transparent)';
        element.style.outline = active ? '2px solid var(--tm-color-primary, #2563eb)' : '0';
        layer.appendChild(element);
    }

    function ensureLayer(pageElement) {
        let layer = findLayer(pageElement);
        if (!layer) {
            layer = doc.createElement('div');
            layer.setAttribute('data-testid', 'document-canvas-search-layer');
            layer.style.position = 'absolute';
            layer.style.inset = '0';
            layer.style.pointerEvents = 'none';
            layer.style.zIndex = '8';
            pageElement.appendChild(layer);
        }

        return layer;
    }

    function snapshot() {
        return {
            highlightCount: Number(canvasStack?.root?.getAttribute?.('data-canvas-search-highlight-count') || 0) || 0,
            matchCount: Number(canvasStack?.root?.getAttribute?.('data-canvas-search-match-count') || 0) || 0,
        };
    }

    return { update, clear, destroy: clear, snapshot };
}

function rectsForMatch(textRects, match) {
    const start = Number(match.start || 0) || 0;
    const end = Number(match.end || 0) || 0;
    const blockId = String(match.blockId || '');
    const fragments = [];
    for (const rect of textRects) {
        if (String(rect.blockId || '') !== blockId) {
            continue;
        }

        const rectStart = Number(rect.start || 0) || 0;
        const rectEnd = Number(rect.end || rectStart) || rectStart;
        const overlapStart = Math.max(start, rectStart);
        const overlapEnd = Math.min(end, rectEnd);
        if (overlapEnd <= overlapStart || rectEnd <= rectStart) {
            continue;
        }

        const ratioStart = (overlapStart - rectStart) / (rectEnd - rectStart);
        const ratioEnd = (overlapEnd - rectStart) / (rectEnd - rectStart);
        const x = Number(rect.x || 0) + Number(rect.width || 0) * ratioStart;
        const width = Number(rect.width || 0) * Math.max(0.01, ratioEnd - ratioStart);
        fragments.push({
            pageIndex: Number(rect.pageIndex || 0) || 0,
            x,
            y: Number(rect.y || 0) || 0,
            width,
            height: Number(rect.height || 0) || 0,
        });
    }

    return fragments;
}

function findLayer(pageElement) {
    return pageElement?.querySelector?.('[data-testid="document-canvas-search-layer"]') || null;
}
