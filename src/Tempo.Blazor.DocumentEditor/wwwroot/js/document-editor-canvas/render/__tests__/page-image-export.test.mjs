import assert from 'node:assert/strict';
import test from 'node:test';
import {
    buildPageImageDisplayList,
    clampExportScale,
    EXPORT_LAYER_KINDS,
    renderDisplayListPageToCanvas,
    renderPageToCanvas,
} from '../page-image-export.mjs';

// The signing-template page export (plan S1) turns a canvas-editor document into one flat bitmap
// per page. It must reuse the real layout + display-list pipeline (no editing chrome) and work for
// EVERY page, including ones the editor would not have mounted under virtualization.

test('renderPageToCanvas sizes the canvas to the page and paints page content at scale 1', () => {
    const model = createDocumentModel(3);
    const { canvas, width, height, scale } = renderPageToCanvas(model, createLayout(), 0, {
        scale: 1,
        createCanvas: createRecordingCanvasFactory(),
        fontMetrics: createDeterministicMetrics(),
    });

    assert.equal(scale, 1);
    assert.equal(canvas.width, Math.round(width));
    assert.equal(canvas.height, Math.round(height));
    assert.ok(width > 0 && height > 0, 'the page must report real logical dimensions');

    const context = canvas.getContext('2d');
    const setTransform = context.calls.find(call => call.name === 'setTransform');
    assert.ok(setTransform, 'the export must establish a paint transform');
    assert.deepEqual(setTransform.args, [1, 0, 0, 1, 0, 0], 'scale 1 paints 1:1');
    assert.ok(context.calls.some(call => call.name === 'fillRect'), 'the page background/fill is painted');
    assert.ok(context.calls.some(call => call.name === 'fillText'), 'paragraph text is painted onto the page');
});

test('export scale multiplies both the canvas backing size and the paint transform', () => {
    const model = createDocumentModel(3);
    const layout = createLayout();
    const metrics = createDeterministicMetrics();

    const base = renderPageToCanvas(model, layout, 0, { scale: 1, createCanvas: createRecordingCanvasFactory(), fontMetrics: metrics });
    const retina = renderPageToCanvas(model, layout, 0, { scale: 2, createCanvas: createRecordingCanvasFactory(), fontMetrics: metrics });

    assert.equal(retina.canvas.width, Math.round(base.width * 2));
    assert.equal(retina.canvas.height, Math.round(base.height * 2));
    const transform = retina.canvas.getContext('2d').calls.find(call => call.name === 'setTransform');
    assert.deepEqual(transform.args, [2, 0, 0, 2, 0, 0], 'scale 2 scales the device transform');
});

test('clampExportScale keeps the export between 1x and 3x and defaults to 2x', () => {
    assert.equal(clampExportScale(undefined), 2);
    assert.equal(clampExportScale(0), 2);
    assert.equal(clampExportScale(-5), 2);
    assert.equal(clampExportScale('not a number'), 2);
    assert.equal(clampExportScale(1), 1);
    assert.equal(clampExportScale(2.5), 2.5);
    assert.equal(clampExportScale(9), 3);
});

test('renders a page the editor would not have mounted under virtualization (no viewport)', () => {
    const model = createDocumentModel(90);
    const displayList = buildPageImageDisplayList(model, createLayout(), { fontMetrics: createDeterministicMetrics() });
    assert.ok(displayList.pages.length >= 2, `the document must paginate to multiple pages (got ${displayList.pages.length})`);

    const lastIndex = displayList.pages.length - 1;
    const { canvas, pageIndex } = renderDisplayListPageToCanvas(displayList, lastIndex, {
        scale: 1,
        createCanvas: createRecordingCanvasFactory(),
    });

    assert.equal(pageIndex, lastIndex);
    const context = canvas.getContext('2d');
    assert.ok(context.calls.some(call => call.name === 'fillText'),
        'the last page is fully painted even though no viewport ever mounted it');
});

test('renderDisplayListPageToCanvas throws for an out-of-range page index', () => {
    const displayList = buildPageImageDisplayList(createDocumentModel(2), createLayout(), { fontMetrics: createDeterministicMetrics() });
    assert.throws(
        () => renderDisplayListPageToCanvas(displayList, 99, { createCanvas: createRecordingCanvasFactory() }),
        /out of range/i);
});

