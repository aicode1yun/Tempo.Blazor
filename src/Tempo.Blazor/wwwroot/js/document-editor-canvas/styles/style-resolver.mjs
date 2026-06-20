import {
    ensureStyleStore,
    findStyle,
    normalizeStyleDefinition,
    normalizeStyleKey,
} from './style-store.mjs';

export function resolveStyle(model, idOrName = 'normal', type = 'paragraph') {
    ensureStyleStore(model);
    const style = findStyle(model, idOrName, type) || findStyle(model, idOrName) || findStyle(model, 'normal', type);
    if (!style) {
        return null;
    }

    return resolveStyleNode(model, style, new Set());
}

export function resolveBlockStyleFormatting(model, block) {
    const style = resolveStyle(model, blockStyleRef(block), 'paragraph') || resolveStyle(model, 'normal', 'paragraph');
    const directParagraph = block?.paragraphProperties && typeof block.paragraphProperties === 'object'
        ? normalizeFormatBag(block.paragraphProperties)
        : {};

    return {
        style,
        paragraphFormat: {
            ...(style?.paragraphFormat || {}),
            ...directParagraph,
        },
        characterFormat: {
            ...(style?.characterFormat || {}),
        },
        directFormatting: directFormattingDelta(block, style),
    };
}

export function directFormattingDelta(block, resolvedStyle) {
    const paragraph = block?.paragraphProperties && typeof block.paragraphProperties === 'object'
        ? normalizeFormatBag(block.paragraphProperties)
        : {};
    const base = resolvedStyle?.paragraphFormat || {};
    const delta = {};
    for (const [key, value] of Object.entries(paragraph)) {
        if (normalizeValue(base[key]) !== normalizeValue(value)) {
            delta[key] = value;
        }
    }

    return delta;
}

export function blockStyleRef(block) {
    const content = block?.content || {};
    if (content.styleId) return content.styleId;
    if (content.styleName) return content.styleName;
    const type = String(block?.type || content.type || '').toLowerCase();
    if (type === 'heading') {
        const level = Math.max(1, Math.min(6, Number(content.headingLevel || content.outlineLevel || 1) || 1));
        return `heading-${level}`;
    }
    if (type === 'quote') return 'quote';
    return 'normal';
}

function resolveStyleNode(model, style, visited) {
    const normalized = normalizeStyleDefinition(style);
    if (!normalized) {
        return null;
    }

    const key = normalizeStyleKey(normalized.id || normalized.name);
    if (visited.has(key)) {
        return normalized;
    }

    visited.add(key);
    const parent = normalized.basedOn
        ? resolveStyleNode(model, findStyle(model, normalized.basedOn, normalized.type) || findStyle(model, normalized.basedOn), visited)
        : null;

    return {
        ...(parent || {}),
        ...normalized,
        paragraphFormat: {
            ...(parent?.paragraphFormat || {}),
            ...(normalized.paragraphFormat || {}),
        },
        characterFormat: {
            ...(parent?.characterFormat || {}),
            ...(normalized.characterFormat || {}),
        },
        tableFormat: {
            ...(parent?.tableFormat || {}),
            ...(normalized.tableFormat || {}),
        },
        listFormat: {
            ...(parent?.listFormat || {}),
            ...(normalized.listFormat || {}),
        },
    };
}

function normalizeFormatBag(value) {
    return Object.fromEntries(Object.entries(value || {}).map(([key, item]) => [normalizeFormatKey(key), item]));
}

function normalizeFormatKey(key) {
    const text = String(key || '');
    return `${text.charAt(0).toLowerCase()}${text.slice(1)}`;
}

function normalizeValue(value) {
    return value == null ? null : JSON.stringify(value);
}
