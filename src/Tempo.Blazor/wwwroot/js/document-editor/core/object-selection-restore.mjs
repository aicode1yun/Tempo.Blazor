// Phase D — core/object-selection-restore.mjs
// `createObjectSelectionRestorer({...})` factory → `restoreTextSelectionFromObjectSelection(selection)`
// — when the user dismisses an object selection (e.g. presses Escape on a selected
// image), this returns the text-selection snapshot to fall back to (preferring the
// originally-anchored text selection if recorded, otherwise the object's anchor
// block, finally the first model selection).

export function createObjectSelectionRestorer(options) {
    const opts = options || {};
    const required = [
        'createSelectionSnapshot',
        'normalizeTextSelectionPayload',
        'firstModelSelection',
    ];
    for (const key of required) {
        if (typeof opts[key] !== 'function') {
            throw new TypeError(
                `createObjectSelectionRestorer requires options.${key} (function)`);
        }
    }
    const {
        createSelectionSnapshot,
        normalizeTextSelectionPayload,
        firstModelSelection,
    } = opts;

    function restoreTextSelectionFromObjectSelection(selection) {
        const snapshot = createSelectionSnapshot(selection || {});
        const objectSelection = snapshot.objectSelection || {};
        const textSelection = objectSelection.textSelection || snapshot.textSelection || null;
        const anchorBlockId = objectSelection.anchorBlockId
            || objectSelection.blockId
            || snapshot.blockId;
        const region = snapshot.region || 'Body';
        let restored;
        if (anchorBlockId) {
            restored = normalizeTextSelectionPayload({
                region: objectSelection.region || snapshot.region || 'Body',
                blockId: anchorBlockId,
                offset: objectSelection.anchorOffset ?? snapshot.offset ?? 0,
                headerFooterId: objectSelection.headerFooterId
                    || snapshot.headerFooterId || null,
                tableId: objectSelection.tableId
                    || snapshot.activeTableId || snapshot.tableId || null,
                cellId: objectSelection.cellId
                    || snapshot.activeTableCellId || snapshot.cellId || null,
            }, null, region);
        } else if (textSelection) {
            restored = normalizeTextSelectionPayload(textSelection, null, region);
        } else {
            restored = normalizeTextSelectionPayload(
                firstModelSelection(null), null, region);
        }
        return createSelectionSnapshot(Object.assign({}, restored, {
            mode: 'Text',
            selectionMode: 'Text',
            isObjectSelection: false,
            objectId: null,
            activeObjectId: null,
            activeImageBlockId: null,
            objectSelection: null,
            hitTargetKind: 'text',
        }));
    }

    return Object.freeze({ restoreTextSelectionFromObjectSelection });
}
