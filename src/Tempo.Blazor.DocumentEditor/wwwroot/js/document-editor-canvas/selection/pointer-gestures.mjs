export function classifyPointerGesture(event, state = {}) {
    const detail = Math.max(1, Number(event?.detail || 1) || 1);
    if (event?.shiftKey === true && state.hasAnchor === true) {
        return 'extend';
    }

    if (detail >= 3) {
        return 'paragraph';
    }

    if (detail === 2) {
        return 'word';
    }

    return 'caret';
}

export function shouldBeginDrag(startPoint, currentPoint, threshold = 3) {
    if (!startPoint || !currentPoint) {
        return false;
    }

    const dx = Number(currentPoint.x || 0) - Number(startPoint.x || 0);
    const dy = Number(currentPoint.y || 0) - Number(startPoint.y || 0);
    return Math.sqrt(dx * dx + dy * dy) >= Math.max(1, Number(threshold) || 3);
}

export function normalizePointerPoint(event, pageElement) {
    if (!event || !pageElement || typeof pageElement.getBoundingClientRect !== 'function') {
        return null;
    }

    const rect = pageElement.getBoundingClientRect();
    const scale = Math.max(0.01, Number(pageElement.getAttribute?.('data-canvas-page-zoom-scale') || 1) || 1);
    return {
        pageIndex: Number(pageElement.getAttribute?.('data-page-index') || 0) || 0,
        x: (Number(event.clientX || 0) - rect.left) / scale,
        y: (Number(event.clientY || 0) - rect.top) / scale,
    };
}
