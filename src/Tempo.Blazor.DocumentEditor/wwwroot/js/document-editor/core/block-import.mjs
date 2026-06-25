// Phase D — core/block-import.mjs
// Block-level import pipeline (C#-JSON → internal model). Pure functions that build
// paragraph, table, image, and page-break block records from the wire format.
//
// Extracted from the legacy IIFE now that inline runs, marks, and helpers are all
// available as ES modules. Body/header/footer regions go through `importRegion`.

import { asArray, asText, hasOwn, read, sortObject } from './helpers.mjs';
import {
    importInlineRun,
    mergeAdjacentTextRuns,
} from './inline-runs.mjs';

function stableId(prefix, path) {
    return String(prefix || 'id') + '-' + String(path || '0').replace(/[^a-z0-9_-]+/gi, '-');
}

export function importParagraphContent(source, path, paragraphProperties) {
    const content = source || {};
    const properties = paragraphProperties || {};
    let runs = asArray(read(content, 'Inlines', 'inlines', []))
        .map((run, index) => importInlineRun(run, path + '-run-' + index));
    if (runs.length === 0) {
        runs.push(importInlineRun({ Text: '' }, path + '-run-0'));
    }
    runs = mergeAdjacentTextRuns(runs);
    return sortObject({
        type: 'paragraph',
        runs,
        alignment: read(content, 'Alignment', 'alignment',
            read(properties, 'Alignment', 'alignment', null)),
        lineSpacing: read(content, 'LineSpacing', 'lineSpacing',
            read(properties, 'LineSpacing', 'lineSpacing', null)),
        spacingBefore: read(properties, 'SpacingBefore', 'spacingBefore', null),
        spacingAfter: read(properties, 'SpacingAfter', 'spacingAfter', null),
        leftIndent: read(properties, 'LeftIndent', 'leftIndent', null),
        rightIndent: read(properties, 'RightIndent', 'rightIndent', null),
        style: sortObject(read(content, 'Style', 'style', {}) || {}),
    });
}

// Image block content (block-level image, distinct from inline drawing runs).
export function importImageObject(source, path) {
    const content = source || {};
    return sortObject({
        type: 'image',
        objectId: asText(read(content, 'ObjectId', 'objectId',
            read(content, 'Id', 'id', ''))) || stableId('object', path),
        source: read(content, 'Source', 'source', 0),
        url: read(content, 'Url', 'url', null),
        assetId: read(content, 'AssetId', 'assetId', null),
        altText: asText(read(content, 'AltText', 'altText', '')),
        isDecorative: read(content, 'IsDecorative', 'isDecorative', false) === true,
        caption: asText(read(content, 'Caption', 'caption', '')),
        size: sortObject(read(content, 'Size', 'size', {}) || {}),
        naturalSize: sortObject(read(content, 'NaturalSize', 'naturalSize', {}) || {}),
        alignment: read(content, 'Alignment', 'alignment', 1),
        layout: sortObject(read(content, 'Layout', 'layout',
            read(content, 'FloatingLayout', 'floatingLayout', {})) || {}),
        style: sortObject(read(content, 'Style', 'style', {}) || {}),
        linkUrl: read(content, 'LinkUrl', 'linkUrl', null),
    });
}

// Table content: rows × cells with nested blocks. Recurses through `importBlock` for
// nested cell content.
export function importTable(source, path) {
    const content = source || {};
    const rows = asArray(read(content, 'Rows', 'rows', [])).map((row, rowIndex) => ({
        id: asText(read(row, 'Id', 'id', '')) || stableId('row', path + '-' + rowIndex),
        type: 'tableRow',
        cells: asArray(read(row, 'Cells', 'cells', [])).map((cell, cellIndex) => ({
            id: asText(read(cell, 'Id', 'id', ''))
                || stableId('cell', path + '-' + rowIndex + '-' + cellIndex),
            type: 'tableCell',
            rowSpan: Math.max(1, Number(read(cell, 'RowSpan', 'rowSpan', 1)) || 1),
            colSpan: Math.max(1, Number(read(cell, 'ColSpan', 'colSpan', 1)) || 1),
            width: Number(read(cell, 'Width', 'width', 0)) || null,
            height: Number(read(cell, 'Height', 'height', 0)) || null,
            style: sortObject(read(cell, 'Style', 'style', {}) || {}),
            blocks: asArray(read(cell, 'Blocks', 'blocks', [])).map((block, blockIndex) =>
                importBlock(block, path + '-' + rowIndex + '-' + cellIndex + '-' + blockIndex)),
        })),
    }));
    return sortObject({
        type: 'table',
        rows,
        style: sortObject(read(content, 'Style', 'style', {}) || {}),
    });
}

// Single block dispatcher — picks paragraph / table / image / pageBreak based on type or
// content shape. Recurses into table cells via `importTable`.
export function importBlock(source, path) {
    const block = source || {};
    const content = read(block, 'Content', 'content', block);
    let type = String(read(block, 'Type', 'type',
        read(content, 'Type', 'type', (content && content.$type) || 'paragraph'))).toLowerCase();
    let normalizedContent;
    const contentType = String(hasOwn(content, '$type') ? content.$type
        : (read(content, 'Type', 'type', '') || '')).toLowerCase();

    if (type === '6' || type.indexOf('pagebreak') >= 0 || type.indexOf('page-break') >= 0) {
        normalizedContent = { type: 'pageBreak' };
        type = 'pageBreak';
    } else if (type === '4' || type.indexOf('table') >= 0 || contentType.indexOf('table') >= 0
        || hasOwn(content, 'Rows') || hasOwn(content, 'rows')) {
        normalizedContent = importTable(content, path + '-table');
        type = 'table';
    } else if (type === '5' || type.indexOf('image') >= 0 || contentType.indexOf('image') >= 0
        || hasOwn(content, 'Url') || hasOwn(content, 'url')
        || hasOwn(content, 'AssetId') || hasOwn(content, 'assetId')
        || hasOwn(content, 'Layout') || hasOwn(content, 'layout')) {
        normalizedContent = importImageObject(content, path + '-image');
        type = 'image';
    } else {
        normalizedContent = importParagraphContent(content, path + '-paragraph',
            read(block, 'ParagraphProperties', 'paragraphProperties', {}));
        type = 'paragraph';
    }

    return sortObject({
        id: asText(read(block, 'Id', 'id', '')) || stableId('block', path),
        type,
        content: normalizedContent,
        order: read(block, 'Order', 'order', null),
        style: sortObject(read(block, 'Style', 'style', {}) || {}),
    });
}

// Body / header / footer regions. `type` is the expected default ('body' / 'header' /
// 'footer') — the importer may downgrade to 'footer' if the source data says so.
export function importRegion(source, path, type) {
    const region = source || {};
    const sourceType = String(read(region, 'Region', 'region',
        read(region, 'Type', 'type', type))).toLowerCase();
    const numericType = Number(read(region, 'Type', 'type', Number.NaN));
    const normalizedType = sourceType.indexOf('footer') >= 0 || numericType === 1
        ? 'footer'
        : sourceType.indexOf('header') >= 0 || numericType === 0
            ? 'header'
            : type;
    return sortObject({
        id: asText(read(region, 'Id', 'id', '')) || stableId(type, path),
        type: normalizedType,
        scope: asText(read(region, 'Scope', 'scope', 'Primary')) || 'Primary',
        sectionId: read(region, 'SectionId', 'sectionId', null),
        blocks: asArray(read(region, 'Blocks', 'blocks', []))
            .map((block, index) => importBlock(block, path + '-block-' + index)),
    });
}
