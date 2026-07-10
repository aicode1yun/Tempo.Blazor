import assert from 'node:assert/strict';
import test from 'node:test';
import { createCanvasStack } from '../canvas-stack.mjs';
import { pageSignature } from '../../perf/tile-cache.mjs';

// Phase N6 (canvas perf 2026-07-10): the per-page/root diagnostic attributes (dozens of
// O(commands) filters, testing contract only) are skipped on the per-keystroke input render
// (`deferPaintDiagnostics`) and published by the debounced idle reconciliation render. The
// tile-cache page signature is a rolling hash instead of a page-sized join string.

function createLayout() {
    return {
        pageSettings: { width: 794, height: 1123, marginTop: 72, marginRight: 72, marginBottom: 72, marginLeft: 72 },
    };
}

function createModel(blockCount = 3) {
    return {
        documentId: 'n6-defer',
        body: {
            blocks: Array.from({ length: blockCount }, (_, index) => ({
                id: `p${index}`,
                type: 'paragraph',
                content: { runs: [{ id: `p${index}-run`, type: 'text', text: `Deterministic defer paragraph ${index + 1}.` }] },
            })),
        },
    };
}

test('deferPaintDiagnostics skips per-page/root diagnostic attributes; a settled render publishes them', () => {
    const document = createFakeDocument();
    const stack = createCanvasStack({ document, pixelRatioProvider: () => 1 });
    stack.mount(document.createElement('main'));

    stack.render(createLayout(), createModel(), {
        viewport: { scrollTop: 0, height: 900 },
        deferPaintDiagnostics: true,
    });

    const page = stack.pages.get('0').pageElement;
    assert.equal(page.getAttribute('data-canvas-text-run-count'), null,
        'deferred input render must not compute per-page command filters');
    assert.equal(page.getAttribute('data-canvas-model-document-id'), null,
        'deferred input render must not compute model diagnostics');
    assert.equal(stack.root.getAttribute('data-canvas-tab-leader-count'), null,
        'deferred input render must not compute root command filters');
    assert.ok(stack.root.getAttribute('data-canvas-page-count'),
        'load-bearing cheap attributes stay published on every render');

    // The idle reconciliation render (no defer flag) publishes the settled diagnostics. Force the
    // repaint as the real idle pass does for dirty pages (a tile-cache hit keeps commands empty).
    stack.render(createLayout(), createModel(), { viewport: { scrollTop: 0, height: 900 }, forceRepaint: true });
    assert.ok(Number(page.getAttribute('data-canvas-text-run-count')) > 0,
        'settled render publishes per-page counts');
    assert.equal(page.getAttribute('data-canvas-model-document-id'), 'n6-defer');
    assert.equal(stack.root.getAttribute('data-canvas-tab-leader-count'), '0');
});

test('page signature: identical lists agree, text/geometry/rotation changes differ, other pages isolated', () => {
    const command = (overrides = {}) => ({
        id: 'cmd-1', type: 'textRun', layer: 'content', pageIndex: 0, blockId: 'p1',
        x: 10, y: 20, width: 100, height: 16, text: 'signature text', ...overrides,
    });
    const list = (commands) => ({ commands });

    const base = pageSignature(list([command()]), 0);
    assert.strictEqual(pageSignature(list([command()]), 0), base, 'identical command lists agree');
    assert.notStrictEqual(pageSignature(list([command({ text: 'signature texT' })]), 0), base, 'text change differs');
    assert.notStrictEqual(pageSignature(list([command({ x: 10.5 })]), 0), base, 'geometry change differs');
    assert.notStrictEqual(pageSignature(list([command({ rotation: 15 })]), 0), base, 'rotation change differs');
    assert.notStrictEqual(pageSignature(list([command(), command({ id: 'cmd-2' })]), 0), base, 'added command differs');
    assert.strictEqual(pageSignature(list([command({ pageIndex: 1 })]), 0), pageSignature(list([]), 0),
        'commands on other pages do not contribute');
    assert.strictEqual(pageSignature(list([command(), command({ id: 'sel', layer: 'selection' })]), 0), base,
        'selection-layer commands are excluded as before');
});

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
    }

    appendChild(child) { child.parentNode = this; this.children.push(child); return child; }
    insertBefore(child, reference) {
        child.parentNode = this;
        const index = this.children.indexOf(reference);
        if (index < 0) this.children.push(child); else this.children.splice(index, 0, child);
        return child;
    }
    removeChild(child) { this.children = this.children.filter(item => item !== child); child.parentNode = null; return child; }
    remove() { this.parentNode?.removeChild(this); }
    replaceChildren(...children) { this.children = [...children]; }
    setAttribute(name, value) { this.attributes.set(String(name), String(value)); }
    removeAttribute(name) { this.attributes.delete(String(name)); }
    getAttribute(name) { return this.attributes.has(String(name)) ? this.attributes.get(String(name)) : null; }
}

class FakeCanvasElement extends FakeElement {
    constructor(ownerDocument) {
        super(ownerDocument, 'CANVAS');
        this.width = 0;
        this.height = 0;
        this.context = new RecordingContext();
    }

    getContext() { return this.context; }
}

class RecordingContext {
    save() {}
    restore() {}
    beginPath() {}
    rect() {}
    clip() {}
    clearRect() {}
    fillRect() {}
    strokeRect() {}
    fillText() {}
    measureText(text) { return { width: String(text).length * 7 }; }
    setTransform() {}
    translate() {}
    scale() {}
    rotate() {}
    set fillStyle(_v) {}
    get fillStyle() { return '#000'; }
    set strokeStyle(_v) {}
    get strokeStyle() { return '#000'; }
    set font(_v) {}
    get font() { return '12px Arial'; }
    set textBaseline(_v) {}
    set textAlign(_v) {}
    set globalAlpha(_v) {}
    get globalAlpha() { return 1; }
    set lineWidth(_v) {}
}
