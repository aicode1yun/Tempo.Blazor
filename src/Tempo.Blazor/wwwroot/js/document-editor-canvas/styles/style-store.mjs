export const BUILT_IN_STYLE_DEFINITIONS = Object.freeze([
    paragraphStyle({
        id: 'normal',
        name: 'Normal',
        basedOn: null,
        next: 'normal',
        isQuickStyle: true,
        isPrimary: true,
        paragraphFormat: {
            alignment: 0,
            lineSpacing: 1,
            spacingBefore: 0,
            spacingAfter: 8,
        },
        characterFormat: {
            fontSize: 11,
            fontWeight: '400',
            fontStyle: 'normal',
        },
    }),
    paragraphStyle({
        id: 'title',
        name: 'Title',
        basedOn: 'normal',
        next: 'normal',
        isQuickStyle: true,
        isPrimary: true,
        headingLevel: 1,
        outlineLevel: 1,
        paragraphFormat: {
            spacingBefore: 0,
            spacingAfter: 14,
        },
        characterFormat: {
            fontSize: 28,
            fontWeight: '700',
        },
    }),
    headingStyle(1, 'normal', 20, 14),
    headingStyle(2, 'heading-1', 16, 12),
    headingStyle(3, 'heading-2', 14, 10),
    headingStyle(4, 'heading-3', 12.5, 8),
    headingStyle(5, 'heading-4', 11.5, 6),
    headingStyle(6, 'heading-5', 11, 6),
    paragraphStyle({
        id: 'quote',
        name: 'Quote',
        basedOn: 'normal',
        next: 'normal',
        isQuickStyle: true,
        isPrimary: true,
        paragraphFormat: {
            leftIndent: 36,
            spacingBefore: 6,
            spacingAfter: 10,
        },
        characterFormat: {
            fontStyle: 'italic',
        },
    }),
    {
        id: 'strong',
        name: 'Strong',
        type: 'character',
        basedOn: null,
        next: null,
        isQuickStyle: true,
        isPrimary: false,
        headingLevel: null,
        outlineLevel: null,
        paragraphFormat: {},
        characterFormat: { fontWeight: '700' },
        tableFormat: {},
        listFormat: {},
    },
    {
        id: 'table-grid',
        name: 'Table Grid',
        type: 'table',
        basedOn: null,
        next: null,
        isQuickStyle: true,
        isPrimary: false,
        headingLevel: null,
        outlineLevel: null,
        paragraphFormat: {},
        characterFormat: {},
        tableFormat: { borderStyle: 'single', borderWidth: 1 },
        listFormat: {},
    },
    {
        id: 'bullet-list',
        name: 'Bullet List',
        type: 'list',
        basedOn: 'normal',
        next: 'bullet-list',
        isQuickStyle: true,
        isPrimary: false,
        headingLevel: null,
        outlineLevel: null,
        paragraphFormat: { leftIndent: 24 },
        characterFormat: {},
        tableFormat: {},
        listFormat: { ordered: false, numberFormat: 'bullet' },
    },
]);

export function ensureStyleStore(model) {
    if (!model || typeof model !== 'object') {
        return [];
    }

    const existing = Array.isArray(model.styles) ? model.styles.map(normalizeStyleDefinition).filter(Boolean) : [];
    const byKey = new Map(existing.flatMap(style => styleKeys(style).map(key => [key, style])));
    const merged = existing.slice();

    for (const builtIn of BUILT_IN_STYLE_DEFINITIONS) {
        const normalized = normalizeStyleDefinition(builtIn);
        if (!normalized) {
            continue;
        }

        const hasExisting = styleKeys(normalized).some(key => byKey.has(key));
        if (!hasExisting) {
            merged.push(clone(normalized));
        }
    }

    model.styles = merged;
    return model.styles;
}

export function quickStyles(model) {
    return ensureStyleStore(model)
        .filter(style => style.isQuickStyle === true || style.isPrimary === true)
        .sort((left, right) => styleSortKey(left).localeCompare(styleSortKey(right)));
}

export function findStyle(model, idOrName, type = null) {
    const key = normalizeStyleKey(idOrName);
    if (!key) {
        return ensureStyleStore(model).find(style => style.id === 'normal') || null;
    }

    return ensureStyleStore(model).find(style => {
        const sameType = !type || String(style.type || '').toLowerCase() === String(type).toLowerCase();
        return sameType && styleKeys(style).includes(key);
    }) || null;
}

export function upsertStyle(model, style) {
    const normalized = normalizeStyleDefinition(style);
    if (!normalized || !model || typeof model !== 'object') {
        return { changed: false, style: null };
    }

    const styles = ensureStyleStore(model);
    const index = styles.findIndex(candidate => styleKeys(candidate).some(key => styleKeys(normalized).includes(key)));
    if (index < 0) {
        styles.push(normalized);
        return { changed: true, style: normalized };
    }

    const before = JSON.stringify(styles[index]);
    styles[index] = {
        ...styles[index],
        ...normalized,
        paragraphFormat: { ...(styles[index].paragraphFormat || {}), ...(normalized.paragraphFormat || {}) },
        characterFormat: { ...(styles[index].characterFormat || {}), ...(normalized.characterFormat || {}) },
        tableFormat: { ...(styles[index].tableFormat || {}), ...(normalized.tableFormat || {}) },
        listFormat: { ...(styles[index].listFormat || {}), ...(normalized.listFormat || {}) },
    };

    return { changed: before !== JSON.stringify(styles[index]), style: styles[index] };
}

