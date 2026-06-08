import assert from 'node:assert/strict';
import test from 'node:test';
import { createCanvasCommandRuntime } from '../../commands/dispatcher.mjs';
import { createHistoryStore } from '../../history/history-store.mjs';
import { createCanvasStack } from '../../render/canvas-stack.mjs';

test('modify style propagates to every block using it and supports undo redo', () => {
    let model = createModel();
    let selection = range('heading-a');
    const runtime = createCanvasCommandRuntime({
        getModel: () => model,
        getSelection: () => selection,
        history: createHistoryStore(),
        commit(change) {
            model = change.model;
            selection = change.selection;
        },
    });

    const result = runtime.execCommand('modifyStyle', {
        id: 'heading-1',
        name: 'Heading 1',
        basedOn: 'normal',
        headingLevel: 1,
        outlineLevel: 1,
        characterFormat: { fontSize: 26, fontWeight: '700' },
        paragraphFormat: { spacingAfter: 18 },
    });

    assert.equal(result.handled, true);
    assert.equal(result.result.changed, true);
    assert.deepEqual(result.result.dirtyBlockIds.sort(), ['heading-a', 'heading-b']);
    assert.equal(model.styles.find(style => style.id === 'heading-1').characterFormat.fontSize, 26);
    assert.equal(model.outlineRevision, 1);
    assert.equal(model.tableOfContentsRevision, 1);

    runtime.execCommand('undo');
    assert.equal(model.styles.find(style => style.id === 'heading-1').characterFormat.fontSize, 20);

    runtime.execCommand('redo');
    assert.equal(model.styles.find(style => style.id === 'heading-1').characterFormat.fontSize, 26);
});

test('style management creates renames deletes and resets direct formatting', () => {
    let model = createModel();
    model.body.blocks[0].paragraphProperties = { leftIndent: 24, spacingAfter: 12 };
    let selection = range('heading-a');
    const runtime = createCanvasCommandRuntime({
        getModel: () => model,
        getSelection: () => selection,
        history: createHistoryStore(),
        commit(change) {
            model = change.model;
            selection = change.selection;
        },
    });

    const createResult = runtime.execCommand('createStyleFromSelection', {
        id: 'executive-accent',
        name: 'Executive Accent',
        basedOn: 'heading-1',
        characterFormat: { fontSize: 22 },
    });

    assert.equal(createResult.handled, true);
    assert.equal(createResult.result.changed, true);
    assert.equal(model.styles.some(style => style.id === 'executive-accent'), true);
    assert.equal(model.body.blocks[0].content.styleId, 'executive-accent');

    const renameResult = runtime.execCommand('renameStyle', {
        id: 'executive-accent',
        name: 'Executive Accent 2',
    });

    assert.equal(renameResult.result.changed, true);
    assert.equal(model.styles.find(style => style.id === 'executive-accent').name, 'Executive Accent 2');
    assert.equal(model.body.blocks[0].content.styleName, 'Executive Accent 2');

    const resetResult = runtime.execCommand('resetStyleFormatting');
    assert.equal(resetResult.result.changed, true);
    assert.deepEqual(model.body.blocks[0].paragraphProperties, {});

    const deleteResult = runtime.execCommand('deleteStyle', 'executive-accent');
    assert.equal(deleteResult.result.changed, true);
    assert.equal(model.styles.some(style => style.id === 'executive-accent'), false);
    assert.equal(model.body.blocks[0].content.styleId, 'heading-1');
});

test('render diagnostics and text layout use modified heading style font size', () => {
    const model = createModel();
    model.styles = [{
        id: 'heading-1',
        name: 'Heading 1',
        type: 'paragraph',
        basedOn: 'normal',
        headingLevel: 1,
        outlineLevel: 1,
        paragraphFormat: {},
        characterFormat: { fontSize: 30, fontWeight: '700' },
    }];
    const document = createFakeDocument();
    const stack = createCanvasStack({ document, pixelRatioProvider: () => 1 });
    const host = document.createElement('main');
    stack.mount(host);
    const render = stack.render({
        pageSettings: { width: 520, height: 620, marginTop: 48, marginRight: 48, marginBottom: 48, marginLeft: 48 },
        pages: [{
            index: 0,
            width: 520,
            height: 620,
            body: { x: 48, y: 48, width: 424, height: 524 },
            columns: [{ x: 48, y: 48, width: 424, height: 524 }],
        }],
        blocks: [],
        textRects: [],
    }, model);
    const page = stack.pages.get('0').pageElement;

    assert.equal(page.getAttribute('data-canvas-style-heading1-font-size'), '30');
    assert.ok(render.displayList.commands.some(command => command.type === 'textRun' && command.style.fontSize === 40));
});

