// Phase D — core/document-export.mjs
// `exportToCSharpJson` — top-level orchestrator that walks the entire model and
// produces the C# wire format. Pure (no closure state).

import { asArray, clone, sortObject } from './helpers.mjs';
import { exportHeaderFooterType, exportHeaderFooterScope } from './export-types.mjs';
import { exportBlock } from './block-export.mjs';
import { exportComment, exportRevision } from './comment-revision-export.mjs';

export function exportToCSharpJson(model) {
    const source = model || {};
    return sortObject({
        SchemaVersion: source.schemaVersion || 1,
        DocumentId: source.documentId || 'document',
        Title: source.title || '',
        Metadata: clone(source.metadata || {}),
        PageSettings: clone(source.pageSettings || {}),
        Blocks: asArray(source.body && source.body.blocks).map(exportBlock),
        HeadersFooters: asArray(source.headers).concat(asArray(source.footers)).map(region => ({
            Id: region.id,
            Type: exportHeaderFooterType(region),
            Region: region.type === 'footer' ? 'Footer' : 'Header',
            Scope: exportHeaderFooterScope(region.scope),
            ScopeName: region.scope || 'Primary',
            SectionId: region.sectionId || null,
            Blocks: asArray(region.blocks).map(exportBlock),
        })),
        Revisions: asArray(source.revisions).map(exportRevision),
        Comments: asArray(source.comments).map(exportComment),
        Assets: [],
    });
}

// Sibling exporters used by the C# bridge when it only needs a subset of the model
// (e.g. live-syncing reviewer state without re-serialising the whole document).

export function exportRevisionsToCSharpJson(model) {
    return asArray(model && model.revisions).map(exportRevision);
}

export function exportCommentsToCSharpJson(model) {
    return asArray(model && model.comments).map(exportComment);
}
