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
    return sortObject(Object.assign({}, source, {
        fontFamily: source.fontFamily || source.FontFamily || 'Arial',
        fontSize: Number(source.fontSize || source.FontSize || 16) || 16,
        fontWeight: asText(source.fontWeight || source.FontWeight || '400'),
        fontStyle: asText(source.fontStyle || source.FontStyle || 'normal'),
        color: source.color || source.Color || null,
        backgroundColor: source.backgroundColor || source.BackgroundColor || null,
    }));
}

export function decorationsFromMarks(marks) {
    const decorations = [];
    asArray(marks).forEach(function (mark) {
        const type = String((mark && (mark.type || mark.Type)) || '').toLowerCase();
        if (type === 'underline') decorations.push('underline');
        if (type === 'strikethrough' || type === 'strike') decorations.push('line-through');
    });
    return unique(decorations);
}

export function applySegmentStyleToElement(element, style, decorations) {
    element.style.fontFamily = style.fontFamily || 'Arial';
    element.style.fontSize = (Number(style.fontSize || 16) || 16) + 'px';
    element.style.fontWeight = style.fontWeight || '400';
    element.style.fontStyle = style.fontStyle || 'normal';
    if (style.color) element.style.color = style.color;
    if (style.backgroundColor) element.style.backgroundColor = style.backgroundColor;
    if (asArray(decorations).length) element.style.textDecoration = decorations.join(' ');
}
