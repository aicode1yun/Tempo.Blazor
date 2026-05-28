// Phase D — objects/image-preview-controller.mjs
// `createImagePreviewControllerFactory({findBlock, normalizeImageObject,
// imageObjectToLayout, buildIndexes, createParagraphLayoutEngine,
// affectedParagraphsAroundObject, createOperation, applyOperation,
// OperationTypes})` factory → `createImagePreviewController(model, options?)`
// — drag/resize preview state machine for an image block. While previewing, the
// image's `content.layout` is mutated in place to its current `preview` snapshot
// so the layout engine reflects the in-progress change without touching history.
//   • startDrag(blockId) / moveDrag(delta) — slides via horizontal/vertical offsets.
//   • startResize(blockId, settings) / moveResize(delta) — adjusts width/height;
//     when `settings.lockAspectRatio === true`, height follows width by the
//     original aspect ratio.
//   • cancel() — restores original layout; idempotent (returns `{rolledBack:false}` if no preview).
//   • commit() — rolls layout back to original, emits an `UpdateImageLayout`
//     operation with the preview as the target value, lets `applyOperation`
//     register it in history and returns the operation result tagged as
//     `transactionType: 'preview'`.
// Returns the controller object; throws when `begin()` targets a non-image block.

import { clone, sortObject } from '../core/helpers.mjs';

const REQUIRED = [
    'findBlock', 'normalizeImageObject', 'imageObjectToLayout', 'buildIndexes',
    'createParagraphLayoutEngine', 'affectedParagraphsAroundObject',
    'createOperation', 'applyOperation', 'OperationTypes',
];

export function createImagePreviewControllerFactory(deps) {
    const opts = deps || {};
    for (const key of REQUIRED) {
        if (opts[key] === undefined || opts[key] === null) {
            throw new TypeError(
                `createImagePreviewControllerFactory requires options.${key}`);
        }
    }
    const {
        findBlock,
        normalizeImageObject,
        imageObjectToLayout,
        buildIndexes,
        createParagraphLayoutEngine,
        affectedParagraphsAroundObject,
        createOperation,
        applyOperation,
        OperationTypes,
    } = opts;

    return function createImagePreviewController(model, options) {
        const controllerOpts = options || {};
        let state = null;

        function findImage(blockId) {
            const block = findBlock(model, blockId);
            if (!block || block.type !== 'image') {
                throw new Error('image-preview: missing image block ' + blockId);
            }
            return block;
        }

        function begin(blockId, mode, settings) {
            const block = findImage(blockId);
            const normalized = normalizeImageObject(block);
            state = {
                mode,
                blockId,
                original: normalized,
                preview: clone(normalized),
                settings: settings || {},
            };
            return sortObject({ ok: true, preview: true, mode, object: state.preview });
        }

        function applyPreview() {
            const block = findImage(state.blockId);
            block.content.layout = imageObjectToLayout(state.preview);
            buildIndexes(model);
            const layout = createParagraphLayoutEngine(null, controllerOpts).layoutDocument(model);
            return layout;
        }

        function startDrag(blockId) { return begin(blockId, 'drag', {}); }

        function moveDrag(delta) {
            if (!state || state.mode !== 'drag') {
                return { ok: false, error: 'drag-not-started' };
            }
            state.preview.horizontalPosition.offset =
                Number(state.original.horizontalPosition.offset || 0)
                + Number((delta && (delta.dx ?? delta.Dx)) || 0);
            state.preview.verticalPosition.offset =
                Number(state.original.verticalPosition.offset || 0)
                + Number((delta && (delta.dy ?? delta.Dy)) || 0);
            const layout = applyPreview();
            return sortObject({
                ok: true, preview: true, mode: 'drag',
                object: state.preview, layout,
            });
        }

        function startResize(blockId, settings) { return begin(blockId, 'resize', settings || {}); }

        function moveResize(delta) {
            if (!state || state.mode !== 'resize') {
                return { ok: false, error: 'resize-not-started' };
            }
            const dx = Number((delta && (delta.dx ?? delta.Dx)) || 0);
            const dy = Number((delta && (delta.dy ?? delta.Dy)) || 0);
            let nextWidth = Math.max(1, Number(state.original.width || 1) + dx);
            let nextHeight = Math.max(1, Number(state.original.height || 1) + dy);
            if (state.settings.lockAspectRatio === true
                || state.settings.LockAspectRatio === true) {
                const ratio = Math.max(0.01,
                    Number(state.original.width || 1)
                    / Math.max(1, Number(state.original.height || 1)));
                nextHeight = nextWidth / ratio;
            }
            state.preview.width = nextWidth;
            state.preview.height = nextHeight;
            const layout = applyPreview();
            return sortObject({
                ok: true, preview: true, mode: 'resize',
                object: state.preview, layout,
            });
        }

        function cancel() {
            if (!state) return { ok: true, rolledBack: false };
            const block = findImage(state.blockId);
            block.content.layout = imageObjectToLayout(state.original);
            const cancelled = state;
            state = null;
            buildIndexes(model);
            return sortObject({
                ok: true, rolledBack: true,
                mode: cancelled.mode, object: cancelled.original,
            });
        }

        function commit() {
            if (!state) return { ok: false, error: 'preview-not-started' };
            const preview = clone(state.preview);
            const blockId = state.blockId;
            const mode = state.mode;
            const affected = affectedParagraphsAroundObject(model, blockId);
            const block = findImage(blockId);
            block.content.layout = imageObjectToLayout(state.original);
            const op = createOperation(OperationTypes.UpdateImageLayout, {
                target: { blockId, offset: 0 },
                layout: imageObjectToLayout(preview),
                affectedParagraphIds: affected,
            }, { source: mode + '-preview-commit' });
            const result = applyOperation(model, op);
            state = null;
            return sortObject(Object.assign({}, result, {
                ok: result.ok !== false,
                singleTransaction: true,
                operationCount: 1,
                affectedParagraphIds: affected,
                transactionType: 'preview',
                command: 'UpdateImageLayout',
            }));
        }

        return {
            startDrag,
            moveDrag,
            startResize,
            moveResize,
            cancel,
            commit,
        };
    };
}