export function renameStyle(model, idOrName, nextName) {
    const style = findStyle(model, idOrName);
    const name = String(nextName || '').trim();
    if (!style || !name) {
        return { changed: false, style: null, previousName: null };
    }

    const previousName = style.name;
    if (previousName === name) {
        return { changed: false, style, previousName };
    }

    style.name = name;
    return { changed: true, style, previousName };
}

export function deleteStyle(model, idOrName) {
    const styles = ensureStyleStore(model);
    const requestedKey = normalizeStyleKey(idOrName);
    const style = styles.find(candidate => styleKeys(candidate).includes(requestedKey)) || null;
    if (!style || isBuiltInStyle(style.id)) {
        return { changed: false, style: null };
    }

    const keys = styleKeys(style);
    const index = styles.findIndex(candidate => styleKeys(candidate).some(key => keys.includes(key)));
    if (index < 0) {
        return { changed: false, style: null };
    }

    styles.splice(index, 1);
    return { changed: true, style };
}

export function normalizeStyleDefinition(style) {
    if (!style || typeof style !== 'object') {
        return null;
    }

    const name = String(style.name ?? style.Name ?? style.styleName ?? style.StyleName ?? '').trim();
    const id = String(style.id ?? style.Id ?? style.styleId ?? style.StyleId ?? slugStyleName(name)).trim();
    if (!id && !name) {
        return null;
    }

    const normalizedName = name || humanizeStyleId(id);
    return {
        id: id || slugStyleName(normalizedName),
        name: normalizedName,
        type: normalizeStyleType(style.type ?? style.Type),
        basedOn: normalizeNullableStyleRef(style.basedOn ?? style.BasedOn),
        next: normalizeNullableStyleRef(style.next ?? style.Next),
        isQuickStyle: style.isQuickStyle === true || style.IsQuickStyle === true,
        isPrimary: style.isPrimary === true || style.IsPrimary === true,
        headingLevel: nullablePositiveInt(style.headingLevel ?? style.HeadingLevel),
        outlineLevel: nullablePositiveInt(style.outlineLevel ?? style.OutlineLevel),
        paragraphFormat: normalizeFormatBag(style.paragraphFormat ?? style.ParagraphFormat),
        characterFormat: normalizeFormatBag(style.characterFormat ?? style.CharacterFormat),
        tableFormat: normalizeFormatBag(style.tableFormat ?? style.TableFormat),
        listFormat: normalizeFormatBag(style.listFormat ?? style.ListFormat),
    };
}

export function styleKeys(style) {
    return [
        normalizeStyleKey(style?.id),
        normalizeStyleKey(style?.name),
    ].filter(Boolean);
}

export function normalizeStyleKey(value) {
    return String(value || '').replace(/[\s_-]/g, '').toLowerCase();
}

export function isBuiltInStyle(idOrName) {
    const key = normalizeStyleKey(idOrName);
    return BUILT_IN_STYLE_DEFINITIONS.some(style => styleKeys(style).includes(key));
}

function headingStyle(level, basedOn, fontSize, spacingBefore) {
    return paragraphStyle({
        id: `heading-${level}`,
        name: `Heading ${level}`,
        basedOn,
        next: 'normal',
        isQuickStyle: true,
        isPrimary: true,
        headingLevel: level,
        outlineLevel: level,
        paragraphFormat: {
            spacingBefore,
            spacingAfter: Math.max(4, 12 - level),
        },
        characterFormat: {
            fontSize,
            fontWeight: '700',
        },
    });
}

function paragraphStyle(style) {
    return Object.freeze({
        type: 'paragraph',
        headingLevel: null,
        outlineLevel: null,
        paragraphFormat: {},
        characterFormat: {},
        tableFormat: {},
        listFormat: {},
        ...style,
    });
}

function normalizeStyleType(value) {
    const normalized = String(value || 'paragraph').replace(/[\s_-]/g, '').toLowerCase();
    if (normalized === 'character' || normalized === 'char') return 'character';
    if (normalized === 'table') return 'table';
    if (normalized === 'list' || normalized === 'numbering') return 'list';
    return 'paragraph';
}

function normalizeNullableStyleRef(value) {
    const text = String(value ?? '').trim();
    return text.length > 0 ? text : null;
}

function normalizeFormatBag(value) {
    if (!value || typeof value !== 'object' || Array.isArray(value)) {
        return {};
    }

    return Object.fromEntries(Object.entries(value).filter(([key]) => String(key || '').trim().length > 0));
}

function nullablePositiveInt(value) {
    const number = Number(value);
    return Number.isFinite(number) && number > 0 ? Math.trunc(number) : null;
}

function slugStyleName(value) {
    return String(value || '').trim().replace(/([a-z])([A-Z])/g, '$1-$2').replace(/[\s_]+/g, '-').toLowerCase();
}

function humanizeStyleId(value) {
    const words = String(value || 'Normal').replace(/[-_]+/g, ' ').trim();
    return words.replace(/\b\w/g, letter => letter.toUpperCase());
}

function styleSortKey(style) {
    const id = String(style?.id || '');
    if (id === 'normal') return '00';
    if (id === 'title') return '01';
    const heading = id.match(/^heading-(\d)$/);
    if (heading) return `1${heading[1]}`;
    if (id === 'quote') return '30';
    return `9-${String(style?.name || id)}`;
}

function clone(value) {
    if (typeof structuredClone === 'function') {
        return structuredClone(value);
    }

    return JSON.parse(JSON.stringify(value ?? null));
}
