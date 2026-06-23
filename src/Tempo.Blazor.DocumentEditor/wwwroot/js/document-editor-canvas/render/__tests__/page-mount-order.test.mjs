import assert from 'node:assert/strict';
import test from 'node:test';
import { createCanvasStack } from '../canvas-stack.mjs';

// B5 (UX fix 2026-06-11): mounted canvas pages must keep ASCENDING DOM order regardless of scroll
// direction. The bug: `ensurePage` always inserted a newly mounted page before the bottom spacer, so
// scrolling UP (which mounts a lower page while a higher page is still mounted) produced DOM order
// [.., higher, lower] — the user saw page 0 rendered below page 1 after a scroll roundtrip.

test('pages stay in ascending DOM order when scrolling up mounts a lower page beside a higher one', () => {
    const document = createFakeDocument();
    const stack = createCanvasStack({ document, pixelRatioProvider: () => 1 });
    const root = stack.mount(document.createElement('main'));

    const layout = createLayout();
    const model = createLongModel(160);
    const stride = 1123 + 24; // page height + gap

    stack.render(layout, model, { viewport: { scrollTop: 0, height: 900 } });
    const pageCount = Number(root.getAttribute('data-canvas-page-count') || '0');
    assert.ok(pageCount >= 4, `expected a multi-page layout, got ${pageCount}`);

    // Scroll to the bottom so the last pages mount.
    stack.repaint({ viewport: { scrollTop: pageCount * stride, height: 900 } });
    assertAscending(domPageOrder(root), 'at bottom');

    // Scroll UP one page at a time back to the top — each step mounts a lower page while higher pages
    // remain mounted, which is exactly where the append-only mount corrupted the order.
    for (let i = pageCount; i >= 0; i -= 1) {
        stack.repaint({ viewport: { scrollTop: i * stride, height: 900 } });
        assertAscending(domPageOrder(root), `scrolling up at page ${i}`);
    }
});

test('the top spacer stays first and the bottom spacer stays last around the mounted pages', () => {
    const document = createFakeDocument();
    const stack = createCanvasStack({ document, pixelRatioProvider: () => 1 });
    const root = stack.mount(document.createElement('main'));

    stack.render(createLayout(), createLongModel(160), { viewport: { scrollTop: 0, height: 900 } });
    stack.repaint({ viewport: { scrollTop: 20 * 1147, height: 900 } });
    stack.repaint({ viewport: { scrollTop: 3 * 1147, height: 900 } });

    const ids = root.children.map(child => child.getAttribute('data-testid'));
    assert.equal(ids[0], 'document-canvas-virtual-top-spacer', 'top spacer must be the first child');
    assert.equal(ids[ids.length - 1], 'document-canvas-virtual-bottom-spacer', 'bottom spacer must be the last child');
    assertAscending(domPageOrder(root), 'after scroll roundtrip');
});

function domPageOrder(root) {
    return root.children
        .filter(child => child.getAttribute?.('data-testid') === 'document-canvas-page')
        .map(child => Number(child.getAttribute('data-page-index') || '-1'));
}

function assertAscending(order, when) {
    for (let i = 1; i < order.length; i += 1) {
        assert.ok(
            order[i] > order[i - 1],
            `page DOM order must be strictly ascending (${when}); got [${order.join(', ')}]`);
    }
}

function createLayout() {
    return {
        pageSettings: { width: 794, height: 1123, marginTop: 72, marginRight: 72, marginBottom: 72, marginLeft: 72 },
    };
}

function createLongModel(blockCount) {
    return {
        documentId: 'b5-page-mount-order',
        body: {
            blocks: Array.from({ length: blockCount }, (_, index) => ({
                id: `p${index}`,
                type: 'paragraph',
                content: {
                    runs: [{
                        id: `p${index}-run`,
                        type: 'text',
                        text: `Paragraph ${index + 1} keeps enough deterministic words to flow through multiple virtualized pages and exercise the page mount ordering path.`,
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
