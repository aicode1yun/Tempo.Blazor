// Phase D — core/export-types.mjs
// Pure enum mappers used by the C#-JSON export pipeline. Each maps a JS-side string
// (camelCase / hyphenated / various aliases) or numeric ordinal to the canonical numeric
// value that the C# abstractions expect on the wire.
//
// Extracted verbatim from the legacy IIFE so the bundled engine and the legacy monolith
// produce byte-identical export payloads.

import { asText, sortObject } from './helpers.mjs';

// Block type ordinal: paragraph=0, heading=1, list=2, quote=3, table=4, image=5,
// pageBreak=6. Default 0 (paragraph) for unknown.
export function exportBlockType(block) {
    const type = String((block && block.type) || '').toLowerCase();
    if (type === 'heading') return 1;
    if (type === 'list') return 2;
    if (type === 'quote') return 3;
    if (type === 'table') return 4;
    if (type === 'image') return 5;
    if (type === 'pagebreak' || type === 'page-break') return 6;
    return 0;
}

// Header/footer enum: header=0, footer=1.
export function exportHeaderFooterType(region) {
    return region && region.type === 'footer' ? 1 : 0;
}

// Header/footer scope: Primary=0, FirstPage=1, EvenPage=2, OddPage=3.
export function exportHeaderFooterScope(scope) {
    const normalized = String(scope || 'Primary').toLowerCase();
    if (normalized === 'firstpage' || normalized === 'first-page' || normalized === 'first') return 1;
    if (normalized === 'evenpage' || normalized === 'evenpages' || normalized === 'even-page'
        || normalized === 'even-pages' || normalized === 'even') return 2;
    if (normalized === 'oddpage' || normalized === 'oddpages' || normalized === 'odd-page'
        || normalized === 'odd-pages' || normalized === 'odd') return 3;
    return 0;
}

// Field type: 0=default, 1=PageCount, 2=PageXofY, 3=Date, 4=DocumentTitle, 5=Author,
// 6=LastSaved. Order matters — earlier `indexOf` matches win.
export function exportFieldType(fieldType) {
    const normalized = String(fieldType || '').toLowerCase();
    if (normalized.indexOf('pagecount') >= 0 || normalized.indexOf('page-count') >= 0
        || normalized.indexOf('numpages') >= 0) return 1;
    if (normalized.indexOf('pagexofy') >= 0 || normalized.indexOf('page-x-of-y') >= 0) return 2;
    if (normalized.indexOf('date') >= 0) return 3;
    if (normalized.indexOf('documenttitle') >= 0 || normalized.indexOf('document-title') >= 0
        || normalized.indexOf('title') >= 0) return 4;
    if (normalized.indexOf('author') >= 0) return 5;
    if (normalized.indexOf('lastsaved') >= 0 || normalized.indexOf('last-saved') >= 0
        || normalized.indexOf('modified') >= 0) return 6;
    return 0;
}

// Comment anchor type: 0=Block, 1=TextRange, 2=Docx, 3=Odt, 4=Page, 5=Rendition.
export function exportCommentAnchorType(value) {
    const normalized = String(value || '').toLowerCase();
    if (value === 1 || normalized === 'textrange' || normalized === 'text-range') return 1;
    if (value === 2 || normalized.indexOf('docx') >= 0) return 2;
    if (value === 3 || normalized.indexOf('odt') >= 0) return 3;
    if (value === 4 || normalized === 'page') return 4;
    if (value === 5 || normalized === 'rendition') return 5;
    return 0;
}

// Comment status: 0=Open, 1=Resolved.
export function exportCommentStatus(value) {
    if (value === 1) return 1;
    return String(value || '').toLowerCase().indexOf('resolved') >= 0 ? 1 : 0;
}

// Comment visibility: 0=Internal, 1=External.
export function exportCommentVisibility(value) {
    const normalized = String(value || '').toLowerCase();
    if (value === 1 || normalized === 'external') return 1;
    return 0;
}

// Revision type ordinal — covers Insertion=0, Deletion=1, Formatting=2, Move=3,
// Structure=4, Image=5, Table=6.
export function exportRevisionType(value) {
    const normalized = String(value || '').toLowerCase();
    if (value === 1 || normalized === 'deletion' || normalized === 'delete') return 1;
    if (value === 2 || normalized === 'formatting' || normalized === 'formatchange'
        || normalized === 'format') return 2;
    if (value === 3 || normalized === 'move') return 3;
    if (value === 4 || normalized === 'structure' || normalized === 'structural') return 4;
    if (value === 5 || normalized === 'image') return 5;
    if (value === 6 || normalized === 'table') return 6;
    return 0;
}

// Revision action: 0=Pending, 1=Accepted, 2=Rejected.
export function exportRevisionAction(value) {
    const normalized = String(value || '').toLowerCase();
    if (value === 1 || normalized === 'accepted') return 1;
    if (value === 2 || normalized === 'rejected') return 2;
    return 0;
}

// Build a revision author record from a string id or a `{Id, DisplayName}` object.
// Returns a sorted `{ Id, DisplayName }` shape consumed by the C# importer.
export function exportRevisionAuthor(value, fallbackId) {
    const source = value || {};
    if (typeof source === 'string') {
        return sortObject({
            Id: source || fallbackId || 'local',
            DisplayName: source || fallbackId || 'local',
        });
    }

    const id = asText(source.Id || source.id || fallbackId
        || source.DisplayName || source.displayName || 'local');
    return sortObject({
        Id: id,
        DisplayName: asText(source.DisplayName || source.displayName || id),
    });
}

// Convert a `Date`, finite number (epoch ms), or non-empty string to an ISO 8601 string.
// Anything else (null/undefined/empty/NaN/Invalid Date) falls back to `now`.
export function exportDateTimeOffset(value) {
    if (value instanceof Date && Number.isFinite(value.getTime())) {
        return value.toISOString();
    }
    if (typeof value === 'number' && Number.isFinite(value)) {
        return new Date(value).toISOString();
    }
    if (typeof value === 'string' && value.trim()) {
        return value;
    }
    return new Date().toISOString();
}

// Text alignment ordinal: 0=Left, 1=Center, 2=Right, 3=Justify.
export function exportTextAlignment(value) {
    const normalized = String(value ?? '').trim().toLowerCase();
    if (value === 1 || normalized === '1' || normalized === 'center' || normalized === 'centre') return 1;
    if (value === 2 || normalized === '2' || normalized === 'right' || normalized === 'end') return 2;
    if (value === 3 || normalized === '3' || normalized === 'justify' || normalized === 'justified') return 3;
    return 0;
}
