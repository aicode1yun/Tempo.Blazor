// Phase D — core/fingerprint.mjs
// Stable structural fingerprints for documents. Used by the host to detect when a
// snapshot has actually changed and by the selection stability layer to bind
// soft-references to specific document versions.
//
// All pure functions. The hash is FNV-1a (32-bit), zero-padded to 8 hex chars and
// prefixed `fnv1a-` so callers can recognise the format.

import { asArray, asText, sortObject } from './helpers.mjs';
import { blockText } from './text-helpers.mjs';

export function stableJsonString(value) {
    return JSON.stringify(sortObject(value || {}));
}

export function hashStableString(value) {
    const text = asText(value);
    let hash = 2166136261;
    for (let i = 0; i < text.length; i++) {
        hash ^= text.charCodeAt(i);
        hash += (hash << 1) + (hash << 4) + (hash << 7) + (hash << 8) + (hash << 24);
    }
    return 'fnv1a-' + (hash >>> 0).toString(16).padStart(8, '0');
}

export function createDocumentFingerprint(model) {
    return hashStableString(stableJsonString(model || {}));
}

// A selection-stability fingerprint — captures only the structural identity of blocks
// (ids, types, text content of paragraphs, drawing anchors, table structure, image
// objectIds) so unrelated metadata changes don't invalidate selection tokens.
export function createSelectionDocumentFingerprint(model) {
    function blockFingerprint(block) {
        if (!block) return null;
        const type = block.type || block.Type || '';
        const item = {
            id: block.id || block.Id || '',
            type,
        };
        if (type === 'paragraph') {
            item.text = blockText(block);
            item.drawings = asArray((block.content && block.content.runs)
                || (block.Content && block.Content.Inlines))
                .filter(run => run && String(run.kind || run.Kind || run.$type || '').toLowerCase() === 'drawing')
                .map((run, index) => {
                    const layout = run.layout || run.Layout || {};
                    const anchor = layout.anchor || layout.Anchor || {};
                    const transform = layout.transform || layout.Transform || {};
                    return {
                        id: run.id || run.Id || '',
                        objectId: run.objectId || run.ObjectId || run.id || run.Id || '',
                        inlineIndex: index,
                        anchorBlockId: anchor.blockId || anchor.BlockId || '',
                        anchorOffset: Number(anchor.offset ?? anchor.Offset ?? 0) || 0,
                        width: Number(transform.width ?? transform.Width ?? 0) || 0,
                        height: Number(transform.height ?? transform.Height ?? 0) || 0,
                    };
                });
        } else if (type === 'table') {
            item.rows = asArray((block.content && block.content.rows)
                || (block.Content && block.Content.Rows)).map(row => asArray(row.cells || row.Cells).map(cell => ({
                id: cell.id || cell.Id || '',
                blocks: asArray(cell.blocks || cell.Blocks).map(blockFingerprint),
            })));
        } else if (type === 'image') {
            item.objectId = (block.content && (block.content.objectId || block.content.ObjectId))
                || block.id || block.Id || '';
        }
        return sortObject(item);
    }

    return hashStableString(stableJsonString({
        documentId: (model && (model.documentId || model.DocumentId)) || '',
        schemaVersion: (model && (model.schemaVersion || model.SchemaVersion)) || '',
        body: asArray((model && model.body && model.body.blocks)
            || (model && model.Body && model.Body.Blocks)).map(blockFingerprint),
        headers: asArray((model && model.headers) || (model && model.Headers)).map(header => ({
            id: header.id || header.Id || '',
            blocks: asArray(header.blocks || header.Blocks).map(blockFingerprint),
        })),
        footers: asArray((model && model.footers) || (model && model.Footers)).map(footer => ({
            id: footer.id || footer.Id || '',
            blocks: asArray(footer.blocks || footer.Blocks).map(blockFingerprint),
        })),
    }));
}
