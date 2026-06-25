export const CANVAS_RENDER_LAYERS = Object.freeze({
    pageBackground: 'page-background',
    content: 'content',
    objects: 'objects',
    selectionCaret: 'selection-caret',
    annotations: 'annotations',
    diagnostics: 'diagnostics',
});

export const CANVAS_LAYER_KINDS = Object.freeze([
    CANVAS_RENDER_LAYERS.pageBackground,
    CANVAS_RENDER_LAYERS.content,
    CANVAS_RENDER_LAYERS.objects,
    CANVAS_RENDER_LAYERS.selectionCaret,
    CANVAS_RENDER_LAYERS.annotations,
    CANVAS_RENDER_LAYERS.diagnostics,
]);

export const CANVAS_CACHE_LAYER_KINDS = Object.freeze([
    CANVAS_RENDER_LAYERS.pageBackground,
    CANVAS_RENDER_LAYERS.content,
    CANVAS_RENDER_LAYERS.objects,
]);

export const CANVAS_OVERLAY_LAYER_KINDS = Object.freeze([
    CANVAS_RENDER_LAYERS.selectionCaret,
    CANVAS_RENDER_LAYERS.annotations,
    CANVAS_RENDER_LAYERS.diagnostics,
]);

export function layerForDisplayCommand(command) {
    const type = String(command?.type || '');
    if (type === 'pageFill' || type === 'pageBorder' || type === 'marginGuide' || type === 'bodyArea') {
        return CANVAS_RENDER_LAYERS.pageBackground;
    }

    if (type === 'imageBox' || type === 'drawingRun') {
        return CANVAS_RENDER_LAYERS.objects;
    }

    if (type === 'selectionRange' || type === 'caret') {
        return CANVAS_RENDER_LAYERS.selectionCaret;
    }

    if (type === 'commentAnchor' || type === 'revisionAnchor') {
        return CANVAS_RENDER_LAYERS.annotations;
    }

    if (type === 'diagnosticOverlay' || type === 'debugBounds') {
        return CANVAS_RENDER_LAYERS.diagnostics;
    }

    return CANVAS_RENDER_LAYERS.content;
}
