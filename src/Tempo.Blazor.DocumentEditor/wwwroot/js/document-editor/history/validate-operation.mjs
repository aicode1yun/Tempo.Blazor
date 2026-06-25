// Phase D — history/validate-operation.mjs
// `createOperationValidator({ findBlock, findDrawingRunByObjectId, attachOperationMethods })`
// → `validateOperation(model, operation)`.
//
// Returns `{ ok, errors, operation }`. Errors are collected (not thrown) so the
// caller (the apply-operation dispatcher) can decide what to do with multiple
// validation failures.
//
// Pure factory — no closure state.

import { OperationTypes } from './operation-types.mjs';
import { normalizeTarget, normalizeRange } from '../core/normalize-target.mjs';
import { blockText } from '../core/text-helpers.mjs';

const TARGET_TYPES = Object.freeze([
    OperationTypes.InsertText,
    OperationTypes.SplitParagraph,
    OperationTypes.MergeParagraph,
    OperationTypes.SetParagraphAttribute,
    OperationTypes.InsertImage,
    OperationTypes.UpdateImageLayout,
    OperationTypes.MoveDrawingObject,
    OperationTypes.UpdateImageMetadata,
]);

const RANGE_TYPES = Object.freeze([
    OperationTypes.DeleteRange,
    OperationTypes.ApplyMark,
    OperationTypes.RemoveMark,
]);

const IMAGE_LAYOUT_TYPES = Object.freeze([
    OperationTypes.UpdateImageLayout,
    OperationTypes.MoveDrawingObject,
    OperationTypes.UpdateImageMetadata,
]);

export function createOperationValidator(options) {
    const opts = options || {};
    const required = ['findBlock', 'findDrawingRunByObjectId', 'attachOperationMethods'];
    for (const key of required) {
        if (typeof opts[key] !== 'function') {
            throw new TypeError(`createOperationValidator requires options.${key} (function)`);
        }
    }
    const { findBlock, findDrawingRunByObjectId, attachOperationMethods } = opts;

    function validateOperation(model, operation) {
        const op = attachOperationMethods(operation || {});
        const errors = [];

        if (!op.id) errors.push({ code: 'missing-id', path: 'operation.id' });
        if (!op.type) errors.push({ code: 'missing-type', path: 'operation.type' });
        if (!op.timestamp) errors.push({ code: 'missing-timestamp', path: 'operation.timestamp' });
        if (!op.source) errors.push({ code: 'missing-source', path: 'operation.source' });
        if (op.type && !OperationTypes[op.type]) {
            errors.push({ code: 'unknown-type', path: 'operation.type', value: op.type });
        }

        // Target-block validation (operations that take a `target.blockId`)
        if (TARGET_TYPES.indexOf(op.type) >= 0) {
            const target = normalizeTarget(op.target || op.Target);
            const block = findBlock(model, target.blockId);
            const targetDrawing = (IMAGE_LAYOUT_TYPES.indexOf(op.type) >= 0) && target.objectId
                ? findDrawingRunByObjectId(model, target.objectId)
                : null;
            if (!block) {
                if (!targetDrawing) {
                    errors.push({
                        code: 'missing-target-block',
                        path: 'operation.target.blockId',
                        blockId: target.blockId,
                    });
                }
            } else if (block.type === 'paragraph'
                && (target.offset < 0 || target.offset > blockText(block).length)) {
                errors.push({
                    code: 'offset-out-of-range',
                    path: 'operation.target.offset',
                    offset: target.offset,
                    length: blockText(block).length,
                });
            }
        }

        // Range validation (DeleteRange / ApplyMark / RemoveMark)
        if (RANGE_TYPES.indexOf(op.type) >= 0) {
            const range = normalizeRange(op.range || op.Range);
            const rangeBlock = findBlock(model, range.blockId);
            if (!rangeBlock) {
                errors.push({
                    code: 'missing-target-block',
                    path: 'operation.range.blockId',
                    blockId: range.blockId,
                });
            } else if (rangeBlock.type !== 'paragraph'
                || range.start < 0
                || range.end > blockText(rangeBlock).length
                || range.start > range.end) {
                errors.push({
                    code: 'invalid-range',
                    path: 'operation.range',
                    start: range.start,
                    end: range.end,
                    length: blockText(rangeBlock).length,
                });
            }
        }

        // Image-layout validation
        if (IMAGE_LAYOUT_TYPES.indexOf(op.type) >= 0) {
            const imageTarget = normalizeTarget(op.target || op.Target);
            const drawingTarget = imageTarget.objectId
                ? findDrawingRunByObjectId(model, imageTarget.objectId)
                : null;
            if (!drawingTarget) {
                errors.push({
                    code: 'target-not-drawing-object',
                    path: 'operation.target.objectId',
                    blockId: imageTarget.blockId,
                    objectId: imageTarget.objectId || '',
                });
            }
            const layout = op.newLayout || op.NewLayout || op.layout || op.Layout;
            const anchor = layout && (layout.Anchor || layout.anchor);
            const anchorBlockId = anchor && (anchor.BlockId || anchor.blockId);
            if (anchorBlockId && !findBlock(model, anchorBlockId)) {
                errors.push({
                    code: 'dangling-image-anchor',
                    path: 'operation.layout.anchor.blockId',
                    blockId: anchorBlockId,
                });
            }
        }

        return { ok: errors.length === 0, errors, operation: op };
    }

    return Object.freeze({ validateOperation });
}
