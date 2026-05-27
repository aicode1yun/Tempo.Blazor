// Phase D — core/inline-runs.mjs
// Inline run import/export pipeline + merge logic. Extracted from the legacy IIFE now
// that marks, drawing-kind, and export-types are all available as modules.
//
// All functions are pure: they take plain JSON-like records and produce plain JSON-like
// records. No closure over engine state, no DOM access.
//
// Run shapes:
//   text:    { id, kind: 'text',    text, marks, style, revisionId, commentIds }
//   field:   { id, kind: 'field',   text, key, fieldType, marks, style, revisionId, commentIds }
//   token:   { id, kind: 'token',   text, key,           marks, style, revisionId, commentIds }
//   drawing: { id, kind: 'drawing', type: 'image', objectId, source, url, … }

import {
    asArray,
    asText,
    clone,
    hasOwn,
    read,
    sortObject,
    unique,
} from './helpers.mjs';
import { normalizeMarks, readRevisionIdFromMarks } from './marks.mjs';
import {
    normalizeDrawingKindName,
    exportDrawingKind,
} from '../objects/drawing-kind.mjs';
import { exportFieldType } from './export-types.mjs';

// Convenience — generate a stable id (mirrors legacy `_stableId`). Local copy avoids
// re-importing from a hypothetical id module; the underlying logic is one line.
function stableId(prefix, path) {
    return String(prefix || 'id') + '-' + String(path || '0').replace(/[^a-z0-9_-]+/gi, '-');
}

// ────────────────────────────────────────────────────────────────────────────────
// Drawing run discrimination
// ────────────────────────────────────────────────────────────────────────────────

export function isDrawingRunSource(source) {
    const raw = source || {};
    const discriminator = String(raw.$type || read(raw, 'Type', 'type', '') || '').toLowerCase();
    const internalKind = String(raw.kind || raw.Kind || '').toLowerCase();
    return discriminator.indexOf('drawing') >= 0
        || internalKind === 'drawing'
        || hasOwn(raw, 'ObjectId') || hasOwn(raw, 'objectId');
}

export function normalizeDrawingRun(source, path) {
    const raw = source || {};
    const id = asText(read(raw, 'Id', 'id', '')) || stableId('drawing', path);
    const objectId = asText(read(raw, 'ObjectId', 'objectId', '')) || stableId('object', id || path);
    const marks = normalizeMarks(read(raw, 'Marks', 'marks', []));
    const revisionId = read(raw, 'RevisionId', 'revisionId', null) || readRevisionIdFromMarks(marks);
    return sortObject({
        id,
        kind: 'drawing',
        drawingKind: normalizeDrawingKindName(
            read(raw, 'Kind', 'drawingKind',
                read(raw, 'DrawingKind', 'drawingKind', 'Image'))),
        type: 'image',
        objectId,
        source: read(raw, 'Source', 'source', 0),
        url: read(raw, 'Url', 'url', null),
        assetId: read(raw, 'AssetId', 'assetId', null),
        altText: asText(read(raw, 'AltText', 'altText', '')),
        isDecorative: read(raw, 'IsDecorative', 'isDecorative', false) === true,
        caption: asText(read(raw, 'Caption', 'caption', '')),
        size: sortObject(read(raw, 'Size', 'size', {}) || {}),
        naturalSize: sortObject(read(raw, 'NaturalSize', 'naturalSize', {}) || {}),
        layout: sortObject(read(raw, 'Layout', 'layout',
            read(raw, 'FloatingLayout', 'floatingLayout', {})) || {}),
        style: sortObject(read(raw, 'Style', 'style', {}) || {}),
        linkUrl: read(raw, 'LinkUrl', 'linkUrl', null),
        docx: sortObject(read(raw, 'Docx', 'docx', {}) || {}),
        metadata: sortObject(read(raw, 'Metadata', 'metadata', {}) || {}),
        marks,
        revisionId: revisionId || null,
        commentIds: asArray(read(raw, 'CommentIds', 'commentIds', [])),
    });
}

// ────────────────────────────────────────────────────────────────────────────────
// Inline run import — C#-JSON → internal model
// ────────────────────────────────────────────────────────────────────────────────

export function importInlineRun(source, path) {
    const raw = source || {};
    if (isDrawingRunSource(raw)) {
        return normalizeDrawingRun(raw, path);
    }

    const type = String(read(raw, 'Type', 'type', raw.$type || '')).toLowerCase();
    let kind = 'text';
    if (type.indexOf('field') >= 0 || hasOwn(raw, 'FieldType') || hasOwn(raw, 'fieldType')) kind = 'field';
    if (type.indexOf('token') >= 0 || hasOwn(raw, 'Key') || hasOwn(raw, 'key')) kind = 'token';
    const marks = normalizeMarks(read(raw, 'Marks', 'marks', []));
    const revisionId = read(raw, 'RevisionId', 'revisionId', null) || readRevisionIdFromMarks(marks);
    return sortObject({
        id: asText(read(raw, 'Id', 'id', '')) || stableId('inline', path),
        kind,
        text: asText(read(raw, 'Text', 'text',
            read(raw, 'FallbackText', 'fallbackText',
                read(raw, 'Key', 'key', '')))),
        key: read(raw, 'Key', 'key', null),
        fieldType: read(raw, 'FieldType', 'fieldType', null),
        marks,
        style: sortObject(read(raw, 'Style', 'style', {}) || {}),
        revisionId: revisionId || null,
        commentIds: asArray(read(raw, 'CommentIds', 'commentIds', [])),
    });
}

