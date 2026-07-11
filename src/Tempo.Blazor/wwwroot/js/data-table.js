/**
 * Tempo.Blazor — TmDataTable JS interop.
 * Column resize drag: tracks pointer movement on the document and reports the new
 * width back to .NET (OnColumnResized during drag, OnColumnResizeCommitted on release).
 */
window.tmDataTable = (function () {
    'use strict';

    function startColumnResize(dotNetRef, columnKey, startX, startWidth, minWidth) {
        if (!dotNetRef) return;
        const min = minWidth || 60;
        let lastWidth = Math.round(startWidth);
        let frame = 0;

        function onMove(e) {
            const clientX = (e.touches && e.touches.length) ? e.touches[0].clientX : e.clientX;
            const width = Math.max(min, Math.round(startWidth + (clientX - startX)));
            if (width === lastWidth) return;
            lastWidth = width;
            if (frame) return;
            frame = requestAnimationFrame(() => {
                frame = 0;
                dotNetRef.invokeMethodAsync('OnColumnResized', columnKey, width).catch(() => { });
            });
        }

        function onUp() {
            document.removeEventListener('pointermove', onMove);
            document.removeEventListener('pointerup', onUp);
            document.removeEventListener('pointercancel', onUp);
            document.body.style.userSelect = '';
            document.body.style.cursor = '';
            dotNetRef.invokeMethodAsync('OnColumnResizeCommitted').catch(() => { });
        }

        document.body.style.userSelect = 'none';
        document.body.style.cursor = 'col-resize';
        document.addEventListener('pointermove', onMove);
        document.addEventListener('pointerup', onUp);
        document.addEventListener('pointercancel', onUp);
    }

    return { startColumnResize };
})();
