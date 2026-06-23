// Phase D — core/selection-normalize.mjs
// `createSelectionNormalizers({ findBlock })` → `{ normalizeLogicalPosition,
//   normalizeLogicalRange, normalizeSelectionSnapshot }`. Clamps a selection against
//   the actual model: positions snap to a real block (falling back to the first text
//   block), offsets clamp to the block's text length (or 1 for objects), and a cross-
//   limit range collapses to its anchor. Only `findBlock` is engine-state dependent;
//   block text / inline lookup / limit lookup are pure imports.

import { clone } from './helpers.mjs';
import { blockText } from './text-helpers.mjs';
import { firstTextBlock } from './first-block.mjs';
import { inlineAtOffset } from './run-finders.mjs';
import { findLimitForBlock } from './limit-finder.mjs';
import {
    createLogicalPosition,
    createLogicalRange,
    createSelectionSnapshot,
} from './selection-snapshot.mjs';

export function createSelectionNormalizers(options) {
    const opts = options || {};
    if (typeof opts.findBlock !== 'function') {
        throw new TypeError('createSelectionNormalizers requires options.findBlock (function)');
    }
    const { findBlock } = opts;

    function normalizeLogicalPosition(model, position) {
        const pos = createLogicalPosition(position);
        const block = findBlock(model, pos.blockId) || firstTextBlock(model);
        if (!block) {
            return createLogicalPosition(Object.assign(pos, { blockId: '', inlineId: null, offset: 0, limitId: null }));
        }
        const max = block.type === 'paragraph' ? blockText(block).length : 1;
        const offset = Math.max(0, Math.min(max, Number(pos.offset || 0)));
        const inline = block.type === 'paragraph' ? inlineAtOffset(block, offset) : null;
        return createLogicalPosition(Object.assign(pos, {
            blockId: block.id,
            inlineId: inline && inline.run ? inline.run.id : null,
            offset,
            affinity: pos.affinity === 'before' ? 'before' : 'after',
            limitId: pos.limitId || findLimitForBlock(model, block.id),
            objectId: block.type === 'image' ? (block.content && block.content.objectId || block.id) : pos.objectId,
        }));
    }

    function normalizeLogicalRange(model, range) {
        const source = range || {};
        const anchor = normalizeLogicalPosition(model, source.anchor || source.Anchor || source.start || source.Start || source);
        let focus = normalizeLogicalPosition(model, source.focus || source.Focus || source.end || source.End || source);
        if (anchor.limitId && focus.limitId && anchor.limitId !== focus.limitId) {
            focus = clone(anchor);
        }
        return createLogicalRange(anchor, focus, source.direction || source.Direction || (anchor.offset <= focus.offset ? 'forward' : 'backward'));
    }

    function normalizeSelectionSnapshot(model, selection) {
        const snapshot = createSelectionSnapshot(selection || {});
        const range = normalizeLogicalRange(model, snapshot.range || snapshot);
        return createSelectionSnapshot(Object.assign({}, snapshot, {
            region: range.anchor.region,
            range,
            anchor: range.anchor,
            focus: range.focus,
            selectionMode: snapshot.selectionMode || snapshot.mode,
            mode: snapshot.mode || snapshot.selectionMode,
            textSelection: snapshot.textSelection || null,
            objectSelection: snapshot.objectSelection || null,
            isObjectSelection: snapshot.isObjectSelection === true,
            activeImageBlockId: snapshot.activeImageBlockId || null,
            activeObjectId: snapshot.activeObjectId || null,
            objectId: snapshot.objectId || null,
            hitTargetKind: snapshot.hitTargetKind || null,
        }));
    }

    return Object.freeze({
        normalizeLogicalPosition,
        normalizeLogicalRange,
        normalizeSelectionSnapshot,
    });
}
