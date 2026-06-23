// Phase D — objects/drawing-index.mjs
// `createDrawingIndexHelpers({buildIndexes})` factory →
//   `ensureDrawingIndexes(model)` — lazy-rebuild via buildIndexes when drawing
//     indexes missing.
//   `rebuildDrawingIndexes(model)` — unconditional rebuild.
//
// Returns the model.indexes object (or an empty placeholder when no model).

const EMPTY = Object.freeze({ drawingObjectsById: {}, drawingRunsByBlockId: {} });

export function createDrawingIndexHelpers(options) {
    const opts = options || {};
    if (typeof opts.buildIndexes !== 'function') {
        throw new TypeError(
            'createDrawingIndexHelpers requires options.buildIndexes (function)');
    }
    const { buildIndexes } = opts;

    function ensureDrawingIndexes(model) {
        if (!model) return EMPTY;
        if (!model.indexes
            || !model.indexes.drawingObjectsById
            || !model.indexes.drawingRunsByBlockId) {
            buildIndexes(model);
        }
        return model.indexes || EMPTY;
    }

    function rebuildDrawingIndexes(model) {
        if (!model) return EMPTY;
        buildIndexes(model);
        return model.indexes || EMPTY;
    }

    return Object.freeze({ ensureDrawingIndexes, rebuildDrawingIndexes });
}
