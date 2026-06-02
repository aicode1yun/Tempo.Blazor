// Phase D — layout/paragraph-runs.mjs
// Helpers for flattening paragraph runs into a stable representation used by the
// layout engine. These sit between the document model (raw runs with marks) and the
// line-breaker (which wants a plain array of {id, kind, text, start, end, style}).
//
// `cssLengthToPixels(value, fallback)` — converts a CSS length (number or string
//   with pt/px suffix) to pixels.
// `mergeTextStyle(baseStyle, run)` — merges block-level base style with run-level
//   marks/style, producing an {fontFamily, fontSize, color, …} CSS style object.
// `flattenParagraphRuns(paragraph, normalizeImageObject?)` — walks the runs of a
//   paragraph-input `{runs, style, id}` and returns a flat array suitable for the
//   line-breaker. Drawing runs are represented with `kind:'drawing'` and an attached
//   `object` (normalised via the injected `normalizeImageObject`; pass null to skip).
// `runForOffset(runs, offset)` — binary-search helper: finds the run that covers
//   the given text offset; falls back to the last run.

import { asArray, asText } from '../core/helpers.mjs';
import { markType } from '../core/marks.mjs';

export function cssLengthToPixels(value, fallback) {
    if (typeof value === 'number') return Number.isFinite(value) && value > 0 ? value : fallback;
    const text = asText(value).trim().toLowerCase();
    const number = parseFloat(text);
    if (!Number.isFinite(number) || number <= 0) return fallback;
    if (text.endsWith('pt')) return number * 4 / 3;
    return number;
}

// Combines text-decoration values (underline + line-through can coexist) without dupes.
function addDecoration(existing, value) {
    const parts = asText(existing).split(/\s+/).filter(Boolean);
    if (parts.indexOf(value) === -1) parts.push(value);
    return parts.join(' ');
}

export function mergeTextStyle(baseStyle, run) {
    const style = Object.assign({}, baseStyle || {}, (run && run.style) || (run && run.Style) || {});
    asArray(run && (run.marks || run.Marks)).forEach(function (mark) {
        const type = markType(mark);
        const value = mark && (mark.value ?? mark.Value ?? mark.color ?? mark.Color ?? null);
        if (type === 'bold') style.fontWeight = style.fontWeight || '700';
        if (type === 'italic') style.fontStyle = style.fontStyle || 'italic';
        if (type === 'underline') style.textDecoration = addDecoration(style.textDecoration, 'underline');
        if (type === 'strikethrough' || type === 'strike') style.textDecoration = addDecoration(style.textDecoration, 'line-through');
        if ((type === 'link' || type === 'hyperlink') && value) {
            style.textDecoration = addDecoration(style.textDecoration, 'underline');
            style.color = style.color || '#0563c1'; // Word hyperlink blue (run color still wins)
        }
        if (type === 'insertion') { // tracked insert
            style.textDecoration = addDecoration(style.textDecoration, 'underline');
            style.color = '#1b7f3b';
        }
        if (type === 'deletion') { // tracked delete (text kept, struck through)
            style.textDecoration = addDecoration(style.textDecoration, 'line-through');
            style.color = '#c0392b';
        }
        if (type === 'comment') style.backgroundColor = style.backgroundColor || '#fff3a3'; // commented range highlight
        if (type === 'fontfamily' && value) style.fontFamily = value;
        if (type === 'fontsize' && value) style.fontSize = cssLengthToPixels(value, style.fontSize || 16);
        if ((type === 'textcolor' || type === 'fontcolor' || type === 'foregroundcolor') && value) style.color = value;
        if ((type === 'highlight' || type === 'backgroundcolor') && value) style.backgroundColor = value;
    });
    return style;
}

export function flattenParagraphRuns(paragraph, normalizeImageObject) {
    const source = paragraph || {};
    let runs = asArray(source.runs || source.Runs
        || (source.content && source.content.runs)
        || (source.Content && source.Content.Runs));
    if (runs.length === 0) {
        runs = [{ text: asText(source.text || source.Text || '') }];
    }
    const baseStyle = source.style || source.Style || {};
    let cursor = 0;
    const result = [];
    runs.forEach(function (run, index) {
        const rawKind = String(run.kind || run.Kind || run.type || run.Type || 'text').toLowerCase();
        const kind = rawKind.indexOf('drawing') >= 0
            ? 'drawing'
            : rawKind.indexOf('field') >= 0
                ? 'field'
                : (rawKind.indexOf('token') >= 0 ? 'token' : 'text');
        const text = kind === 'drawing'
            ? ''
            : asText(run.text || run.Text || run.fallbackText || run.FallbackText || '');
        const object = (kind === 'drawing' && typeof normalizeImageObject === 'function')
            ? normalizeImageObject(run, {
                blockId: source.id || source.Id || source.blockId || source.BlockId || '',
                inlineIndex: index,
            })
            : null;
        result.push({
            id: run.id || run.Id || ('run-' + index),
            kind,
            text,
            start: cursor,
            end: cursor + text.length,
            style: mergeTextStyle(baseStyle, run),
            marks: asArray(run.marks || run.Marks),
            object,
            objectId: (object && object.objectId) || run.objectId || run.ObjectId || null,
        });
        cursor += text.length;
    });
    return result;
}

export function runForOffset(runs, offset) {
    const fallback = runs[0] || { style: {} };
    for (let i = 0; i < runs.length; i++) {
        if (offset >= runs[i].start && offset < runs[i].end) return runs[i];
    }
    return runs[runs.length - 1] || fallback;
}
