// Phase D — render/wysiwyg-object-render-entry.mjs
// `createWysiwygObjectRenderEntryFactory({normalizeImageObject,
//   findLayoutObjectForRender, rectFromGeometry, inlineDrawingIsSelected,
//   asText, sortObject})` →
//   `createWysiwygObjectRenderEntry(inst, block, run, inlineIndex, sourceKind)`
// — assembles the render-entry record for an inline / anchored drawing run.
// When an authoritative layout has positioned the object, the layout's rect +
// pageIndex override the model values so the renderer paints the object where
// the engine placed it.
//   • Falls back to `block.id` for the object id when run/object lack one.
//   • Returns `null` when no object id can be derived (object can't be rendered).
//   • `selected` is computed via `inlineDrawingIsSelected(inst, block, source, object)`.
//   • Output shape: `{sourceKind, blockId, runId, inlineIndex, objectId, object, selected}`.

export function createWysiwygObjectRenderEntryFactory(options) {
    const opts = options || {};
    for (const key of [
        'normalizeImageObject', 'findLayoutObjectForRender', 'rectFromGeometry',
        'inlineDrawingIsSelected', 'asText', 'sortObject',
    ]) {
        if (typeof opts[key] !== 'function') {
            throw new TypeError(
                `createWysiwygObjectRenderEntryFactory requires options.${key} (function)`);
        }
    }
    const {
        normalizeImageObject, findLayoutObjectForRender, rectFromGeometry,
        inlineDrawingIsSelected, asText, sortObject,
    } = opts;

    return function createWysiwygObjectRenderEntry(inst, block, run, inlineIndex, sourceKind) {
        const source = run || block || {};
        let object = normalizeImageObject(source, {
            blockId: (block && block.id) || '',
            inlineIndex,
        });
        const objectId = asText(
            object.objectId
            || source.objectId || source.ObjectId
            || source.id || source.Id
            || (block && block.id)
            || '');
        if (!objectId) return null;
        const layoutObject = findLayoutObjectForRender(
            inst && inst.layout,
            objectId,
            (block && block.id) || object.blockId || object.anchorBlockId || '');
        if (layoutObject && (layoutObject.rect || layoutObject.Rect)) {
            object = Object.assign({}, object, {
                rect: rectFromGeometry(layoutObject.rect || layoutObject.Rect),
                pageIndex: Number(
                    layoutObject.pageIndex ?? layoutObject.PageIndex
                    ?? object.pageIndex ?? 0) || 0,
            });
        }
        const runId = asText(source.id || source.Id || objectId);
        const selected = inlineDrawingIsSelected(inst, block, source, object);
        return sortObject({
            sourceKind: sourceKind || 'drawing-run',
            blockId: asText((block && block.id) || object.blockId || ''),
            runId,
            inlineIndex: Number(inlineIndex ?? object.anchorInlineIndex ?? -1),
            objectId,
            object,
            selected: selected === true,
        });
    };
}