function createModel() {
    return {
        documentId: 'phase-e4-styles',
        outlineRevision: 0,
        tableOfContentsRevision: 0,
        styles: [],
        body: {
            blocks: [
                heading('heading-a', 10),
                heading('heading-b', 20),
                paragraph('body-a', 30),
            ],
        },
    };
}

function heading(id, order) {
    return {
        id,
        type: 'heading',
        order,
        paragraphProperties: {},
        content: {
            type: 'heading',
            headingLevel: 1,
            outlineLevel: 1,
            styleId: 'heading-1',
            styleName: 'Heading 1',
            runs: [{ id: `${id}-run`, type: 'text', text: 'Styled heading', marks: [] }],
        },
    };
}

function paragraph(id, order) {
    return {
        id,
        type: 'paragraph',
        order,
        paragraphProperties: {},
        content: { type: 'paragraph', runs: [{ id: `${id}-run`, type: 'text', text: 'Body', marks: [] }] },
    };
}

function range(blockId) {
    return {
        anchor: { blockId, offset: 0 },
        focus: { blockId, offset: 0 },
    };
}

function createFakeDocument() {
    return {
        createElement(tagName) {
            const normalized = String(tagName).toUpperCase();
            return normalized === 'CANVAS'
                ? new FakeCanvasElement(this)
                : new FakeElement(this, normalized);
        },
    };
}

class FakeElement {
    constructor(ownerDocument, tagName) {
        this.ownerDocument = ownerDocument;
        this.tagName = tagName;
        this.children = [];
        this.attributes = new Map();
        this.style = {};
        this.parentNode = null;
        this.className = '';
        this.textContent = '';
    }

    appendChild(child) {
        child.parentNode = this;
        this.children.push(child);
        return child;
    }

    replaceChildren(...children) {
        for (const child of this.children) {
            child.parentNode = null;
        }
        this.children = [];
        for (const child of children) {
            this.appendChild(child);
        }
    }

    setAttribute(name, value) {
        this.attributes.set(String(name), String(value));
    }

    getAttribute(name) {
        return this.attributes.has(String(name)) ? this.attributes.get(String(name)) : null;
    }

    removeAttribute(name) {
        this.attributes.delete(String(name));
    }

    querySelector(selector) {
        const testId = String(selector || '').match(/^\[data-testid="([^"]+)"\]$/)?.[1];
        return testId ? findOne(this, node => node.getAttribute?.('data-testid') === testId) : null;
    }
}

class FakeCanvasElement extends FakeElement {
    constructor(ownerDocument) {
        super(ownerDocument, 'CANVAS');
        this.width = 0;
        this.height = 0;
        this.context = new FakeCanvasContext();
    }

    getContext(type) {
        assert.equal(type, '2d');
        return this.context;
    }
}

class FakeCanvasContext {
    constructor() {
        this.calls = [];
    }

    setTransform(...args) { this.calls.push({ name: 'setTransform', args }); }
    clearRect(...args) { this.calls.push({ name: 'clearRect', args }); }
    fillRect(...args) { this.calls.push({ name: 'fillRect', args }); }
    strokeRect(...args) { this.calls.push({ name: 'strokeRect', args }); }
    fillText(...args) { this.calls.push({ name: 'fillText', args }); }
    save(...args) { this.calls.push({ name: 'save', args }); }
    restore(...args) { this.calls.push({ name: 'restore', args }); }
    beginPath(...args) { this.calls.push({ name: 'beginPath', args }); }
    moveTo(...args) { this.calls.push({ name: 'moveTo', args }); }
    lineTo(...args) { this.calls.push({ name: 'lineTo', args }); }
    stroke(...args) { this.calls.push({ name: 'stroke', args }); }
    setLineDash(...args) { this.calls.push({ name: 'setLineDash', args }); }
}

function findOne(root, predicate) {
    if (predicate(root)) {
        return root;
    }

    for (const child of root.children || []) {
        const match = findOne(child, predicate);
        if (match) {
            return match;
        }
    }

    return null;
}