test('export omits editor chrome layers (selection caret, comment/revision anchors, diagnostics)', () => {
    assert.ok(!EXPORT_LAYER_KINDS.includes('selection-caret'));
    assert.ok(!EXPORT_LAYER_KINDS.includes('annotations'));
    assert.ok(!EXPORT_LAYER_KINDS.includes('diagnostics'));
    assert.deepEqual(EXPORT_LAYER_KINDS.slice().sort(), ['content', 'objects', 'page-background']);

    // A hand-built single-page display list with a chrome-only diagnostic command must not paint it:
    // the diagnostic dash (setLineDash) is the tell-tale that never fires when its layer is skipped.
    const displayList = {
        pages: [{ index: 0, width: 200, height: 300 }],
        commands: [
            { id: 'fill', type: 'pageFill', layer: 'page-background', x: 0, y: 0, width: 200, height: 300, fill: '#ffffff' },
            { id: 'diag', type: 'diagnosticOverlay', layer: 'diagnostics', x: 10, y: 10, width: 50, height: 50, stroke: '#0ea5e9', dash: [3, 3] },
        ],
    };
    const { canvas } = renderDisplayListPageToCanvas(displayList, 0, { scale: 1, createCanvas: createRecordingCanvasFactory() });
    const context = canvas.getContext('2d');
    assert.ok(context.calls.some(call => call.name === 'fillRect'), 'the page background is still painted');
    assert.ok(!context.calls.some(call => call.name === 'setLineDash'), 'diagnostic chrome is never painted into the export');
});

// ── helpers ────────────────────────────────────────────────────────────────────────────────────

function createLayout() {
    return { pages: [{ index: 0, width: 794, height: 1123, body: { x: 96, y: 96, width: 602, height: 931 } }] };
}

function createDocumentModel(paragraphCount) {
    const blocks = [{
        id: 'heading-1',
        type: 'heading',
        order: 1,
        content: { type: 'heading', headingLevel: 1, runs: [{ id: 'heading-run', type: 'text', text: 'Service agreement', marks: [] }] },
    }];
    for (let index = 0; index < paragraphCount; index += 1) {
        blocks.push({
            id: `paragraph-${index}`,
            type: 'paragraph',
            order: index + 2,
            content: {
                type: 'paragraph',
                runs: [{ id: `paragraph-${index}-run`, type: 'text', text: `Clause ${index + 1}: the parties agree to the stated terms.`, marks: [] }],
            },
        });
    }

    return {
        documentId: 'signing-export-test',
        theme: { bodyFontFamily: 'Aptos, Arial, sans-serif', bodyFontSize: 11, bodyLineHeight: 1.15, paragraphSpacingAfter: 8 },
        body: { blocks },
    };
}

function createDeterministicMetrics() {
    return {
        measureRun(request) {
            const fontSize = Number(request.fontSize) || 16;
            const text = String(request.text || '');
            return {
                width: Math.max(1, text.length * fontSize * 0.55),
                ascent: fontSize * 0.8,
                descent: fontSize * 0.2,
                lineHeight: Math.ceil(fontSize * 1.25),
            };
        },
    };
}

function createRecordingCanvasFactory() {
    return (width, height) => new RecordingCanvas(width, height);
}

class RecordingCanvas {
    constructor(width, height) {
        this.width = width;
        this.height = height;
        this._context = new RecordingContext();
    }

    getContext(type) {
        assert.equal(type, '2d');
        return this._context;
    }
}

class RecordingContext {
    constructor() {
        this.calls = [];
        this.fillStyle = '#000000';
        this.strokeStyle = '#000000';
        this.lineWidth = 1;
        this.font = '';
        this.textBaseline = 'alphabetic';
        this.globalAlpha = 1;
    }

    setTransform(...args) { this.calls.push({ name: 'setTransform', args }); }
    save(...args) { this.calls.push({ name: 'save', args }); }
    restore(...args) { this.calls.push({ name: 'restore', args }); }
    fillRect(...args) { this.calls.push({ name: 'fillRect', args }); }
    strokeRect(...args) { this.calls.push({ name: 'strokeRect', args }); }
    clearRect(...args) { this.calls.push({ name: 'clearRect', args }); }
    fillText(...args) { this.calls.push({ name: 'fillText', args }); }
    strokeText(...args) { this.calls.push({ name: 'strokeText', args }); }
    measureText(text) { this.calls.push({ name: 'measureText', args: [text] }); return { width: String(text || '').length * 7 }; }
    beginPath(...args) { this.calls.push({ name: 'beginPath', args }); }
    closePath(...args) { this.calls.push({ name: 'closePath', args }); }
    moveTo(...args) { this.calls.push({ name: 'moveTo', args }); }
    lineTo(...args) { this.calls.push({ name: 'lineTo', args }); }
    rect(...args) { this.calls.push({ name: 'rect', args }); }
    arc(...args) { this.calls.push({ name: 'arc', args }); }
    quadraticCurveTo(...args) { this.calls.push({ name: 'quadraticCurveTo', args }); }
    bezierCurveTo(...args) { this.calls.push({ name: 'bezierCurveTo', args }); }
    stroke(...args) { this.calls.push({ name: 'stroke', args }); }
    fill(...args) { this.calls.push({ name: 'fill', args }); }
    clip(...args) { this.calls.push({ name: 'clip', args }); }
    setLineDash(...args) { this.calls.push({ name: 'setLineDash', args }); }
    translate(...args) { this.calls.push({ name: 'translate', args }); }
    rotate(...args) { this.calls.push({ name: 'rotate', args }); }
    scale(...args) { this.calls.push({ name: 'scale', args }); }
    drawImage(...args) { this.calls.push({ name: 'drawImage', args }); }
    createLinearGradient() { return { addColorStop() {} }; }
}
