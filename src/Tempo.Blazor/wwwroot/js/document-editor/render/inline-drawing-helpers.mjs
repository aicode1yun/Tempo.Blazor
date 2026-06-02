// Phase D — render/inline-drawing-helpers.mjs
// `inlineDrawingIsSelected(inst, block, run, object)` — true when the inline
//   drawing's `objectId` matches the editor's active object selection
//   (`inst.selection.activeObjectId` / `objectId` / `objectSelection.objectId`).
//   Empty / missing object ids never match.
// `createRenderDrawingObjectTestMarkerHtml({escapeHtml, asText})` →
//   `renderDrawingObjectTestMarkerHtml(objectId)` — emits the screen-reader-only
//   `<span data-testid="document-wysiwyg-drawing-object-<id>">` marker that the
//   E2E renderer tests rely on to locate a drawing by id. Empty / missing id
//   returns an empty string.

import { asText } from '../core/helpers.mjs';

export function inlineDrawingIsSelected(inst, block, run, object) {
    const selection = (inst && inst.selection) || {};
    const objectId = asText(
        (object && object.objectId)
        || (run && (run.objectId || run.ObjectId))
        || '');
    if (!objectId) return false;
    const selectedObjectId = asText(
        selection.activeObjectId
        || selection.objectId
        || (selection.objectSelection && selection.objectSelection.objectId)
        || '');
    return !!selectedObjectId && selectedObjectId === objectId;
}

export function createRenderDrawingObjectTestMarkerHtml(options) {
    const opts = options || {};
    if (typeof opts.escapeHtml !== 'function') {
        throw new TypeError(
            'createRenderDrawingObjectTestMarkerHtml requires options.escapeHtml (function)');
    }
    if (typeof opts.asText !== 'function') {
        throw new TypeError(
            'createRenderDrawingObjectTestMarkerHtml requires options.asText (function)');
    }
    const { escapeHtml, asText: asTextDep } = opts;
    return function renderDrawingObjectTestMarkerHtml(objectId) {
        const id = asTextDep(objectId);
        if (!id) return '';
        return '<span class="tm-document-wysiwyg-host__sr-only"'
            + ' data-testid="document-wysiwyg-drawing-object-' + escapeHtml(id) + '"'
            + ' aria-hidden="true"></span>';
    };
}
