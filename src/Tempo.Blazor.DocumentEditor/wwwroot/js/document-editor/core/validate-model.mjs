// Phase D — core/validate-model.mjs
// Structural model validator. Walks the document tree, checks for missing or duplicate
// ids, and verifies that revisionId / commentIds / object-anchor references point at
// real records.
//
// Self-contained: builds the minimal {blocks, revisions, comments} reference maps it
// needs without depending on the heavier `buildIndexes` from the legacy IIFE (which
// pulls in `normalizeImageObject` and the image-pipeline).

import { asArray } from './helpers.mjs';

// Returns `{ ok, errors, counts }`.
//   errors: array of `{ code, path, id? }` records
//   counts: `{ blocks, inlines, objects, drawingObjects, revisions, comments }`
export function validateModel(model) {
    const errors = [];
    const seen = new Set();
    const seenObjectIds = new Set();
    const references = {
        revisions: [],
        comments: [],
        objectAnchors: [],
    };

    // Reference maps populated alongside the walk. Mirror the shape that `buildIndexes`
    // produces so consumers comparing the two can use them interchangeably.
    const indexes = {
        blocks: {},
        inlines: {},
        objects: {},
        drawingObjects: {},
        revisions: {},
        comments: {},
    };

    function requireId(id, path) {
        if (!id) {
            errors.push({ code: 'missing-id', path });
            return;
        }
        if (seen.has(id)) errors.push({ code: 'duplicate-id', path, id });
        seen.add(id);
    }

    function requireObjectId(id, path) {
        if (!id) {
            errors.push({ code: 'missing-id', path });
            return;
        }
        if (seenObjectIds.has(id)) errors.push({ code: 'duplicate-object-id', path, id });
        seenObjectIds.add(id);
    }

    function visitBlock(block, path) {
        if (!block) return;
        requireId(block.id, path);
        if (block.id) indexes.blocks[block.id] = block;

        if (block.type === 'paragraph') {
            asArray(block.content && block.content.runs).forEach((run, index) => {
                requireId(run.id, `${path}.runs[${index}]`);
                if (run.id) indexes.inlines[run.id] = run;
                if (run.kind === 'drawing') {
                    requireObjectId(run.objectId, `${path}.runs[${index}].objectId`);
                    if (run.objectId) {
                        indexes.objects[run.objectId] = run;
                        indexes.drawingObjects[run.objectId] = run;
                    }
                    const runLayout = run.layout || run.Layout || {};
                    const runAnchor = runLayout.anchor || runLayout.Anchor || {};
                    const runAnchorBlockId = runAnchor.blockId || runAnchor.BlockId
                        || runLayout.anchorBlockId || runLayout.AnchorBlockId;
                    if (runAnchorBlockId) {
                        references.objectAnchors.push({
                            id: runAnchorBlockId,
                            path: `${path}.runs[${index}].layout.anchor.blockId`,
                        });
                    }
                }
                if (run.revisionId) {
                    references.revisions.push({
                        id: run.revisionId,
                        path: `${path}.runs[${index}].revisionId`,
                    });
                }
                asArray(run.commentIds).forEach((commentId, commentIndex) => {
                    references.comments.push({
                        id: commentId,
                        path: `${path}.runs[${index}].commentIds[${commentIndex}]`,
                    });
                });
            });
        }

        if (block.type === 'image') {
            requireObjectId(block.content && block.content.objectId, `${path}.object`);
            if (block.content && block.content.objectId) {
                indexes.objects[block.content.objectId] = block;
            }
            const anchor = block.content
                && block.content.layout
                && (block.content.layout.Anchor || block.content.layout.anchor);
            const anchorBlockId = anchor && (anchor.BlockId || anchor.blockId);
            if (anchorBlockId) {
                references.objectAnchors.push({
                    id: anchorBlockId,
                    path: `${path}.content.layout.anchor.blockId`,
                });
            }
        }

        if (block.type === 'table') {
            asArray(block.content && block.content.rows).forEach((row, rowIndex) => {
                requireId(row.id, `${path}.rows[${rowIndex}]`);
                if (row.id) indexes.blocks[row.id] = row;
                asArray(row.cells).forEach((cell, cellIndex) => {
                    requireId(cell.id, `${path}.rows[${rowIndex}].cells[${cellIndex}]`);
                    if (cell.id) indexes.blocks[cell.id] = cell;
                    asArray(cell.blocks).forEach((child, blockIndex) => {
                        visitBlock(child, `${path}.rows[${rowIndex}].cells[${cellIndex}].blocks[${blockIndex}]`);
                    });
                });
            });
        }
    }

    asArray(model && model.body && model.body.blocks).forEach((block, index) => {
        visitBlock(block, `body.blocks[${index}]`);
    });
    asArray(model && model.headers).forEach((region, index) => {
        requireId(region.id, `headers[${index}]`);
        if (region.id) indexes.blocks[region.id] = region;
        asArray(region.blocks).forEach((block, blockIndex) => {
            visitBlock(block, `headers[${index}].blocks[${blockIndex}]`);
        });
    });
    asArray(model && model.footers).forEach((region, index) => {
        requireId(region.id, `footers[${index}]`);
        if (region.id) indexes.blocks[region.id] = region;
        asArray(region.blocks).forEach((block, blockIndex) => {
            visitBlock(block, `footers[${index}].blocks[${blockIndex}]`);
        });
    });

    // Populate revision + comment indexes from the model's top-level arrays.
    asArray(model && model.revisions).forEach(revision => {
        if (revision && (revision.id || revision.Id)) {
            indexes.revisions[revision.id || revision.Id] = revision;
        }
    });
    asArray(model && model.comments).forEach(comment => {
        if (comment && (comment.id || comment.Id)) {
            indexes.comments[comment.id || comment.Id] = comment;
        }
    });

    // Cross-reference validation.
    references.revisions.forEach(reference => {
        if (!indexes.revisions[reference.id]) {
            errors.push({ code: 'dangling-revision-reference', path: reference.path, id: reference.id });
        }
    });
    references.comments.forEach(reference => {
        if (!indexes.comments[reference.id]) {
            errors.push({ code: 'dangling-comment-reference', path: reference.path, id: reference.id });
        }
    });
    references.objectAnchors.forEach(reference => {
        if (!indexes.blocks[reference.id]) {
            errors.push({ code: 'dangling-object-anchor', path: reference.path, id: reference.id });
        }
    });

    return {
        ok: errors.length === 0,
        errors,
        counts: {
            blocks: Object.keys(indexes.blocks).length,
            inlines: Object.keys(indexes.inlines).length,
            objects: Object.keys(indexes.objects).length,
            drawingObjects: Object.keys(indexes.drawingObjects).length,
            revisions: Object.keys(indexes.revisions).length,
            comments: Object.keys(indexes.comments).length,
        },
    };
}
