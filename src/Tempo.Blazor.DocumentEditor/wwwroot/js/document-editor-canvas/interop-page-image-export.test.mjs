import assert from 'node:assert/strict';
import test from 'node:test';
import * as interop from './interop.mjs';

// The interop layer exposes the page export to .NET: it reuses the engine's CURRENT display list
// (so the bitmap matches what the editor laid out) and emits one descriptor per page with a data URL.
// `exportPageImage` does a single page so the host can paginate without huge interop strings.

test('exportPageImages returns one descriptor per laid-out page with a data URL (default 2x)', () => {
    const { handle } = mountEngine(createContractModel(200));

    const metrics = JSON.parse(interop.getPageMetricsJson(handle));
    assert.ok(metrics.totalPages >= 2, `the contract must paginate to multiple pages (got ${metrics.totalPages})`);

    const images = JSON.parse(interop.exportPageImages(handle, JSON.stringify({})));
    assert.equal(images.length, metrics.totalPages, 'every page is exported');
    images.forEach((image, ordinal) => {
        assert.equal(image.pageIndex, ordinal, 'descriptors are in page order');
        assert.ok(image.width > 0 && image.height > 0, 'each page reports logical dimensions');
        assert.equal(image.scale, 2, 'the default export scale is 2x (retina)');
        assert.ok(/^data:image\/png;base64,/.test(image.dataUrl), 'each page yields a PNG data URL');
    });

    interop.dispose(handle);
});

test('exportPageImage exports a single page outside the viewport at the requested scale', () => {
    const { handle } = mountEngine(createContractModel(200));

    const page1 = JSON.parse(interop.exportPageImage(handle, 1, JSON.stringify({ scale: 1 })));
    assert.equal(page1.pageIndex, 1);
    assert.equal(page1.scale, 1);
    // The fake canvas encodes its backing size into the data URL; at scale 1 it equals the logical size.
    assert.ok(page1.dataUrl.includes(`${Math.round(page1.width)}x${Math.round(page1.height)}`),
        'scale 1 backs the canvas at the logical page size');

    const retina = JSON.parse(interop.exportPageImage(handle, 1, JSON.stringify({ scale: 2 })));
    assert.ok(retina.dataUrl.includes(`${Math.round(page1.width * 2)}x${Math.round(page1.height * 2)}`),
        'scale 2 doubles the canvas backing store');

    interop.dispose(handle);
});

test('export scale is clamped to the supported range and JPEG is opt-in', () => {
    const { handle } = mountEngine(createContractModel(40));

    const clamped = JSON.parse(interop.exportPageImage(handle, 0, JSON.stringify({ scale: 9 })));
    assert.equal(clamped.scale, 3, 'scale is clamped to 3x');

    const jpeg = JSON.parse(interop.exportPageImage(handle, 0, JSON.stringify({ format: 'jpeg', quality: 0.7 })));
    assert.ok(/^data:image\/jpeg;base64,/.test(jpeg.dataUrl), 'JPEG export emits a JPEG data URL');

    interop.dispose(handle);
});

test('getLayoutSnapshotJson exports the live display list as a print snapshot', () => {
    const { handle } = mountEngine(createContractModel(200));

    const metrics = JSON.parse(interop.getPageMetricsJson(handle));
    const snapshot = JSON.parse(interop.getLayoutSnapshotJson(handle));

    assert.equal(snapshot.schemaVersion, 1);
    assert.equal(snapshot.pageCount, metrics.totalPages, 'snapshot page count matches the editor pagination');
    assert.ok(snapshot.pages[0].width > 0 && snapshot.pages[0].height > 0, 'pages carry CSS-pixel dimensions');
    const pageTexts = snapshot.pages[0].commands
        .filter(command => command.type === 'text')
        .map(command => command.text)
        .join(' ');
    assert.ok(
        pageTexts.includes('Service'),
        `body text prints as text commands (got: ${pageTexts.slice(0, 200) || '<none>'})`);
    assert.ok(
        snapshot.pages.every(page => page.commands.every(command =>
            command.sourceType !== 'marginGuide' && command.sourceType !== 'bodyArea' && command.sourceType !== 'pageFill')),
        'screen chrome never reaches the print snapshot');

    interop.dispose(handle);
});

// ── fake DOM + engine harness ────────────────────────────────────────────────────────────────────

function mountEngine(model) {
    const doc = createFakeDocument();
    const host = doc.createElement('div');
    const mountElement = doc.createElement('div');
    const handle = interop.mount(host, mountElement, JSON.stringify(model), JSON.stringify({}), null);
    return { handle, doc, host };
}

function createContractModel(paragraphCount) {
    const blocks = [{
        id: 'heading-1',
        sectionId: 'section-1',
        type: 'heading',
        order: 1,
        content: { type: 'heading', headingLevel: 1, runs: [{ id: 'heading-run', type: 'text', text: 'Service agreement', marks: [] }] },
    }];
    for (let index = 0; index < paragraphCount; index += 1) {
        blocks.push({
            id: `paragraph-${index}`,
            sectionId: 'section-1',
            type: 'paragraph',
            order: index + 2,
            content: { type: 'paragraph', runs: [{ id: `paragraph-${index}-run`, type: 'text', text: `Clause ${index + 1}: the parties agree to the stated terms and conditions herein.`, marks: [] }] },
        });
    }

    return { documentId: 'signing-export-interop', body: { blocks }, sections: [{ id: 'section-1', blocks: [] }] };
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

    getContext(type) { assert.equal(type, '2d'); return this._context; }

    toDataURL(type, _quality) {
        const mime = type || 'image/png';
        // Encode the backing size so tests can assert that scale changes the resolution.
        return `data:${mime};base64,FAKE-${this.width}x${this.height}`;
    }
}

class FakeCanvasContext {
    constructor() {
        this.calls = [];
        this.fillStyle = '#000000';
        this.strokeStyle = '#000000';
        this.font = '';
        this.lineWidth = 1;
        this.textBaseline = 'alphabetic';
        this.globalAlpha = 1;
    }

    setTransform(...args) { this.calls.push({ name: 'setTransform', args }); }
    clearRect(...args) { this.calls.push({ name: 'clearRect', args }); }
    fillRect(...args) { this.calls.push({ name: 'fillRect', args }); }
    strokeRect(...args) { this.calls.push({ name: 'strokeRect', args }); }
    fillText(...args) { this.calls.push({ name: 'fillText', args }); }
    measureText(text) { return { width: String(text || '').length * 7 }; }
    save() {} restore() {} beginPath() {} closePath() {} moveTo() {} lineTo() {}
    rect() {} arc() {} stroke() {} fill() {} clip() {} setLineDash() {}
    translate() {} rotate() {} scale() {}
    quadraticCurveTo() {} bezierCurveTo() {} drawImage() {}
    createLinearGradient() { return { addColorStop() {} }; }
}
