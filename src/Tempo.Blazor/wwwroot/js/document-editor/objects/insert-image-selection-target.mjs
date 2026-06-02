// Phase D — objects/insert-image-selection-target.mjs
// `createSelectionTargetForInsertImageCommand({read, createSelectionSnapshot,
//   firstModelSelection, restoreTextSelectionFromObjectSelection, findBlock,
//   firstTextBlock, isEditableTextBlock, blockText, sortObject})` →
//   `selectionTargetForInsertImageCommand(model, body, currentSelection)`.
// Coerces the command body's `Selection` (or the current editor selection) into a
// canonical `{blockId, offset, region, headerFooterId, tableId, cellId}` insertion
// target. When the selection is an object selection, it's first restored to text;
// when the selection's block can't be found, the first editable text block in the
// model is used and the offset resets to 0. The offset is clamped to the block's
// text length (Pascal/camel input accepted via `read`).

const REQUIRED = [
    'read', 'createSelectionSnapshot', 'firstModelSelection',
    'restoreTextSelectionFromObjectSelection', 'findBlock', 'firstTextBlock',
    'isEditableTextBlock', 'blockText', 'sortObject',
];

export function createSelectionTargetForInsertImageCommand(deps) {
    const opts = deps || {};
    for (const key of REQUIRED) {
        if (typeof opts[key] !== 'function') {
            throw new TypeError(
                `createSelectionTargetForInsertImageCommand requires options.${key} (function)`);
        }
    }
    const {
        read, createSelectionSnapshot, firstModelSelection,
        restoreTextSelectionFromObjectSelection, findBlock, firstTextBlock,
        isEditableTextBlock, blockText, sortObject,
    } = opts;

    return function selectionTargetForInsertImageCommand(model, body, currentSelection) {
        const explicitSelection = read(body || {}, 'Selection', 'selection', null);
        let snapshot = createSelectionSnapshot(
            explicitSelection || currentSelection || firstModelSelection(model));
        if (snapshot.isObjectSelection === true || snapshot.selectionMode === 'Object') {
            snapshot = restoreTextSelectionFromObjectSelection(snapshot);
        }
        let block = snapshot.blockId ? findBlock(model, snapshot.blockId) : null;
        if (!block) {
            block = firstTextBlock(model);
            snapshot = createSelectionSnapshot(block
                ? { region: 'Body', blockId: block.id, offset: 0 }
                : firstModelSelection(model));
        }
        const offset = isEditableTextBlock(block)
            ? Math.max(0, Math.min(blockText(block).length, Number(snapshot.offset || 0) || 0))
            : 0;
        return sortObject({
            blockId: (block && block.id) || snapshot.blockId || '',
            offset,
            region: snapshot.region || read(body || {}, 'Region', 'region', 'Body'),
            headerFooterId: snapshot.headerFooterId
                || read(body || {}, 'HeaderFooterId', 'headerFooterId', null),
            tableId: snapshot.activeTableId || snapshot.tableId
                || read(body || {}, 'TableId', 'tableId', null),
            cellId: snapshot.activeTableCellId || snapshot.cellId
                || read(body || {}, 'CellId', 'cellId', null),
        });
    };
}
