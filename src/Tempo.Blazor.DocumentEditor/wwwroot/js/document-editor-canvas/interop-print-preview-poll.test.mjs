import assert from 'node:assert/strict';
import test from 'node:test';
import * as interop from './interop.mjs';

// Fáze 23 (code review N6.3/N11): interop.getPrintPreviewStateJson volal getPrintPreviewSnapshot()
// BEZ argumentů — a bez allowPartial vynutí synchronní render({ fullLayout: true }). Jakýkoli poll
// print-preview stavu během progresivního okna tak kompletně zneguje N11 a zablokuje UI vlákno na
// plný layout dokumentu. Kontrakt: rutinní poll čte partial snapshot; plný layout se vynucuje až
// při skutečně AKTIVNÍM print preview (jeho aktivační render layout stejně dokončí).

test('print-preview state poll does not force the full layout during the progressive window', () => {
    const { handle } = mountEngine(createLargeModel(200));

    const before = JSON.parse(interop.getPageMetricsJson(handle));
    assert.equal(before.layoutComplete, false,
        `the mount render must still be progressive (laid ${before.totalPages} pages)`);

    const stateJson = interop.getPrintPreviewStateJson(handle);
    assert.ok(typeof stateJson === 'string' && stateJson.length > 0, 'the poll still returns a snapshot');

    const after = JSON.parse(interop.getPageMetricsJson(handle));
    assert.equal(after.layoutComplete, false,
        'a routine print-preview poll must NOT complete the progressive layout synchronously');
    assert.equal(after.totalPages, before.totalPages,
        'the poll must not extend the laid page range either');

    interop.dispose(handle);
});

// ── fake DOM + engine harness (vzor interop-page-image-export.test.mjs) ─────────────────────────

function mountEngine(model) {
    const doc = createFakeDocument();
    const host = doc.createElement('div');
    const mountElement = doc.createElement('div');
    const handle = interop.mount(host, mountElement, JSON.stringify(model), JSON.stringify({}), null);
    return { handle, doc, host };
}

function createLargeModel(paragraphCount) {
    const blocks = [];
    for (let index = 0; index < paragraphCount; index += 1) {
        blocks.push({
            id: `paragraph-${index}`,
            sectionId: 'section-1',
            type: 'paragraph',
            order: index + 1,
            content: { type: 'paragraph', runs: [{ id: `paragraph-${index}-run`, type: 'text', text: `Clause ${index + 1}: ${'print preview progressive filler '.repeat(6)}`, marks: [] }] },
        });
    }

    return { documentId: 'print-preview-poll-interop', body: { blocks }, sections: [{ id: 'section-1', blocks: [] }] };
}

function createFakeDocument() {
    const doc = {
        defaultView: globalThis,
        createElement(tagName) {
            return String(tagName).toUpperCase() === 'CANVAS'
                ? new FakeCanvasElement(doc)
                : new FakeElement(doc, tagName);
        },
    };
    return doc;
}

class FakeElement {
    constructor(ownerDocument, tagName) {
        this.ownerDocument = ownerDocument;
        this.tagName = String(tagName).toUpperCase();
        this.children = [];
        this.attributes = new Map();
        this.style = {};
        this.parentNode = null;
        this.textContent = '';
        this.className = '';
    }

    appendChild(child) { child.parentNode = this; this.children.push(child); return child; }
    append(...children) { for (const child of children) { this.appendChild(child); } }
    removeChild(child) { this.children = this.children.filter(item => item !== child); child.parentNode = null; return child; }
    insertBefore(child, reference) {
        const index = reference ? this.children.indexOf(reference) : -1;
        if (index < 0) { return this.appendChild(child); }
        child.parentNode = this; this.children.splice(index, 0, child); return child;
    }
    replaceChildren(...children) {
        for (const child of this.children) { child.parentNode = null; }
        this.children = [];
        for (const child of children) { this.appendChild(child); }
    }
    querySelector() { return null; }
    setAttribute(name, value) { this.attributes.set(String(name), String(value)); }
    getAttribute(name) { return this.attributes.has(String(name)) ? this.attributes.get(String(name)) : null; }
    removeAttribute(name) { this.attributes.delete(String(name)); }
    addEventListener() {}
    removeEventListener() {}
    focus() { this.focused = true; }
    getBoundingClientRect() { return { x: 0, y: 0, top: 0, left: 0, width: 0, height: 0, right: 0, bottom: 0 }; }
}

class FakeCanvasElement extends FakeElement {
    constructor(ownerDocument) {
        super(ownerDocument, 'CANVAS');
        this.width = 0;
        this.height = 0;
        this._context = new FakeCanvasContext();
    }

    getContext() { return this._context; }
    toDataURL(type) { return `data:${type || 'image/png'};base64,FAKE-${this.width}x${this.height}`; }
}

class FakeCanvasContext {
    constructor() {
        this.fillStyle = '#000000';
        this.strokeStyle = '#000000';
        this.font = '';
        this.lineWidth = 1;
        this.textBaseline = 'alphabetic';
        this.globalAlpha = 1;
    }

    setTransform() {} clearRect() {} fillRect() {} strokeRect() {} fillText() {}
    measureText(text) { return { width: String(text || '').length * 7 }; }
    save() {} restore() {} beginPath() {} closePath() {} moveTo() {} lineTo() {}
    rect() {} arc() {} stroke() {} fill() {} clip() {} setLineDash() {}
    translate() {} rotate() {} scale() {}
    quadraticCurveTo() {} bezierCurveTo() {} drawImage() {}
    createLinearGradient() { return { addColorStop() {} }; }
}
