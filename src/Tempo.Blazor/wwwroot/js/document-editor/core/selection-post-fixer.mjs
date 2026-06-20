// Phase D — core/selection-post-fixer.mjs
// `createSelectionPostFixerFactory({ findBlock, findDrawingRunByObjectId,
//   createObjectSelectionSnapshot })` → `createSelectionPostFixer(schema)` →
//   `{ schema, fix(model, selection) }`. The post-fixer canonicalises a selection
//   after an edit: object selections that still resolve to a drawing run become a
//   proper Object-mode snapshot; a caret on an image block snaps to the object;
//   cross-limit (e.g. cross-cell) ranges collapse to the anchor.
//
// Selection normalisation is built internally from the injected `findBlock`.

import { clone, sortObject } from './helpers.mjs';
import { createDefaultSchemaRegistry } from './schema.mjs';
import { isObjectSelectionSnapshot, createLogicalRange } from './selection-snapshot.mjs';
import { createSelectionNormalizers } from './selection-normalize.mjs';

export function createSelectionPostFixerFactory(options) {
    const opts = options || {};
    for (const key of ['findBlock', 'findDrawingRunByObjectId', 'createObjectSelectionSnapshot']) {
        if (typeof opts[key] !== 'function') {
            throw new TypeError(`createSelectionPostFixerFactory requires options.${key} (function)`);
        }
    }
    const { findBlock, findDrawingRunByObjectId, createObjectSelectionSnapshot } = opts;
    const { normalizeSelectionSnapshot } = createSelectionNormalizers({ findBlock });

    return function createSelectionPostFixer(schema) {
        return {
            schema: schema || createDefaultSchemaRegistry(),
            fix: function (model, selection) {
                const snapshot = normalizeSelectionSnapshot(model, selection);
                if (isObjectSelectionSnapshot(snapshot)) {
                    const objectId = snapshot.activeObjectId || snapshot.objectId || (snapshot.objectSelection && snapshot.objectSelection.objectId) || '';
                    if (objectId && findDrawingRunByObjectId(model, objectId)) {
                        return sortObject(createObjectSelectionSnapshot(model, { objectId, region: snapshot.region || 'Body' }, snapshot.textSelection || null));
                    }
                }
                const focusBlock = findBlock(model, snapshot.focus.blockId);
                if (focusBlock && focusBlock.type === 'image') {
                    snapshot.focus.objectId = (focusBlock.content && focusBlock.content.objectId) || focusBlock.id;
                    snapshot.focus.offset = snapshot.focus.affinity === 'before' ? 0 : 1;
                    snapshot.anchor = clone(snapshot.focus);
                    snapshot.range = createLogicalRange(snapshot.anchor, snapshot.focus, 'none');
                    snapshot.isCollapsed = true;
                }
                if (!snapshot.isCollapsed && snapshot.anchor.limitId && snapshot.focus.limitId && snapshot.anchor.limitId !== snapshot.focus.limitId) {
                    snapshot.focus = clone(snapshot.anchor);
                    snapshot.range = createLogicalRange(snapshot.anchor, snapshot.focus, 'none');
                    snapshot.isCollapsed = true;
                    snapshot.rejectedCrossLimit = true;
                }
                return sortObject(snapshot);
            },
        };
    };
}
