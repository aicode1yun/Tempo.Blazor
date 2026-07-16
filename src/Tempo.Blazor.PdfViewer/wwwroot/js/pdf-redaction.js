/**
 * tmRedaction — destructive redaction export helpers for TmRedactionLayer.
 *  - measure:             size of the redaction surface for pointer normalization
 *  - exportRedactedPdf:   rasterize every page (PDF.js), burn black rectangles, and
 *                         rebuild a brand-new PDF (pdf-lib) from the pixels only. The
 *                         original content streams, text layer, fonts and metadata are
 *                         NEVER copied, so redacted content is not extractable.
 *  - exportRedactedImage: repaint the image on a canvas with black rectangles and
 *                         download the new bitmap (original pixels are gone).
 *  - createSampleDocument: build a small text PDF (pdf-lib) and return a blob URL —
 *                         used by demos/tests that need a deterministic text layer.
 *
 * Requires pdf.min.mjs (PDF.js v5) and pdf-lib.min.js in the same directory.
 */
window.tmRedaction = (function () {
    'use strict';

    const _scriptDir = (function () {
        const src = document.currentScript && document.currentScript.src;
        return src ? src.substring(0, src.lastIndexOf('/') + 1) : '/_content/Tempo.Blazor.PdfViewer/js/';
    })();

    let _pdfjs = null;

    async function _ensurePdfJs() {
        if (_pdfjs) return _pdfjs;
        const mod = await import(_scriptDir + 'pdf.min.mjs');
        mod.GlobalWorkerOptions.workerSrc = _scriptDir + 'pdf.worker.min.mjs';
        _pdfjs = mod;
        return _pdfjs;
    }

    function _ensurePdfLib() {
        if (window.PDFLib) return Promise.resolve(window.PDFLib);
        return new Promise((resolve, reject) => {
            const script = document.createElement('script');
            script.src = _scriptDir + 'pdf-lib.min.js';
            script.onload = () => window.PDFLib ? resolve(window.PDFLib) : reject(new Error('pdf-lib failed to load'));
            script.onerror = () => reject(new Error('pdf-lib failed to load'));
            document.head.appendChild(script);
        });
    }

    function measure(el) {
        if (!el) return [0, 0];
        return [el.clientWidth || 0, el.clientHeight || 0];
    }

    function _download(blob, fileName) {
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = fileName || 'download';
        document.body.appendChild(link);
        link.click();
        link.remove();
        // Keep the blob alive long enough for tests to fetch its content.
        setTimeout(() => URL.revokeObjectURL(url), 60000);
    }

    function _dataUrlToBytes(dataUrl) {
        const base64 = dataUrl.substring(dataUrl.indexOf(',') + 1);
        const binary = atob(base64);
        const bytes = new Uint8Array(binary.length);
        for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
        return bytes;
    }

    /**
     * Rasterizing rebuild: the output PDF contains ONLY page-sized PNG images rendered
     * from the source with the redaction rectangles already painted over. Nothing from
     * the source document object graph is copied.
     */
    async function exportRedactedPdf(url, payloadJson, fileName) {
        const payload = JSON.parse(payloadJson || '{"pages":[]}');
        const rectsByPage = new Map();
        (payload.pages || []).forEach(p => rectsByPage.set(p.pageNumber, p.rects || []));

        const pdfjs = await _ensurePdfJs();
        const PDFLib = await _ensurePdfLib();
        const source = await pdfjs.getDocument(url).promise;
        const output = await PDFLib.PDFDocument.create();

        const renderScale = 2;
        for (let pageNumber = 1; pageNumber <= source.numPages; pageNumber++) {
            const page = await source.getPage(pageNumber);
            const viewport = page.getViewport({ scale: renderScale });
            const canvas = document.createElement('canvas');
            canvas.width = Math.ceil(viewport.width);
            canvas.height = Math.ceil(viewport.height);
            const ctx = canvas.getContext('2d');
            await page.render({ canvasContext: ctx, viewport: viewport }).promise;

            ctx.fillStyle = '#000000';
            (rectsByPage.get(pageNumber) || []).forEach(r => {
                ctx.fillRect(r.x * canvas.width, r.y * canvas.height, r.width * canvas.width, r.height * canvas.height);
            });

            const png = await output.embedPng(_dataUrlToBytes(canvas.toDataURL('image/png')));
            const pageSize = page.getViewport({ scale: 1 });
            const outPage = output.addPage([pageSize.width, pageSize.height]);
            outPage.drawImage(png, { x: 0, y: 0, width: pageSize.width, height: pageSize.height });
        }

        const bytes = await output.save();
        _download(new Blob([bytes], { type: 'application/pdf' }), fileName || 'redacted.pdf');
        await source.destroy();
    }

    /** Repaints the image with the redaction rectangles burned in and downloads the new bitmap. */
    function exportRedactedImage(url, payloadJson, fileName) {
        const payload = JSON.parse(payloadJson || '{"pages":[]}');
        const rects = (payload.pages || []).flatMap(p => p.rects || []);

        return new Promise((resolve, reject) => {
            const image = new Image();
            image.crossOrigin = 'anonymous';
            image.onload = () => {
                try {
                    const canvas = document.createElement('canvas');
                    canvas.width = image.naturalWidth;
                    canvas.height = image.naturalHeight;
                    const ctx = canvas.getContext('2d');
                    ctx.drawImage(image, 0, 0);
                    ctx.fillStyle = '#000000';
                    rects.forEach(r => {
                        ctx.fillRect(r.x * canvas.width, r.y * canvas.height, r.width * canvas.width, r.height * canvas.height);
                    });
                    canvas.toBlob(blob => {
                        if (!blob) { reject(new Error('canvas export failed')); return; }
                        _download(blob, fileName || 'redacted.png');
                        resolve(null);
                    }, 'image/png');
                } catch (err) {
                    reject(err);
                }
            };
            image.onerror = () => reject(new Error('image failed to load: ' + url));
            image.src = url;
        });
    }

    /**
     * Builds a simple single-page text PDF (real, extractable text operators) and
     * returns a blob URL. linesJson is a JSON array of strings (WinAnsi-safe).
     */
    async function createSampleDocument(linesJson) {
        const PDFLib = await _ensurePdfLib();
        const lines = JSON.parse(linesJson || '[]');
        const doc = await PDFLib.PDFDocument.create();
        const page = doc.addPage([595, 842]); // A4 portrait, points
        const font = await doc.embedFont(PDFLib.StandardFonts.Helvetica);
        let y = 780;
        lines.forEach(line => {
            page.drawText(String(line), { x: 60, y: y, size: 14, font: font });
            y -= 28;
        });
        const bytes = await doc.save();
        const url = URL.createObjectURL(new Blob([bytes], { type: 'application/pdf' }));
        return url;
    }

    /** Draws a simple ID-card-like bitmap with the given text lines and returns a blob URL. */
    function createSampleImage(linesJson) {
        const lines = JSON.parse(linesJson || '[]');
        const canvas = document.createElement('canvas');
        canvas.width = 800;
        canvas.height = 480;
        const ctx = canvas.getContext('2d');
        ctx.fillStyle = '#f1f5f9';
        ctx.fillRect(0, 0, canvas.width, canvas.height);
        ctx.strokeStyle = '#94a3b8';
        ctx.strokeRect(10, 10, canvas.width - 20, canvas.height - 20);
        ctx.fillStyle = '#0f172a';
        ctx.font = '28px sans-serif';
        let y = 90;
        lines.forEach(line => {
            ctx.fillText(String(line), 50, y);
            y += 60;
        });
        return new Promise((resolve, reject) => {
            canvas.toBlob(blob => {
                if (!blob) { reject(new Error('canvas export failed')); return; }
                resolve(URL.createObjectURL(blob));
            }, 'image/png');
        });
    }

    return {
        measure,
        exportRedactedPdf,
        exportRedactedImage,
        createSampleDocument,
        createSampleImage
    };
})();
