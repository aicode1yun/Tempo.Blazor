export function getElementRect(element) {
    if (!element || typeof element.getBoundingClientRect !== 'function') {
        return { left: 0, top: 0, width: 0, height: 0 };
    }

    const rect = element.getBoundingClientRect();
    return {
        left: rect.left,
        top: rect.top,
        width: rect.width,
        height: rect.height
    };
}
