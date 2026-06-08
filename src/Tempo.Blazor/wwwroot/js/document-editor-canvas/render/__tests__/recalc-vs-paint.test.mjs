import assert from 'node:assert/strict';
import test from 'node:test';
import { createCanvasStack } from '../canvas-stack.mjs';

// Phase 2 (perf+rendering fix 2026-06-08): scrolling must paint visible pages from the cached
// render plan WITHOUT re-running the document layout / display list. The strongest proof is that
// `repaint` returns the exact same display-list object that `render` produced, while still
// re-planning virtualization for the new viewport.

test('repaint reuses the cached display list (no relayout) but re-plans virtualization per viewport', () => {
    const document = createFakeDocument();
    const host = document.createElement('main');
    const stack = createCanvasStack({ document, pixelRatioProvider: () => 1 });
    stack.mount(host);

    const layout = createLayout();
    const model = createLongModel(90);

    const rendered = stack.render(layout, model, { viewport: { scrollTop: 0, height: 900 } });
    assert.ok(rendered.displayList, 'render must produce a display list');
    assert.deepEqual(rendered.virtualization.visiblePageIndexes, [0, 1]);

    // Build the display list exactly once: the layout object captured at render time must be the
    // very same instance after any number of repaints.
    const layoutInstance = rendered.displayList.layout;

    let last = null;
    for (let i = 0; i < 5; i += 1) {
        last = stack.repaint({ viewport: { scrollTop: 1123 * 4, height: 900 } });
        assert.ok(last, 'repaint must return a paint result');
        assert.strictEqual(last.displayList, rendered.displayList, 'repaint must not rebuild the display list');
        assert.strictEqual(last.displayList.layout, layoutInstance, 'repaint must reuse the cached layout');
    }

    // The viewport changed, so different pages are now visible even though nothing was re-laid-out.
    assert.deepEqual(last.virtualization.visiblePageIndexes, [2, 3, 4, 5]);
    assert.notDeepEqual(last.virtualization.visiblePageIndexes, rendered.virtualization.visiblePageIndexes);

    // Scrolling back home re-mounts page 0 from the same cached plan.
    const home = stack.repaint({ viewport: { scrollTop: 0, height: 900 } });
    assert.strictEqual(home.displayList, rendered.displayList);
    assert.deepEqual(home.virtualization.visiblePageIndexes, [0, 1]);
});

test('repainting the same viewport reuses painted pages (tile cache hit, no re-clear)', () => {
    const document = createFakeDocument();
    const stack = createCanvasStack({ document, pixelRatioProvider: () => 1 });
    stack.mount(document.createElement('main'));
    stack.render(createLayout(), createLongModel(30), { viewport: { scrollTop: 0, height: 900 } });

    const contentCanvas = stack.pages.get('0').layers.get('content');
    const clearsAfterRender = contentCanvas.context.calls.filter(call => call.name === 'clearRect').length;
    assert.ok(clearsAfterRender >= 1, 'first render paints (clears) the content layer');

    // Repaint with the SAME viewport: the page is unchanged so the tile cache must skip repainting it.
    stack.repaint({ viewport: { scrollTop: 0, height: 900 } });
    const clearsAfterRepaint = stack.pages.get('0').layers.get('content').context.calls.filter(call => call.name === 'clearRect').length;
    assert.equal(clearsAfterRepaint, clearsAfterRender, 'an unchanged page must not be re-cleared/re-painted on repaint');
});

test('repaint before any render returns null (no cached plan)', () => {
    const document = createFakeDocument();
    const stack = createCanvasStack({ document, pixelRatioProvider: () => 1 });
    stack.mount(document.createElement('main'));
    assert.equal(stack.repaint({ viewport: { scrollTop: 0, height: 900 } }), null);
});

function createLayout() {
    return {
        pageSettings: { width: 794, height: 1123, marginTop: 72, marginRight: 72, marginBottom: 72, marginLeft: 72 },
    };
}

function createLongModel(blockCount) {
    return {
        documentId: 'phase2-recalc-vs-paint',
        body: {
            blocks: Array.from({ length: blockCount }, (_, index) => ({
                id: `p${index}`,
                type: 'paragraph',
                content: {
                    runs: [{
                        id: `p${index}-run`,
                        type: 'text',
                        text: `Paragraph ${index + 1} keeps enough deterministic words to flow through multiple virtualized pages and exercise the paint-only scroll path.`,
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
            return normalized === 'CANVAS' ? new FakeCanvasElement(this) : new FakeElement(this, normalized);
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

    remove() {
        this.parentNode?.removeChild(this);
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

    removeAttribute(name) {
        this.attributes.delete(String(name));
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

    save() {}
    restore() {}
    beginPath() {}
    rect() {}
    clip() {}
    clearRect() { this.calls.push({ name: 'clearRect' }); }
    fillRect() {}
    strokeRect() {}
    fillText() {}
    measureText(text) { return { width: String(text).length * 7 }; }
    setTransform() {}
    translate() {}
    scale() {}
    rotate() {}
    set fillStyle(_value) {}
    get fillStyle() { return '#000'; }
    set strokeStyle(_value) {}
    get strokeStyle() { return '#000'; }
    set font(_value) {}
    get font() { return '12px Arial'; }
    set textBaseline(_value) {}
    set textAlign(_value) {}
    set globalAlpha(_value) {}
    get globalAlpha() { return 1; }
    set lineWidth(_value) {}
}

function findOne(node, predicate) {
    for (const child of node.children || []) {
        if (predicate(child)) {
            return child;
        }

        const nested = findOne(child, predicate);
        if (nested) {
            return nested;
        }
    }

    return null;
}
