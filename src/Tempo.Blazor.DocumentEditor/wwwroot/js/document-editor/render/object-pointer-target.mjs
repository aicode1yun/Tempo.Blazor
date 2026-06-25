// Phase D — render/object-pointer-target.mjs
// `createGetObjectPointerTarget({ELEMENT_NODE, asText})` →
//   `getObjectPointerTarget(inst, target, event?)` — resolves a pointer target to
//   the floating-object overlay/widget/inline-drawing element it belongs to.
//   Returns `{objectId, blockId, element, handleName, isResize}` or null when the
//   target isn't an object surface.
//
// When the pointer is on the selection overlay but no explicit `[data-resize-handle]`
// was matched, the function infers a resize handle name from the pointer position
// within 16px of an edge/corner of the overlay bounding rect.

const OBJECT_SELECTOR = '.tm-wysiwyg-object-selection-overlay[data-object-id], '
    + '.tm-wysiwyg-object-guides-overlay[data-object-id], '
    + '.tm-wysiwyg-object-layer-item[data-object-id], '
    + 'figure.tm-wysiwyg-image[data-object-id], '
    + '.tm-render-image-widget[data-render-object-id], '
    + '.tm-wysiwyg-inline-drawing[data-object-id]';

export function createGetObjectPointerTarget(options) {
    const opts = options || {};
    if (typeof opts.asText !== 'function') {
        throw new TypeError(
            'createGetObjectPointerTarget requires options.asText (function)');
    }
    const ELEMENT_NODE = typeof opts.ELEMENT_NODE === 'number' ? opts.ELEMENT_NODE : 1;
    const { asText } = opts;

    return function getObjectPointerTarget(inst, target, event) {
        if (!inst || !inst.root || !target) return null;
        const element = target.nodeType === ELEMENT_NODE ? target : target.parentElement;
        if (!element || !inst.root.contains(element)) return null;
        const handle = element.closest && element.closest('[data-resize-handle]');
        const objectElement = (handle && handle.closest && handle.closest(OBJECT_SELECTOR))
            || (element.closest && element.closest(OBJECT_SELECTOR));
        if (!objectElement || !inst.root.contains(objectElement)) return null;
        let inferredHandleName = handle ? asText(handle.getAttribute('data-resize-handle') || '') : '';
        if (!inferredHandleName
            && event
            && objectElement.classList
            && objectElement.classList.contains('tm-wysiwyg-object-selection-overlay')) {
            const rect = objectElement.getBoundingClientRect();
            const x = Number(event.clientX || 0);
            const y = Number(event.clientY || 0);
            const edge = 16;
            if (Math.abs(x - rect.right) <= edge && Math.abs(y - rect.bottom) <= edge) inferredHandleName = 'se';
            else if (Math.abs(x - rect.right) <= edge && Math.abs(y - rect.top) <= edge) inferredHandleName = 'ne';
            else if (Math.abs(x - rect.left) <= edge && Math.abs(y - rect.bottom) <= edge) inferredHandleName = 'sw';
            else if (Math.abs(x - rect.left) <= edge && Math.abs(y - rect.top) <= edge) inferredHandleName = 'nw';
            else if (Math.abs(x - rect.right) <= edge) inferredHandleName = 'e';
            else if (Math.abs(x - rect.left) <= edge) inferredHandleName = 'w';
            else if (Math.abs(y - rect.bottom) <= edge) inferredHandleName = 's';
            else if (Math.abs(y - rect.top) <= edge) inferredHandleName = 'n';
        }
        const objectId = asText(
            objectElement.getAttribute('data-object-id')
            || objectElement.getAttribute('data-render-object-id')
            || objectElement.getAttribute('data-block-id')
            || '');
        if (!objectId) return null;
        return {
            objectId,
            blockId: asText(objectElement.getAttribute('data-block-id')
                || objectElement.getAttribute('data-render-block-id') || ''),
            element: objectElement,
            handleName: inferredHandleName,
            isResize: !!inferredHandleName,
        };
    };
}
