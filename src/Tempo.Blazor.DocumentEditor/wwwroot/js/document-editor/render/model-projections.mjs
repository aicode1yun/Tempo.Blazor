// Phase D — render/model-projections.mjs
// `createModelProjections({blockText, hasRevisionRun})` factory returning
//   `{projectEditing, projectData}`.
// `projectEditing(model)` — UI-side projection used by the editing renderer:
//   image blocks become `imageWidget` with resize handles + accessibility-warning
//   badge when alt text is missing; paragraphs carry a `revision-overlay` class
//   when any run has a `revisionId`. Includes the overlay list at the root.
// `projectData(model)` — pure data projection (no DOM hints): image blocks emit
//   `{type:'image', url, assetId, altText, caption, layout}`; paragraphs emit
//   text + run snapshots (`marks` cloned). Includes a cloned `revisions` array
//   at the root.

import { asArray, clone, sortObject } from '../core/helpers.mjs';

export function createModelProjections(options) {
    const opts = options || {};
    if (typeof opts.blockText !== 'function') {
        throw new TypeError(
            'createModelProjections requires options.blockText (function)');
    }
    if (typeof opts.hasRevisionRun !== 'function') {
        throw new TypeError(
            'createModelProjections requires options.hasRevisionRun (function)');
    }
    const { blockText, hasRevisionRun } = opts;

    function projectEditing(model) {
        const blocks = asArray(model && model.body && model.body.blocks).map(function (block) {
            if (block.type === 'image') {
                return sortObject({
                    kind: 'imageWidget',
                    className: 'tm-editing-image-widget resize-handle data-debug-id',
                    resizeHandles: ['nw', 'n', 'ne', 'e', 'se', 's', 'sw', 'w'],
                    warningBadges: block.content && !block.content.altText
                        ? ['accessibility-warning']
                        : [],
                    mapping: {
                        blockId: block.id,
                        objectId: (block.content && block.content.objectId) || block.id,
                    },
                });
            }
            return sortObject({
                kind: 'paragraph',
                className: hasRevisionRun(block)
                    ? 'tm-editing-paragraph revision-overlay data-debug-id'
                    : 'tm-editing-paragraph data-debug-id',
                mapping: { blockId: block.id },
                runs: asArray(block.content && block.content.runs).map(function (run) {
                    return {
                        id: run.id,
                        text: run.text,
                        mapping: { blockId: block.id, runId: run.id },
                    };
                }),
            });
        });
        return sortObject({
            mode: 'editing',
            blocks,
            overlays: ['selection', 'revision', 'comments'],
        });
    }

    function projectData(model) {
        const blocks = asArray(model && model.body && model.body.blocks).map(function (block) {
            if (block.type === 'image') {
                return sortObject({
                    type: 'image',
                    blockId: block.id,
                    objectId: (block.content && block.content.objectId) || block.id,
                    url: (block.content && block.content.url) || null,
                    assetId: (block.content && block.content.assetId) || null,
                    altText: (block.content && block.content.altText) || '',
                    caption: (block.content && block.content.caption) || '',
                    layout: clone((block.content && block.content.layout) || {}),
                });
            }
            return sortObject({
                type: 'paragraph',
                blockId: block.id,
                text: blockText(block),
                runs: asArray(block.content && block.content.runs).map(function (run) {
                    return {
                        id: run.id,
                        kind: run.kind,
                        text: run.text,
                        marks: clone(run.marks || []),
                        revisionId: run.revisionId || null,
                    };
                }),
            });
        });
        return sortObject({
            mode: 'data',
            blocks,
            revisions: clone((model && model.revisions) || []),
        });
    }

    return Object.freeze({ projectEditing, projectData });
}
