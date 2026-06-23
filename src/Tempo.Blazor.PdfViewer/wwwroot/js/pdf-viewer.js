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
            const page = await state.pdfDoc.getPage(pageNum);
            const viewport = page.getViewport({ scale, rotation: rotation ?? state.rotation });
            const textContent = await page.getTextContent();

            textLayerEl.innerHTML = '';
            textLayerEl.style.width = viewport.width + 'px';
            textLayerEl.style.height = viewport.height + 'px';

            for (const item of textContent.items) {
                const tx = window.pdfjsLib?.Util.transform(viewport.transform, item.transform);
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

    async function search(canvasEl, query, dotNetRef) {
        const state = _docs.get(canvasEl);
        if (!state || !query) {
            dotNetRef.invokeMethodAsync('OnSearchResults', 0, 0).catch(console.error);
            return;
        }
        const regex = new RegExp(query.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'), 'gi');
        let totalMatches = 0;
        const matchesPerPage = [];
        try {
            for (let i = 1; i <= state.pdfDoc.numPages; i++) {
                const page = await state.pdfDoc.getPage(i);
                const textContent = await page.getTextContent();
                const pageText = textContent.items.map(item => item.str).join(' ');
                const pageMatches = (pageText.match(regex) || []).length;
                matchesPerPage.push(pageMatches);
                totalMatches += pageMatches;
            }
            dotNetRef.invokeMethodAsync('OnSearchResults', totalMatches, matchesPerPage).catch(console.error);
        } catch (err) {
            console.error('tmPdfViewer.search', err);
            dotNetRef.invokeMethodAsync('OnSearchResults', 0, []).catch(console.error);
        }
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
        search,
        renderAllPages,
        destroy
    };
})();
