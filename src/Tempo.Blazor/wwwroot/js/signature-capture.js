window.tmSignatureCapture = window.tmSignatureCapture || {
    capturePointer: function (svgElement, pointerId) {
        if (!svgElement || typeof svgElement.setPointerCapture !== 'function' || pointerId == null) {
            return;
        }

        try {
            svgElement.setPointerCapture(pointerId);
        } catch {
            // Pointer capture is an enhancement; drawing still works without it.
        }
    },

    releasePointer: function (svgElement, pointerId) {
        if (!svgElement || typeof svgElement.releasePointerCapture !== 'function' || pointerId == null) {
            return;
        }

        try {
            if (typeof svgElement.hasPointerCapture !== 'function' || svgElement.hasPointerCapture(pointerId)) {
                svgElement.releasePointerCapture(pointerId);
            }
        } catch {
            // Browsers may reject release after implicit capture loss.
        }
    },

    exportPng: function (svgElement) {
        if (!svgElement) {
            return null;
        }

        const serializer = new XMLSerializer();
        const svgText = serializer.serializeToString(svgElement);
        const rect = svgElement.getBoundingClientRect();
        const width = Math.max(Math.ceil(rect.width || svgElement.clientWidth || 520), 1);
        const height = Math.max(Math.ceil(rect.height || svgElement.clientHeight || 180), 1);
        const ratio = Math.max(window.devicePixelRatio || 1, 1);
        const canvas = document.createElement('canvas');
        canvas.width = Math.ceil(width * ratio);
        canvas.height = Math.ceil(height * ratio);
        canvas.style.width = `${width}px`;
        canvas.style.height = `${height}px`;

        const context = canvas.getContext('2d');
        if (!context) {
            return `data:image/svg+xml,${encodeURIComponent(svgText)}`;
        }

        context.scale(ratio, ratio);
        const image = new Image();
        const svgUrl = `data:image/svg+xml;charset=utf-8,${encodeURIComponent(svgText)}`;

        return new Promise((resolve) => {
            image.onload = function () {
                context.clearRect(0, 0, width, height);
                context.drawImage(image, 0, 0, width, height);
                resolve(canvas.toDataURL('image/png'));
            };

            image.onerror = function () {
                resolve(`data:image/svg+xml,${encodeURIComponent(svgText)}`);
            };

            image.src = svgUrl;
        });
    }
};
