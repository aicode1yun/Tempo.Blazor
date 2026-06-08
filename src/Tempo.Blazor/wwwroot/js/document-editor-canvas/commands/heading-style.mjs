import { BUILT_IN_STYLE_DEFINITIONS, findStyle, normalizeStyleKey } from '../styles/style-store.mjs';
import { blockStyleRef, resolveStyle } from '../styles/style-resolver.mjs';

export const BLOCK_STYLE_DEFINITIONS = Object.freeze(Object.fromEntries(
    BUILT_IN_STYLE_DEFINITIONS
        .filter(style => style.type === 'paragraph')
        .map(style => [normalizeStyleKey(style.id), Object.freeze(blockStyleDefinition(style))])
));

export function resolveBlockStyle(styleName, model = null) {
    const style = model
        ? resolveStyle(model, normalizeHeadingArgument(styleName), 'paragraph')
        : findBuiltInStyle(styleName);

    return style ? blockStyleDefinition(style) : null;
}

export function applyBlockStyleToBlock(block, styleName, model = null) {
    const definition = resolveBlockStyle(styleName, model);
    if (!definition || !isEditableTextBlock(block)) {
        return { changed: false, block };
    }

    const nextBlock = clone(block);
    const content = nextBlock.content && typeof nextBlock.content === 'object'
        ? { ...nextBlock.content }
        : { runs: [] };
    const before = JSON.stringify({
        type: nextBlock.type,
        contentType: content.type,
        headingLevel: content.headingLevel ?? null,
        styleId: content.styleId ?? null,
        styleName: content.styleName ?? null,
        outlineLevel: content.outlineLevel ?? null,
        list: content.list ?? null,
    });

    nextBlock.type = definition.type;
    content.type = definition.type;
    content.headingLevel = definition.headingLevel;
    content.styleId = definition.id;
    content.styleName = definition.name;
    content.outlineLevel = definition.outlineLevel;
    content.list = definition.type === 'list' ? content.list : null;
    nextBlock.content = content;

    return {
        changed: JSON.stringify({
            type: nextBlock.type,
            contentType: content.type,
            headingLevel: content.headingLevel ?? null,
            styleId: content.styleId ?? null,
            styleName: content.styleName ?? null,
            outlineLevel: content.outlineLevel ?? null,
            list: content.list ?? null,
        }) !== before,
        block: nextBlock,
        style: definition,
    };
}

export function blockStyleState(block, model = null) {
    if (!isEditableTextBlock(block)) {
        return resolveBlockStyle('Normal', model) || BLOCK_STYLE_DEFINITIONS.normal;
    }

    return resolveBlockStyle(blockStyleRef(block), model)
        || resolveBlockStyle('Normal', model)
        || BLOCK_STYLE_DEFINITIONS.normal;
}

export function invalidatesOutlineCache(previousBlocks, nextBlocks) {
    const previous = outlineSignature(previousBlocks);
    const next = outlineSignature(nextBlocks);
    return previous !== next;
}

function blockStyleDefinition(style) {
    const id = String(style?.id || 'normal');
    const name = String(style?.name || 'Normal');
    const headingLevel = style?.headingLevel ?? null;
    const isQuote = normalizeStyleKey(id) === 'quote' || normalizeStyleKey(name) === 'quote';
    const type = headingLevel ? 'heading' : isQuote ? 'quote' : 'paragraph';

    return {
        id,
        name,
        basedOn: displayStyleReference(style?.basedOn ?? null),
        next: style?.next ?? null,
        type,
        headingLevel,
        outlineLevel: style?.outlineLevel ?? (headingLevel || null),
        directFormatting: false,
        paragraphFormat: { ...(style?.paragraphFormat || {}) },
        characterFormat: { ...(style?.characterFormat || {}) },
    };
}

function findBuiltInStyle(styleName) {
    const requested = normalizeHeadingArgument(styleName);
    const key = normalizeStyleKey(requested);
    return BUILT_IN_STYLE_DEFINITIONS.find(style => [style.id, style.name].some(value => normalizeStyleKey(value) === key))
        || BUILT_IN_STYLE_DEFINITIONS.find(style => style.id === 'normal')
        || null;
}

function normalizeHeadingArgument(value) {
    const text = String(value || 'Normal').trim();
    const compact = normalizeStyleKey(text);
    const heading = compact.match(/^heading([1-6])$/);
    return heading ? `Heading ${heading[1]}` : text;
}

function displayStyleReference(value) {
    const text = String(value || '').trim();
    const heading = normalizeStyleKey(text).match(/^heading([1-6])$/);
    if (heading) {
        return `Heading ${heading[1]}`;
    }

    return normalizeStyleKey(text) === 'normal' ? 'Normal' : (text || null);
}

function isEditableTextBlock(block) {
    const type = String(block?.type || block?.content?.type || '').toLowerCase();
    return type === 'paragraph' || type === 'heading' || type === 'list' || type === 'quote';
}

function outlineSignature(blocks) {
    return (Array.isArray(blocks) ? blocks : [])
        .map(block => {
            const content = block?.content || {};
            return [
                block?.id || '',
                block?.type || '',
                content.type || '',
                content.headingLevel ?? '',
                content.outlineLevel ?? '',
                content.styleId ?? '',
                content.styleName ?? '',
                textSignature(content.runs),
            ].join(':');
        })
        .join('|');
}

function textSignature(runs) {
    return (Array.isArray(runs) ? runs : [])
        .map(run => String(run?.text || ''))
        .join('');
}

function clone(value) {
    if (typeof structuredClone === 'function') {
        return structuredClone(value);
    }

    return JSON.parse(JSON.stringify(value ?? null));
}
