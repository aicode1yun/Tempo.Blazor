import { buildDisplayList } from './display-list.mjs';
import { paintDisplayList } from './canvas-renderer.mjs';
import { CANVAS_RENDER_LAYERS } from './layers.mjs';

// Flattens a canvas-editor document into one opaque bitmap per page so it can be used as a signing
// template page (plan S1). Reuses the real layout + display-list + renderer pipeline so the exported
// image is pixel-faithful to the editor, but paints ONLY the printable document layers — never the
// editing chrome (caret/selection, comment & revision anchors, diagnostics). It is a pure module
// with no DOM globals on the hot path: the canvas factory is injected so it runs under Node tests.

// The printable document layers, in back-to-front paint order. Deliberately excludes
// `selection-caret`, `annotations` and `diagnostics` (editor chrome).
export const EXPORT_LAYER_KINDS = Object.freeze([
    CANVAS_RENDER_LAYERS.pageBackground,
    CANVAS_RENDER_LAYERS.content,
    CANVAS_RENDER_LAYERS.objects,
]);

const MIN_EXPORT_SCALE = 1;
const MAX_EXPORT_SCALE = 3;
const DEFAULT_EXPORT_SCALE = 2;

/// Clamps a requested export scale into the supported 1x–3x range, defaulting to 2x (retina) for
/// invalid/missing input (plan O1).
export function clampExportScale(scale) {
    const value = Number(scale);
    if (!Number.isFinite(value) || value <= 0) {
        return DEFAULT_EXPORT_SCALE;
    }

    return Math.min(MAX_EXPORT_SCALE, Math.max(MIN_EXPORT_SCALE, value));
}

/// Builds the display list once for a whole document so callers can render every page without
/// re-laying-out the document per page.
export function buildPageImageDisplayList(model, layout, options = {}) {
    const documentModel = model || {};
    return buildDisplayList(documentModel, layout || {}, {
        theme: options.theme || documentModel.theme || {},
        fontMetrics: options.fontMetrics,
        fontMetricsOptions: options.fontMetricsOptions,
    });
}

/// Renders a single page of a pre-built display list onto a freshly created canvas. Returns the
/// canvas plus the logical page geometry and the applied scale.
export function renderDisplayListPageToCanvas(displayList, pageIndex, options = {}) {
    const pages = Array.isArray(displayList?.pages) ? displayList.pages : [];
    const index = Math.max(0, Number(pageIndex) || 0);
    const page = pages.find(candidate => (Number(candidate?.index) || 0) === index) || pages[index];
    if (!page) {
        throw new Error(`page-image-export: page index ${index} is out of range (pageCount=${pages.length}).`);
    }

    const scale = clampExportScale(options.scale);
    const createCanvas = resolveCanvasFactory(options.createCanvas);
    const logicalWidth = Math.max(1, Number(page.width) || 1);
    const logicalHeight = Math.max(1, Number(page.height) || 1);
    const backingWidth = Math.round(logicalWidth * scale);
    const backingHeight = Math.round(logicalHeight * scale);

    const canvas = createCanvas(backingWidth, backingHeight);
    canvas.width = backingWidth;
    canvas.height = backingHeight;

    const context = canvas.getContext('2d');
    if (context && typeof context.setTransform === 'function') {
        context.setTransform(scale, 0, 0, scale, 0, 0);
    }

    // Opaque white page first: the exported bitmap must never be transparent even when the model has
    // no explicit page fill (a signing page image is composited over arbitrary designer backgrounds).
    if (context && typeof context.fillRect === 'function') {
        context.fillStyle = '#ffffff';
        context.fillRect(0, 0, logicalWidth, logicalHeight);
    }

    // Route every printable layer kind to the single export canvas so all commands flatten onto one
    // context, in the display list's existing back-to-front order.
    const layers = new Map();
    for (const kind of EXPORT_LAYER_KINDS) {
        layers.set(kind, canvas);
    }

    const pageCommands = (Array.isArray(displayList.commands) ? displayList.commands : [])
        .filter(command => (Number(command.pageIndex) || 0) === index && EXPORT_LAYER_KINDS.includes(command.layer));
    paintDisplayList(layers, { ...displayList, commands: pageCommands });

    return { canvas, pageIndex: index, width: logicalWidth, height: logicalHeight, scale };
}

/// Convenience for one page: builds the display list and renders the requested page.
export function renderPageToCanvas(model, layout, pageIndex, options = {}) {
    const displayList = buildPageImageDisplayList(model, layout, options);
    return renderDisplayListPageToCanvas(displayList, pageIndex, options);
}

function resolveCanvasFactory(factory) {
    if (typeof factory === 'function') {
        return (width, height) => factory(width, height);
    }

    const doc = globalThis.document;
    if (doc && typeof doc.createElement === 'function') {
        return (width, height) => {
            const canvas = doc.createElement('canvas');
            canvas.width = width;
            canvas.height = height;
            return canvas;
        };
    }

    throw new Error('page-image-export: no canvas factory available; pass options.createCanvas.');
}
