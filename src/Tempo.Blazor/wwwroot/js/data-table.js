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
            if (frame) {
                cancelAnimationFrame(frame);
                frame = 0;
            }
            // Flush the final width before committing so a release within one frame of the last
            // movement still persists the exact width the user left.
            dotNetRef.invokeMethodAsync('OnColumnResized', columnKey, lastWidth)
                .then(() => dotNetRef.invokeMethodAsync('OnColumnResizeCommitted'))
                .catch(() => { });
        }

        document.body.style.userSelect = 'none';
        document.body.style.cursor = 'col-resize';
        document.addEventListener('pointermove', onMove);
        document.addEventListener('pointerup', onUp);
        document.addEventListener('pointercancel', onUp);
    }

    function downloadFile(fileName, contentType, base64) {
        try {
            const binary = atob(base64);
            const bytes = new Uint8Array(binary.length);
            for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
            const blob = new Blob([bytes], { type: contentType || 'application/octet-stream' });
            const url = URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = fileName || 'export';
            document.body.appendChild(a);
            a.click();
            a.remove();
            // Keep the blob URL alive long enough for slow consumers (e.g. automated
            // download capture) — revoking too early cancels an in-flight download.
            setTimeout(() => URL.revokeObjectURL(url), 60000);
        } catch (err) {
            console.error('tmDataTable.downloadFile', err);
        }
    }

    return { startColumnResize, downloadFile };
})();
