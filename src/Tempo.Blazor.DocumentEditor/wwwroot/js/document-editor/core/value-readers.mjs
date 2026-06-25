// Phase D — core/value-readers.mjs
// Small permissive value readers used across the import/export pipeline.

// Read the first non-null/undefined boolean-coerceable value from `source` for any of
// `keys`. Accepts: real booleans, numbers (0 → false, else true), and strings
// `true/false/1/0/yes/no/on/off` (case-insensitive). Returns `null` if no key matches
// or all values are null/undefined.
export function readOptionalBoolean(source, keys) {
    const valueSource = source || {};
    for (let i = 0; i < keys.length; i++) {
        const key = keys[i];
        if (!Object.prototype.hasOwnProperty.call(valueSource, key)) continue;
        const value = valueSource[key];
        if (value === null || value === undefined) continue;
        if (typeof value === 'boolean') return value;
        if (typeof value === 'number') return value !== 0;
        const text = String(value).trim().toLowerCase();
        if (text === 'true' || text === '1' || text === 'yes' || text === 'on') return true;
        if (text === 'false' || text === '0' || text === 'no' || text === 'off') return false;
    }
    return null;
}
