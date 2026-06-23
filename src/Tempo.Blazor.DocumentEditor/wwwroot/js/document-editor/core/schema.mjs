// Phase D — core/schema.mjs
// DocumentSchemaRegistry + default schema. Pure (no closure over engine state).
// Mirrors the inline definition in the legacy IIFE so the bundled engine and the legacy
// monolith agree on the schema element/child/attribute tables.

export class DocumentSchemaRegistry {
    constructor() {
        this.elements = new Map();
        this.children = new Map();
        this.attributes = new Map();
    }

    registerElement(type, definition) {
        this.elements.set(type, Object.assign({
            type,
            isBlock: false,
            isInline: false,
            isObject: false,
            isLimit: false,
            isSelectable: false,
        }, definition || {}));
        return this;
    }

    allowChild(parentType, childType) {
        if (!this.children.has(parentType)) this.children.set(parentType, new Set());
        this.children.get(parentType).add(childType);
        return this;
    }

    allowAttribute(type, attributeName) {
        if (!this.attributes.has(type)) this.attributes.set(type, new Set());
        this.attributes.get(type).add(attributeName);
        return this;
    }

    getDefinition(type) {
        return this.elements.get(type) || null;
    }

    checkChild(parent, childType) {
        const parentType = typeof parent === 'string' ? parent : parent && (parent.type || parent.kind);
        return !!parentType && this.children.has(parentType) && this.children.get(parentType).has(childType);
    }

    checkAttribute(element, attributeName) {
        const type = typeof element === 'string' ? element : element && (element.type || element.kind);
        return !!type && this.attributes.has(type) && this.attributes.get(type).has(attributeName);
    }

    getLimitElement(position) {
        let current = position && position.element;
        while (current) {
            const definition = this.getDefinition(current.type || current.kind);
            if (definition && definition.isLimit) return current;
            current = current.parent || null;
        }
        return null;
    }

    getNearestSelectionRange(position, direction) {
        const limit = this.getLimitElement(position);
        return {
            limitElementId: limit ? limit.id : null,
            direction: direction || 'forward',
            start: position || null,
            end: position || null,
        };
    }
}

export function createDefaultSchemaRegistry() {
    const schema = new DocumentSchemaRegistry();
    schema
        .registerElement('document', { isLimit: true })
        .registerElement('body', { isLimit: true })
        .registerElement('header', { isLimit: true })
        .registerElement('footer', { isLimit: true })
        .registerElement('paragraph', { isBlock: true })
        .registerElement('table', { isBlock: true, isObject: true, isSelectable: true })
        .registerElement('tableRow', { isLimit: true })
        .registerElement('tableCell', { isLimit: true })
        .registerElement('caption', { isLimit: true })
        .registerElement('text', { isInline: true })
        .registerElement('field', { isInline: true, isObject: true, isSelectable: true })
        .registerElement('token', { isInline: true, isObject: true, isSelectable: true })
        .registerElement('drawing', { isInline: true, isObject: true, isSelectable: true })
        .registerElement('image', { isBlock: true, isObject: true, isSelectable: true });

    for (const child of ['paragraph', 'table', 'image']) {
        schema.allowChild('body', child);
        schema.allowChild('header', child);
        schema.allowChild('footer', child);
        schema.allowChild('tableCell', child);
    }
    for (const child of ['text', 'field', 'token', 'drawing']) {
        schema.allowChild('paragraph', child);
    }
    schema
        .allowChild('image', 'caption')
        .allowChild('table', 'tableRow')
        .allowChild('tableRow', 'tableCell');

    for (const type of ['paragraph', 'text', 'field', 'token', 'drawing', 'image', 'table', 'tableCell']) {
        for (const attribute of ['style', 'marks', 'revisionId', 'commentIds', 'layout', 'metadata']) {
            schema.allowAttribute(type, attribute);
        }
    }

    return schema;
}
