/**
 * Tempo Blazor — PDF Viewer JS Interop
 *
 * Uses PDF.js v5 (ES modules) via dynamic import.
 * Requires pdf.min.mjs + pdf.worker.min.mjs in the same directory as this file.
 */
window.tmPdfViewer = (function () {
    'use strict';

    // Capture script directory while document.currentScript is still set.
    const _scriptDir = (() => {
        const src = document.currentScript?.src ?? '';
        return src ? src.substring(0, src.lastIndexOf('/') + 1) : '_content/Tempo.Blazor/js/';
    })();

    // canvasEl → { pdfDoc, currentPage, scale, rotation, dotNetRef }
    const _docs = new WeakMap();
    let _lib = null;

    async function _ensureLib() {
        if (_lib) return _lib;
        const mod = await import(_scriptDir + 'pdf.min.mjs');
        mod.GlobalWorkerOptions.workerSrc = _scriptDir + 'pdf.worker.min.mjs';
        _lib = mod;
        return _lib;
    }

    function isAvailable() {
        return true;
    }

    async function init(canvasEl, url, dotNetRef) {
        if (!canvasEl || !url) return;
        destroy(canvasEl);
        try {
            const pdfjs = await _ensureLib();
            const pdfDoc = await pdfjs.getDocument(url).promise;
            _docs.set(canvasEl, { pdfDoc, currentPage: 1, scale: 1.0, rotation: 0, dotNetRef });
            await renderPage(canvasEl, 1, 1.0, 0);
            dotNetRef.invokeMethodAsync('OnPdfLoaded', pdfDoc.numPages).catch(console.error);
        } catch (err) {
            dotNetRef.invokeMethodAsync('OnPdfLoadError', String(err?.message ?? err))
                     .catch(console.error);
        }
    }

    async function renderPage(canvasEl, pageNum, scale, rotation) {
        const state = _docs.get(canvasEl);
        if (!state) return;
        state.currentPage = pageNum;
        state.scale = scale;
        if (rotation !== undefined) state.rotation = rotation;
        try {
            const page = await state.pdfDoc.getPage(pageNum);
            const viewport = page.getViewport({ scale, rotation: state.rotation });
            canvasEl.width = viewport.width;
            canvasEl.height = viewport.height;
            await page.render({ canvasContext: canvasEl.getContext('2d'), viewport }).promise;
        } catch (err) {
            console.error('tmPdfViewer.renderPage', err);
        }
    }

    function getTotalPages(canvasEl) {
        return _docs.get(canvasEl)?.pdfDoc?.numPages ?? 0;
    }

    async function setScale(canvasEl, scale) {
        const state = _docs.get(canvasEl);
        if (!state) return;
        await renderPage(canvasEl, state.currentPage, scale, state.rotation);
    }

    async function setRotation(canvasEl, rotation) {
        const state = _docs.get(canvasEl);
        if (!state) return;
        await renderPage(canvasEl, state.currentPage, state.scale, rotation);
    }

    async function renderTextLayer(canvasEl, textLayerEl, pageNum, scale, rotation) {
        const state = _docs.get(canvasEl);
        if (!state || !textLayerEl) return;
        try {
            const pdfjs = await _ensureLib();
            const page = await state.pdfDoc.getPage(pageNum);
            const viewport = page.getViewport({ scale, rotation: rotation ?? state.rotation });
            const textContent = await page.getTextContent();

            textLayerEl.innerHTML = '';
            textLayerEl.style.width = viewport.width + 'px';
            textLayerEl.style.height = viewport.height + 'px';

            for (const item of textContent.items) {
                const tx = pdfjs.Util.transform(viewport.transform, item.transform);
                const fontHeight = Math.hypot(tx[0], tx[1]);
                const fontWidth = Math.hypot(tx[2], tx[3]);
                const div = document.createElement('div');
                div.textContent = item.str;
                div.style.position = 'absolute';
                div.style.left = tx[4] + 'px';
                div.style.top = tx[5] + 'px';
                div.style.fontSize = fontHeight + 'px';
                div.style.fontFamily = item.fontName;
                div.style.transform = `scaleX(${fontWidth > 0 ? fontHeight / fontWidth : 1})`;
                div.style.transformOrigin = '0 0';
                div.style.whiteSpace = 'pre';
                div.style.userSelect = 'text';
                div.style.cursor = 'text';
                div.style.opacity = '0.2';
                textLayerEl.appendChild(div);
            }
        } catch (err) {
            console.error('tmPdfViewer.renderTextLayer', err);
        }
    }

    async function renderThumbnails(containerEl, url, scale, dotNetRef) {
        const pdfjs = await _ensureLib();
        const pdfDoc = await pdfjs.getDocument(url).promise;
        containerEl.innerHTML = '';
        for (let i = 1; i <= pdfDoc.numPages; i++) {
            const thumbWrap = document.createElement('div');
            thumbWrap.className = 'tm-pdf-thumbnail';
            thumbWrap.dataset.page = String(i);
            thumbWrap.style.cursor = 'pointer';
            thumbWrap.style.marginBottom = '8px';
            thumbWrap.style.padding = '4px';
            thumbWrap.style.borderRadius = '4px';
            thumbWrap.style.transition = 'background .15s';

            const thumbCanvas = document.createElement('canvas');
            thumbCanvas.style.display = 'block';
            thumbCanvas.style.width = '100%';
            thumbCanvas.style.borderRadius = '2px';

            const pageNumLabel = document.createElement('div');
            pageNumLabel.textContent = String(i);
            pageNumLabel.style.textAlign = 'center';
            pageNumLabel.style.fontSize = '11px';
            pageNumLabel.style.marginTop = '2px';
            pageNumLabel.style.color = 'var(--tm-text-muted, #888)';

            thumbWrap.appendChild(thumbCanvas);
            thumbWrap.appendChild(pageNumLabel);
            containerEl.appendChild(thumbWrap);

            try {
                const page = await pdfDoc.getPage(i);
                const thumbScale = scale ?? 0.3;
                const viewport = page.getViewport({ scale: thumbScale });
                thumbCanvas.width = viewport.width;
                thumbCanvas.height = viewport.height;
                await page.render({ canvasContext: thumbCanvas.getContext('2d'), viewport }).promise;
            } catch (err) {
                console.error('tmPdfViewer.renderThumbnails page', i, err);
            }

            thumbWrap.addEventListener('click', () => {
                dotNetRef.invokeMethodAsync('OnThumbnailClicked', i).catch(console.error);
            });
        }
    }

    function highlightThumbnail(containerEl, pageNum) {
        if (!containerEl) return;
        containerEl.querySelectorAll('.tm-pdf-thumbnail').forEach(el => {
            const isActive = parseInt(el.dataset.page, 10) === pageNum;
            el.classList.toggle('tm-pdf-thumbnail--active', isActive);
            el.style.background = isActive ? 'var(--tm-primary-100, #e0f2fe)' : 'transparent';
        });
    }

    // ── Text selection ──────────────────────────────────────────────────────

    function enableSelection(canvasEl, textLayerEl, dotNetRef) {
        if (!canvasEl || !textLayerEl) return;
        const state = _docs.get(canvasEl);
        if (state) state.dotNetRef = dotNetRef;
        if (textLayerEl.dataset.tmSelectionBound === '1') return;
        textLayerEl.dataset.tmSelectionBound = '1';

        document.addEventListener('mouseup', () => {
            // Always read the live state so a later document (Url) change — which replaces
            // the dotNet reference without rebinding this listener — is honoured.
            const doc = _docs.get(canvasEl);
            if (!doc || !doc.dotNetRef) return;

            const sel = window.getSelection();
            if (!sel || sel.rangeCount === 0 || sel.isCollapsed) return;
            const anchor = sel.anchorNode;
            if (!anchor || !textLayerEl.contains(anchor)) return;

            const canvasRect = canvasEl.getBoundingClientRect();
            if (canvasRect.width < 1 || canvasRect.height < 1) return;

            const flat = [];
            const range = sel.getRangeAt(0);
            for (const r of range.getClientRects()) {
                if (r.width < 1 || r.height < 1) continue;
                const x = _clamp01((r.left - canvasRect.left) / canvasRect.width);
                const y = _clamp01((r.top - canvasRect.top) / canvasRect.height);
                const w = _clamp01(r.width / canvasRect.width);
                const h = _clamp01(r.height / canvasRect.height);
                flat.push(x, y, w, h);
            }
            if (flat.length === 0) return;

            doc.dotNetRef.invokeMethodAsync('OnTextSelectionChanged', sel.toString(), doc.currentPage, flat).catch(() => { });
        });
    }

    // ── Overlay positioning ──────────────────────────────────────────────────

    // overlayEl → ResizeObserver keeping it aligned with its canvas.
    const _overlayObservers = new WeakMap();

    function syncOverlay(canvasEl, overlayEl) {
        if (!canvasEl || !overlayEl) return;
        const apply = () => {
            overlayEl.style.left = canvasEl.offsetLeft + 'px';
            overlayEl.style.top = canvasEl.offsetTop + 'px';
            overlayEl.style.width = canvasEl.offsetWidth + 'px';
            overlayEl.style.height = canvasEl.offsetHeight + 'px';
        };
        apply();
        // The canvas is resized asynchronously when a page renders (initial load, zoom,
        // rotation). Track it so overlays never go stale between explicit sync calls.
        if (typeof ResizeObserver !== 'undefined' && !_overlayObservers.has(overlayEl)) {
            const observer = new ResizeObserver(apply);
            observer.observe(canvasEl);
            _overlayObservers.set(overlayEl, observer);
        }
    }

    // ── Search ────────────────────────────────────────────────────────────────

    async function search(canvasEl, searchLayerEl, query, dotNetRef) {
        const state = _docs.get(canvasEl);
        if (!state) return;
        state.searchLayerEl = searchLayerEl;
        state.dotNetRef = dotNetRef ?? state.dotNetRef;

        if (!query) {
            clearSearch(canvasEl, searchLayerEl);
            dotNetRef.invokeMethodAsync('OnSearchResults', 0, []).catch(() => { });
            return;
        }

        state.searchQuery = query;
        const q = query.toLowerCase();
        const matches = [];
        const perPage = [];
        try {
            const pdfjs = await _ensureLib();
            for (let p = 1; p <= state.pdfDoc.numPages; p++) {
                const page = await state.pdfDoc.getPage(p);
                const viewport = page.getViewport({ scale: 1 });
                const pw = viewport.width;
                const ph = viewport.height;
                const textContent = await page.getTextContent();
                let count = 0;

                for (const item of textContent.items) {
                    const str = item.str;
                    if (!str) continue;
                    const lower = str.toLowerCase();
                    if (lower.indexOf(q) === -1) continue;

                    const tx = pdfjs.Util.transform(viewport.transform, item.transform);
                    const fontHeight = Math.hypot(tx[2], tx[3]) || item.height || 0;
                    const left = tx[4];
                    const top = tx[5] - fontHeight;
                    const widthPx = item.width || (Math.hypot(tx[0], tx[1]) * str.length);
                    const charWidth = widthPx / Math.max(str.length, 1);

                    let from = 0;
                    let idx;
                    while ((idx = lower.indexOf(q, from)) !== -1) {
                        matches.push({
                            page: p,
                            x: _clamp01((left + idx * charWidth) / pw),
                            y: _clamp01(top / ph),
                            w: _clamp01((q.length * charWidth) / pw),
                            h: _clamp01(fontHeight / ph)
                        });
                        count++;
                        from = idx + q.length;
                    }
                }
                perPage.push(count);
            }
        } catch (err) {
            console.error('tmPdfViewer.search', err);
        }

        state.searchMatches = matches;
        state.searchActive = matches.length ? 0 : -1;
        dotNetRef.invokeMethodAsync('OnSearchResults', matches.length, perPage).catch(() => { });

        if (matches.length) {
            await _activateMatch(canvasEl, searchLayerEl);
        } else {
            _drawSearchHighlights(canvasEl, searchLayerEl, state.currentPage);
        }
    }

    async function searchNext(canvasEl, searchLayerEl) {
        const state = _docs.get(canvasEl);
        if (!state || !state.searchMatches || !state.searchMatches.length) return;
        state.searchActive = (state.searchActive + 1) % state.searchMatches.length;
        await _activateMatch(canvasEl, searchLayerEl);
    }

    async function searchPrev(canvasEl, searchLayerEl) {
        const state = _docs.get(canvasEl);
        if (!state || !state.searchMatches || !state.searchMatches.length) return;
        const n = state.searchMatches.length;
        state.searchActive = (state.searchActive - 1 + n) % n;
        await _activateMatch(canvasEl, searchLayerEl);
    }

    function redrawSearch(canvasEl, searchLayerEl, pageNum) {
        _drawSearchHighlights(canvasEl, searchLayerEl, pageNum);
    }

    function clearSearch(canvasEl, searchLayerEl) {
        const state = _docs.get(canvasEl);
        if (state) {
            state.searchMatches = [];
            state.searchActive = -1;
            state.searchQuery = null;
        }
        if (searchLayerEl) searchLayerEl.innerHTML = '';
    }

    async function _activateMatch(canvasEl, searchLayerEl) {
        const state = _docs.get(canvasEl);
        if (!state || !state.searchMatches || state.searchActive < 0) return;
        const match = state.searchMatches[state.searchActive];
        if (match.page !== state.currentPage) {
            await renderPage(canvasEl, match.page, state.scale, state.rotation);
        }
        _drawSearchHighlights(canvasEl, searchLayerEl, match.page);
        _scrollToActive(searchLayerEl);
        if (state.dotNetRef) {
            state.dotNetRef.invokeMethodAsync('OnSearchActiveChanged', state.searchActive + 1, match.page).catch(() => { });
        }
    }

    function _drawSearchHighlights(canvasEl, searchLayerEl, pageNum) {
        if (!searchLayerEl) return;
        syncOverlay(canvasEl, searchLayerEl);
        searchLayerEl.innerHTML = '';
        const state = _docs.get(canvasEl);
        if (!state || !state.searchMatches) return;

        state.searchMatches.forEach((match, i) => {
            if (match.page !== pageNum) return;
            const div = document.createElement('div');
            const isActive = i === state.searchActive;
            div.className = 'tm-pdf-search-highlight' + (isActive ? ' tm-pdf-search-highlight--active' : '');
            div.style.left = (match.x * 100) + '%';
            div.style.top = (match.y * 100) + '%';
            div.style.width = (match.w * 100) + '%';
            div.style.height = (match.h * 100) + '%';
            if (isActive) div.dataset.active = '1';
            searchLayerEl.appendChild(div);
        });
    }

    function _scrollToActive(searchLayerEl) {
        if (!searchLayerEl) return;
        const active = searchLayerEl.querySelector('[data-active="1"]');
        if (active && typeof active.scrollIntoView === 'function') {
            active.scrollIntoView({ block: 'center', inline: 'center' });
        }
    }

    function _clamp01(value) {
        if (!Number.isFinite(value)) return 0;
        return Math.min(Math.max(value, 0), 1);
    }

    async function renderAllPages(containerEl, canvasEl, scale, rotation) {
        const state = _docs.get(canvasEl);
        if (!state) return;
        containerEl.innerHTML = '';
        let currentTop = 0;
        const pageCanvases = [];
        for (let i = 1; i <= state.pdfDoc.numPages; i++) {
            const wrap = document.createElement('div');
            wrap.style.marginBottom = '16px';
            wrap.style.display = 'flex';
            wrap.style.justifyContent = 'center';

            const pageCanvas = document.createElement('canvas');
            pageCanvas.style.display = 'block';
            pageCanvas.style.boxShadow = '0 1px 3px rgba(0,0,0,0.08)';
            pageCanvas.style.borderRadius = '4px';
            pageCanvas.dataset.page = String(i);

            wrap.appendChild(pageCanvas);
            containerEl.appendChild(wrap);
            pageCanvases.push(pageCanvas);

            try {
                const page = await state.pdfDoc.getPage(i);
                const viewport = page.getViewport({ scale: scale ?? 1.0, rotation: rotation ?? 0 });
                pageCanvas.width = viewport.width;
                pageCanvas.height = viewport.height;
                await page.render({ canvasContext: pageCanvas.getContext('2d'), viewport }).promise;
            } catch (err) {
                console.error('tmPdfViewer.renderAllPages page', i, err);
            }
        }
    }

    function destroy(canvasEl) {
        const state = _docs.get(canvasEl);
        if (!state) return;
        try { state.pdfDoc.destroy(); } catch { }
        _docs.delete(canvasEl);
    }

    return {
        isAvailable,
        init,
        renderPage,
        getTotalPages,
        setScale,
        setRotation,
        renderTextLayer,
        renderThumbnails,
        highlightThumbnail,
        enableSelection,
        syncOverlay,
        search,
        searchNext,
        searchPrev,
        redrawSearch,
        clearSearch,
        renderAllPages,
        destroy
    };
})();