// ────────────────────────────────────────────────────────────────────────────────
// Inline run export — internal model → C#-JSON
// ────────────────────────────────────────────────────────────────────────────────

export function exportInlineRun(run) {
    const result = {
        Id: run.id,
        Marks: clone(run.marks || []),
    };
    if (run.kind === 'field') {
        result.$type = 'field';
        result.FieldType = exportFieldType(run.fieldType);
        result.FallbackText = run.fallbackText || run.text || null;
        result.DisplayText = run.text || run.displayText || null;
    } else if (run.kind === 'token') {
        result.$type = 'token';
        result.Key = run.key || run.text || '';
        result.DisplayName = run.displayName || run.text || run.key || '';
        result.FallbackText = run.fallbackText || run.text || null;
    } else if (run.kind === 'drawing') {
        result.$type = 'drawing';
        result.ObjectId = run.objectId || run.id;
        result.Kind = exportDrawingKind(run.drawingKind || run.DrawingKind || 'Image');
        result.Source = run.source ?? run.Source ?? 0;
        result.Url = run.url ?? run.Url ?? null;
        result.AssetId = run.assetId ?? run.AssetId ?? null;
        result.AltText = run.altText ?? run.AltText ?? null;
        result.IsDecorative = run.isDecorative === true || run.IsDecorative === true;
        result.Caption = run.caption ?? run.Caption ?? null;
        result.Size = clone(run.size || run.Size || {});
        result.NaturalSize = clone(run.naturalSize || run.NaturalSize || {});
        result.Layout = clone(run.layout || run.Layout || {});
        result.LinkUrl = run.linkUrl ?? run.LinkUrl ?? null;
        if (run.docx || run.Docx) result.Docx = clone(run.docx || run.Docx || {});
        result.Metadata = clone(run.metadata || run.Metadata || {});
    } else {
        result.$type = 'text';
        result.Text = asText(run.text);
    }
    return sortObject(result);
}

// ────────────────────────────────────────────────────────────────────────────────
// Run merging — used by the model importer and by typing operations to keep adjacent
// text runs collapsed when they share identical styling.
// ────────────────────────────────────────────────────────────────────────────────

export function normalizeTextRunForMerge(run) {
    const c = clone(run || {});
    c.id = asText(c.id || c.Id || '');
    delete c.Id;
    c.kind = c.kind || c.Kind || 'text';
    delete c.Kind;
    c.text = asText(c.text ?? c.Text);
    delete c.Text;
    if (c.kind === 'text') {
        delete c.key;
        delete c.Key;
        delete c.fieldType;
        delete c.FieldType;
        delete c.fallbackText;
        delete c.FallbackText;
    }
    c.marks = normalizeMarks(c.marks || c.Marks || []);
    delete c.Marks;
    c.style = sortObject(c.style || c.Style || {});
    delete c.Style;
    c.commentIds = unique(c.commentIds || c.CommentIds || []).sort();
    delete c.CommentIds;
    if (c.revisionId === undefined && c.RevisionId !== undefined) c.revisionId = c.RevisionId;
    delete c.RevisionId;
    if (c.revisionId === undefined) c.revisionId = null;
    return sortObject(c);
}

// Stable JSON key used by `mergeAdjacentTextRuns` to decide if two runs are mergeable.
// Strips `id` and `text` so only styling/marks/etc. drive equality.
function runMergeKey(run) {
    const normalized = normalizeTextRunForMerge(run);
    delete normalized.id;
    delete normalized.text;
    return JSON.stringify(sortObject(normalized));
}

// Replace adjacent text runs that share the same styling with a single concatenated run.
// Drawing runs are preserved verbatim (no merging). Empty trailing runs are dropped.
// An empty input collapses to a single empty text run (so paragraphs never have zero runs).
export function mergeAdjacentTextRuns(runs) {
    const result = [];
    asArray(runs).forEach(run => {
        if (!run) return;
        if (run.kind === 'drawing' || isDrawingRunSource(run)) {
            result.push(normalizeDrawingRun(run, run.id || run.objectId || result.length));
            return;
        }

        const normalized = normalizeTextRunForMerge(run);
        const text = asText(normalized.text);
        if (text.length === 0 && result.length > 0) return;
        const previous = result[result.length - 1];
        if (previous && previous.kind === normalized.kind && runMergeKey(previous) === runMergeKey(normalized)) {
            previous.text = asText(previous.text) + text;
        } else {
            result.push(normalized);
        }
    });
    return result.length > 0 ? result : plainRuns('', 'empty');
}

// Build a single-text-run array — used by `mergeAdjacentTextRuns` and by paragraph
// constructors when an empty paragraph needs at least one inline.
export function plainRuns(text, path) {
    return [importInlineRun({ Id: stableId('inline', path || 'run'), Text: text || '' }, path || 'run')];
}
