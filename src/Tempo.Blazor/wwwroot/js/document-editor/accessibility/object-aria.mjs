// Phase D — accessibility/object-aria.mjs
// A11y helpers for drawing objects (images / shapes) in the editor.
//
//   - `objectAccessibilityIdFragment(value)` — sanitises a value to a safe `id`
//     fragment for HTML attributes (alnum/underscore/dash only). Empty input falls
//     back to `'document-object'`.
//   - `activeObjectStatusId(inst)` — builds the canonical `aria-describedby` target
//     for the active-object status live region.
//   - `appendAriaDescribedByToken(current, token, enabled)` — adds/removes a token
//     to a space-separated `aria-describedby` attribute, dedup'd.
//   - `getImageObjectAccessibleLabel(object, fallback)` — falls back through
//     altText → caption → fallback → `'Image'`.
//   - `objectResizeHandleDirectionLabel(handleName)` / `objectResizeHandleAriaLabel(inst, handleName)`
//     — spoken direction (north/east/etc.) and full a11y label for resize handles.

import { asArray, asText } from '../core/helpers.mjs';

export function objectAccessibilityIdFragment(value) {
    const text = asText(value || 'document-object').trim();
    return (text || 'document-object')
        .replace(/[^A-Za-z0-9_-]+/g, '-')
        .replace(/^-+|-+$/g, '')
        || 'document-object';
}

export function activeObjectStatusId(inst) {
    return 'tm-wysiwyg-active-object-status-'
        + objectAccessibilityIdFragment(inst && inst.id || 'default');
}

export function appendAriaDescribedByToken(current, token, enabled) {
    const id = asText(token);
    const parts = asArray(asText(current).split(/\s+/))
        .filter(Boolean)
        .filter(function (part) { return part !== id; });
    if (enabled && id) parts.push(id);
    return parts.join(' ');
}

export function getImageObjectAccessibleLabel(object, fallback) {
    const source = object || {};
    return asText(source.altText || source.AltText
        || source.caption || source.Caption
        || fallback || 'Image')
        || 'Image';
}

export function objectResizeHandleDirectionLabel(handleName) {
    switch (asText(handleName).toLowerCase()) {
        case 'nw': return 'north west';
        case 'n': return 'north';
        case 'ne': return 'north east';
        case 'e': return 'east';
        case 'se': return 'south east';
        case 's': return 'south';
        case 'sw': return 'south west';
        case 'w': return 'west';
        default: return asText(handleName) || 'corner';
    }
}

export function objectResizeHandleAriaLabel(inst, handleName) {
    const opts = inst && inst.options || {};
    const base = opts.ImageResizeHandleLabel || opts.imageResizeHandleLabel || 'Resize image';
    return asText(base || 'Resize image') + ' ' + objectResizeHandleDirectionLabel(handleName);
}
