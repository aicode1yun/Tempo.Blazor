// Phase D — core/import-orchestrator.mjs
// `createImportOrchestrator({ normalizeRevision, buildIndexes })` →
//                             `importFromCSharpJson(document)`.
//
// The orchestrator unwraps the C# wire envelope, runs the importRegion pipeline for
// body+headers+footers, normalises revisions/comments/assets, and triggers index
// building. Factory pattern lets it inject `buildIndexes` (which depends on the
// image pipeline via `normalizeImageObject`) and `normalizeRevision` (which lives in
// the legacy IIFE because it generates non-deterministic ids).

import { asArray, asText, read, sortObject } from './helpers.mjs';
import { importRegion } from './block-import.mjs';

export function createImportOrchestrator(options) {
    const opts = options || {};
    const required = ['normalizeRevision', 'buildIndexes'];
    for (const key of required) {
        if (typeof opts[key] !== 'function') {
            throw new TypeError(`createImportOrchestrator requires options.${key} (function)`);
        }
    }
    const { normalizeRevision, buildIndexes } = opts;

    function importFromCSharpJson(document) {
        const source = (document && (document.Document || document.document))
            ? (document.Document || document.document)
            : (document || {});

        const headerFooterRegions = asArray(read(source, 'HeadersFooters', 'headersFooters', []));
        const isFooterRegion = region => {
            const typeValue = read(region, 'Region', 'region',
                read(region, 'Type', 'type', 'header'));
            const numericType = Number(read(region, 'Type', 'type', Number.NaN));
            return String(typeValue).toLowerCase().indexOf('footer') >= 0 || numericType === 1;
        };

        const model = sortObject({
            schemaVersion: Number(read(source, 'SchemaVersion', 'schemaVersion', 1) || 1),
            documentId: asText(read(source, 'DocumentId', 'documentId', 'document')),
            title: asText(read(source, 'Title', 'title',
                read(source, 'Name', 'name', ''))),
            metadata: sortObject(read(source, 'Metadata', 'metadata', {}) || {}),
            pageSettings: sortObject(read(source, 'PageSettings', 'pageSettings', {}) || {}),
            body: importRegion({
                Id: 'body',
                Blocks: read(source, 'Blocks', 'blocks', []),
            }, 'body', 'body'),
            headers: headerFooterRegions
                .filter(region => !isFooterRegion(region))
                .map((region, index) => importRegion(region, 'header-' + index, 'header')),
            footers: headerFooterRegions
                .filter(isFooterRegion)
                .map((region, index) => importRegion(region, 'footer-' + index, 'footer')),
            revisions: asArray(read(source, 'Revisions', 'revisions', [])).map(normalizeRevision),
            comments: asArray(read(source, 'Comments', 'comments', [])).map(sortObject),
            assets: asArray(read(source, 'Assets', 'assets', [])).map(sortObject),
        });

        buildIndexes(model);
        return model;
    }

    return Object.freeze({ importFromCSharpJson });
}
