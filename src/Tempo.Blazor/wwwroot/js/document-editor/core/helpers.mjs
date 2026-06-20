// Phase D — core/helpers.mjs
// Standalone utility helpers shared by the document editor modules.
//
// These functions are pure (no closure over instance state) and have no dependency on the
// rest of the engine, which makes them the safest first extraction.
//
// This module is the canonical source for these helpers.

export function hasOwn(value, key) {
    return !!value && Object.prototype.hasOwnProperty.call(value, key);
}

export function clone(value) {
    if (value === undefined || value === null) return value;
    return JSON.parse(JSON.stringify(value));
}

// Phase B4 — shallow clone helper for hot-path mutators. Use when value is a flat object
// (no nested arrays/objects that need their own copy) — e.g. inline mark records like
// { bold: true, italic: false, fontFamily: 'Arial' }. Falls back to clone for nested data.
export function shallowClone(value) {
    if (value === undefined || value === null) return value;
    if (Array.isArray(value)) {
        const arr = new Array(value.length);
        for (let i = 0; i < value.length; i++) arr[i] = value[i];
        return arr;
    }
    if (typeof value !== 'object') return value;
    const copy = {};
    for (const k in value) {
        if (Object.prototype.hasOwnProperty.call(value, k)) copy[k] = value[k];
    }
    return copy;
}

export function read(value, pascalKey, camelKey, fallback) {
    if (hasOwn(value, camelKey)) return value[camelKey];
    if (hasOwn(value, pascalKey)) return value[pascalKey];
    return fallback;
}

export function stableId(prefix, path) {
    return String(prefix || 'id') + '-' + String(path || '0').replace(/[^a-z0-9_-]+/gi, '-');
}

export function sortObject(value) {
    if (Array.isArray(value)) return value.map(sortObject);
    if (!value || typeof value !== 'object') return value;
    const result = {};
    Object.keys(value).sort().forEach(function (key) {
        if (key.indexOf('__dom') === 0 || key.indexOf('__runtime') === 0 || key.indexOf('_runtime') === 0) return;
        result[key] = sortObject(value[key]);
    });
    return result;
}

export function asArray(value) {
    return Array.isArray(value) ? value : [];
}

export function asText(value) {
    return value === undefined || value === null ? '' : String(value);
}

export function textFromRuns(runs) {
    return asArray(runs).map(function (run) {
        return run.kind === 'text' || run.kind === 'token' || run.kind === 'field'
            ? asText(run.text || run.fallbackText || run.key)
            : '';
    }).join('');
}

export function unique(values) {
    return Array.from(new Set(asArray(values).filter(function (value) {
        return value !== undefined && value !== null && value !== '';
    })));
}

// Aggregated default export so consumers can do
//   import helpers from '.../core/helpers.mjs';
// and call helpers.clone(x) — useful when a single namespace import keeps call-sites short.
export default Object.freeze({
    hasOwn,
    clone,
    shallowClone,
    read,
    stableId,
    sortObject,
    asArray,
    asText,
    textFromRuns,
    unique,
});
