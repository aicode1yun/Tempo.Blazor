import assert from 'node:assert/strict';
import test from 'node:test';
import { createPageVirtualizer, visibleIndexes } from '../page-virtualizer.mjs';
import { createTileCache, pageSignature } from '../tile-cache.mjs';
import { createCanvasStack } from '../../render/canvas-stack.mjs';

test('page virtualizer returns only visible pages plus buffer and keeps document spacers', () => {
    const pages = Array.from({ length: 10 }, (_, index) => ({
        index,
        y: index * 1123,
        width: 794,
        height: 1123,
    }));

    assert.deepEqual(visibleIndexes(pages, { scrollTop: 0, height: 900 }, 1), [0, 1]);

    const virtualizer = createPageVirtualizer({ bufferPages: 1 });
    const plan = virtualizer.plan(pages, { scrollTop: 1123 * 4, height: 900 });

    assert.deepEqual(plan.visiblePageIndexes, [2, 3, 4, 5]);
    assert.equal(plan.pages.length, 4);
    assert.equal(plan.progressive, true);
    assert.ok(plan.topSpacerHeight > 0);
    assert.ok(plan.bottomSpacerHeight > 0);
});

test('tile cache invalidates dirty pages without treating overlay-only commands as content changes', () => {
    const cache = createTileCache({ maxEntries: 2 });
    const displayList = createDisplayList('Hello');
    const first = cache.shouldRepaint(0, displayList);
    cache.commitPage(0, first.signature);

    const overlayOnly = {
        ...displayList,
        commands: [
            ...displayList.commands,
            { id: 'caret', type: 'caret', layer: 'selection-caret', pageIndex: 0, x: 10, y: 20, width: 1, height: 18 },
        ],
    };
    const cached = cache.shouldRepaint(0, overlayOnly);
    const dirty = cache.shouldRepaint(0, overlayOnly, { dirtyPageIndexes: [0] });
    const changed = cache.shouldRepaint(0, createDisplayList('Changed'));

    assert.equal(first.repaint, true);
    assert.equal(cached.repaint, false);
    assert.equal(dirty.repaint, true);
    assert.equal(changed.repaint, true);
    assert.equal(pageSignature(displayList, 0), pageSignature(overlayOnly, 0));
});

test('tile cache enforces bounded page entries with LRU eviction', () => {
    const cache = createTileCache({ maxEntries: 2 });
    for (let pageIndex = 0; pageIndex < 3; pageIndex += 1) {
        const decision = cache.shouldRepaint(pageIndex, {
            commands: [{ id: `p${pageIndex}`, type: 'textRun', pageIndex, text: String(pageIndex) }],
        });
        cache.commitPage(pageIndex, decision.signature);
    }

    const snapshot = cache.snapshot();
    assert.equal(snapshot.entryCount, 2);
    assert.equal(snapshot.evictions, 1);
});

test('virtualized page remount repaints new canvases even when tile signature is cached', () => {
    const document = createFakeDocument();
    const host = document.createElement('main');
    const stack = createCanvasStack({ document, pixelRatioProvider: () => 1 });
    stack.mount(host);
    const layout = createLayout();
    const model = createLongModel(90);

    stack.render(layout, model, { viewport: { scrollTop: 0, height: 900 } });
    const initialPage = stack.pages.get('0');
    assert.ok(initialPage.layers.get('page-background').context.calls.some(call => call.name === 'fillRect'));

    stack.render(layout, model, { viewport: { scrollTop: 1123 * 6, height: 900 } });
    assert.equal(stack.pages.has('0'), false);

    stack.render(layout, model, { viewport: { scrollTop: 0, height: 900 } });
    const remountedPage = stack.pages.get('0');
    assert.notEqual(remountedPage, initialPage);
    assert.ok(remountedPage.layers.get('page-background').context.calls.some(call => call.name === 'fillRect'));
});

function createDisplayList(text) {
    return {
        commands: [
            { id: 'p0-box', type: 'paragraphBox', pageIndex: 0, blockId: 'p0', x: 80, y: 80, width: 420, height: 24 },
            { id: 'p0-run', type: 'textRun', pageIndex: 0, blockId: 'p0', text, x: 84, y: 84, width: 120, height: 18 },
        ],
    };
}

function createLayout() {
    return {
        pageSettings: { width: 794, height: 1123, marginTop: 72, marginRight: 72, marginBottom: 72, marginLeft: 72 },
    };
}

function createLongModel(blockCount) {
    return {
        documentId: 'phase-22-cache-remount',
        body: {
            blocks: Array.from({ length: blockCount }, (_, index) => ({
                id: `p${index}`,
                type: 'paragraph',
                content: {
                    runs: [{
                        id: `p${index}-run`,
                        type: 'text',
                        text: `Paragraph ${index + 1} keeps enough deterministic words to flow through multiple virtualized pages and exercise cache remount rendering.`,
                    }],
                },
            })),
        },
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

    insertBefore(child, reference) {
        child.parentNode = this;
        const index = this.children.indexOf(reference);
        if (index < 0) {
            this.children.push(child);
        } else {
            this.children.splice(index, 0, child);
        }

        return child;
    }

    removeChild(child) {
        this.children = this.children.filter(item => item !== child);
        child.parentNode = null;
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
        this.context = new RecordingContext();
    }

    getContext(type) {
        assert.equal(type, '2d');
        return this.context;
    }
}

class RecordingContext {
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
        const found = findOne(child, predicate);
        if (found) {
            return found;
        }
    }

    return null;
}
