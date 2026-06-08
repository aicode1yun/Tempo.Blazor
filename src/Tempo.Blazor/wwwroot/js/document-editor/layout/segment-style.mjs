// Phase D — layout/segment-style.mjs
// `normalizeLayoutSegmentStyle(style)` — canonical font/color shape for a layout
//   segment. Pascal/camel inputs collapse, defaults: Arial / 16 / 400 / normal,
//   color & backgroundColor default to `null`. Output is `sortObject`-stable.
// `decorationsFromMarks(marks)` — extracts `'underline'` / `'line-through'` from
//   the run-mark list (strikethrough / strike both map to `'line-through'`), dedup.
// `applySegmentStyleToElement(element, style, decorations)` — writes the style
//   properties to a DOM element. Element is assumed to expose a writable `style`
//   object; `textDecoration` is set only when decorations are present.

import { asArray, asText, sortObject, unique } from '../core/helpers.mjs';

export function normalizeLayoutSegmentStyle(style) {
    const source = style || {};
    // Per-segment style; consumed by field name, so canonical key sorting is unnecessary here and
    // was a hot spot during cold layout (see paragraph-tokenizer / paragraph-engine).
    return Object.assign({}, source, {
        fontFamily: source.fontFamily || source.FontFamily || 'Arial',
        fontSize: Number(source.fontSize || source.FontSize || 16) || 16,
        fontWeight: asText(source.fontWeight || source.FontWeight || '400'),
        fontStyle: asText(source.fontStyle || source.FontStyle || 'normal'),
        color: source.color || source.Color || null,
        backgroundColor: source.backgroundColor || source.BackgroundColor || null,
        baselineShift: Number(source.baselineShift ?? source.BaselineShift ?? 0) || 0,
        characterScale: Math.max(0.1, Number(source.characterScale ?? source.CharacterScale ?? 1) || 1),
        fontVariantCaps: source.fontVariantCaps || source.FontVariantCaps || 'normal',
        kerning: source.kerning ?? source.Kerning ?? true,
        letterSpacing: Number(source.letterSpacing ?? source.LetterSpacing ?? 0) || 0,
    });
}

export function decorationsFromMarks(marks) {
    const decorations = [];
    asArray(marks).forEach(function (mark) {
        const type = String((mark && (mark.type || mark.Type)) || '').toLowerCase();
        if (type === 'underline') decorations.push('underline');
        if (type === 'strikethrough' || type === 'strike') decorations.push('line-through');
        if (type === 'doublestrikethrough' || type === 'doublestrike') decorations.push('double-line-through');
        if (type === 'link' || type === 'hyperlink') decorations.push('underline'); // hyperlinks underline
        if (type === 'insertion') decorations.push('underline'); // tracked insert
        if (type === 'deletion') decorations.push('line-through'); // tracked delete
    });
    return unique(decorations);
}

export function applySegmentStyleToElement(element, style, decorations) {
    element.style.fontFamily = style.fontFamily || 'Arial';
    element.style.fontSize = (Number(style.fontSize || 16) || 16) + 'px';
    element.style.fontWeight = style.fontWeight || '400';
    element.style.fontStyle = style.fontStyle || 'normal';
    // Always assign (with an empty reset) so a REUSED span clears stale color/decoration
    // when a mark is removed (unbold / unlink / accept-revision); otherwise the old value
    // would linger because the renderer reuses segment elements by id.
    element.style.color = style.color || '';
    element.style.backgroundColor = style.backgroundColor || '';
    element.style.textDecoration = asArray(decorations).length ? decorations.join(' ') : '';
}
