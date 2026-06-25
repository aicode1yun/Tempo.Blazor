// Phase D — objects/layout-helpers.mjs
// Pure helpers for floating-image / drawing layout: relative position resolution,
// vertical alignment, position spec normalization, layout kind (inline/anchored/fixed),
// and the `readObjectWrapSide` aggregator that walks the `object.wrap` /
// `object.wrapSide` precedence chain.

import { asText, sortObject } from '../core/helpers.mjs';
import { normalizeWrapSideName } from './wrap-modes.mjs';

// Walk the various `wrap`-related keys on an object record to find the wrap side.
// Mirrors the legacy IIFE `readObjectWrapSide` precedence exactly.
export function readObjectWrapSide(object) {
    const obj = object || {};
    const wrap = obj.wrap || obj.Wrap || {};
    return normalizeWrapSideName(
        obj.wrapSide ?? obj.WrapSide
        ?? obj.side ?? obj.Side
        ?? obj.wrapText ?? obj.WrapText
        ?? wrap.side ?? wrap.Side
        ?? wrap.wrapSide ?? wrap.WrapSide
        ?? wrap.wrapText ?? wrap.WrapText);
}

// Relative position enum: Page=0, Margin=1, Column=2, Paragraph=3, Character=4, Line=5.
// Default 'Column' for unknown / missing values.
export function normalizeRelativePositionName(value) {
    if (value === undefined || value === null || value === '') return 'Column';
    if (typeof value === 'number') {
        if (value === 0) return 'Page';
        if (value === 1) return 'Margin';
        if (value === 2) return 'Column';
        if (value === 3) return 'Paragraph';
        if (value === 4) return 'Character';
        if (value === 5) return 'Line';
        return 'Column';
    }
    const raw = String(value).replace(/\s+/g, '').replace(/-/g, '').toLowerCase();
    if (raw === '0' || raw === 'page') return 'Page';
    if (raw === '1' || raw === 'margin' || raw === 'margins') return 'Margin';
    if (raw === '2' || raw === 'column') return 'Column';
    if (raw === '3' || raw === 'paragraph') return 'Paragraph';
    if (raw === '4' || raw === 'character' || raw === 'char') return 'Character';
    if (raw === '5' || raw === 'line') return 'Line';
    return asText(value) || 'Column';
}

export function relativePositionToValue(value) {
    const normalized = normalizeRelativePositionName(value);
    if (normalized === 'Page') return 0;
    if (normalized === 'Margin') return 1;
    if (normalized === 'Column') return 2;
    if (normalized === 'Paragraph') return 3;
    if (normalized === 'Character') return 4;
    if (normalized === 'Line') return 5;
    return 2;
}

// Vertical alignment ordinal: 0=default/None, 1=Top, 2=Middle, 3=Bottom.
export function verticalAlignmentToValue(value) {
    if (value === 0 || value === 1 || value === 2 || value === 3) return value;
    const raw = String(value || '').replace(/[\s_-]+/g, '').toLowerCase();
    if (raw === 'top' || raw === 'start') return 1;
    if (raw === 'middle' || raw === 'center' || raw === 'centre') return 2;
    if (raw === 'bottom' || raw === 'end') return 3;
    return 0;
}

// Normalize a `{ relativeTo, align, offset }` position spec to a stable sorted shape.
// `fallbackAlign` (e.g. 'Left' or 'Top') is used when neither camel nor Pascal align
// keys are provided.
export function normalizePositionSpec(value, fallbackAlign) {
    const source = value || {};
    return sortObject({
        relativeTo: normalizeRelativePositionName(source.relativeTo ?? source.RelativeTo ?? 'Column'),
        align: asText(source.align || source.Align || fallbackAlign || 'Left'),
        offset: Number(source.offset ?? source.Offset ?? 0) || 0,
    });
}

// Layout kind enum: Inline=0, Anchored=1, Fixed=2.
export function normalizeLayoutKindName(value) {
    if (value === undefined || value === null || value === '') return 'Inline';
    if (typeof value === 'number') {
        if (value === 1) return 'Anchored';
        if (value === 2) return 'Fixed';
        return 'Inline';
    }
    const raw = String(value).replace(/\s+/g, '').replace(/-/g, '').toLowerCase();
    if (raw === '1' || raw === 'anchored' || raw === 'floating') return 'Anchored';
    if (raw === '2' || raw === 'fixed' || raw === 'fixedonpage') return 'Fixed';
    return 'Inline';
}
