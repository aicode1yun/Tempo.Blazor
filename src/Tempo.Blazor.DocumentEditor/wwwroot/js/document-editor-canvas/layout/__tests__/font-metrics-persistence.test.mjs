import assert from 'node:assert/strict';
import test from 'node:test';
import { createFontMetricsService } from '../../../document-editor/layout/font-metrics.mjs';
import { layoutCanvasDocument } from '../pagination.mjs';
import { createCanvasStack } from '../../render/canvas-stack.mjs';

// Phase N1 (canvas perf 2026-07-10): the font-metrics LRU cache must survive across layout
// passes. Historically every layout pass created a fresh service (empty cache), so each
// keystroke re-measured every run of the edited paragraph. The canvas stack now owns ONE
// persistent service and threads it through the display list into the document, header/footer
// and notes layouts.

test('two layout passes sharing one metrics service: second pass is all cache hits, byte-identical output', () => {
    const service = createFontMetricsService({ createMeasureContext: () => null });
    const model = buildModel(12);

    const first = layoutCanvasDocument(model, { fontMetrics: service });
    const statsAfterFirst = service.getStats();
    assert.ok(statsAfterFirst.MeasureCount > 0, 'cold pass performs real measurements');

    const second = layoutCanvasDocument(model, { fontMetrics: service });
    const statsAfterSecond = service.getStats();
    assert.equal(statsAfterSecond.MeasureCount, statsAfterFirst.MeasureCount,
        'warm pass measures nothing new');
    assert.ok(statsAfterSecond.MeasureCacheHits > statsAfterFirst.MeasureCacheHits,
        'warm pass is served from the cache');
    assert.equal(JSON.stringify(second.blocks), JSON.stringify(first.blocks),
        'cached measurements produce byte-identical layout');
});

test('canvas stack keeps one persistent metrics service across renders', () => {
    const document = createFakeDocument();
    const stack = createCanvasStack({ document, pixelRatioProvider: () => 1 });
    stack.mount(document.createElement('main'));

    const layout = createLayout();
    const model = buildModel(12);

    const first = stack.render(layout, model, { viewport: { scrollTop: 0, height: 900 } });
    assert.ok(first.measurementStats, 'render exposes measurement stats');
    const sizeAfterFirst = Number(first.measurementStats.MeasureCacheSize) || 0;
    const countAfterFirst = Number(first.measurementStats.MeasureCount) || 0;
    assert.ok(sizeAfterFirst > 0, 'first render fills the measurement cache');

    const second = stack.render(layout, model, { viewport: { scrollTop: 0, height: 900 } });
    assert.equal(Number(second.measurementStats.MeasureCacheSize) || 0, sizeAfterFirst,
        'measurement cache must survive across renders (persistent service)');
    assert.equal(Number(second.measurementStats.MeasureCount) || 0, countAfterFirst,
        'an unchanged model re-render must not re-measure anything');
});

test('editing one paragraph re-measures far less than a cold pass (typing path)', () => {
    const document = createFakeDocument();
    const stack = createCanvasStack({ document, pixelRatioProvider: () => 1 });
    stack.mount(document.createElement('main'));

    const layout = createLayout();
    const model = buildModel(12);
    const first = stack.render(layout, model, { viewport: { scrollTop: 0, height: 900 } });
    const coldCount = Number(first.measurementStats.MeasureCount) || 0;
    assert.ok(coldCount > 0);

    // Simulate a keystroke: clone-on-write of a single block, everything else shares references.
    const edited = { ...model, body: { ...model.body, blocks: model.body.blocks.slice() } };
    const block = structuredClone(edited.body.blocks[5]);
    block.content.runs[0].text += 'x';
    edited.body.blocks[5] = block;

    const afterEdit = stack.render(layout, edited, { viewport: { scrollTop: 0, height: 900 } });
    const editDelta = (Number(afterEdit.measurementStats.MeasureCount) || 0) - coldCount;
    assert.ok(editDelta < coldCount / 2,
        `a one-block edit must reuse the warm cache (measured ${editDelta} new runs vs ${coldCount} cold)`);
});

test('a devicePixelRatio change invalidates the measurement cache exactly once', () => {
    const document = createFakeDocument();
    let dpr = 1;
    const stack = createCanvasStack({ document, pixelRatioProvider: () => dpr });
    stack.mount(document.createElement('main'));

    const layout = createLayout();
    const model = buildModel(6);
    const first = stack.render(layout, model, { viewport: { scrollTop: 0, height: 900 } });
    assert.equal(Number(first.measurementStats.MeasureInvalidations) || 0, 0,
        'no invalidation while DPR is stable');

    dpr = 2;
    const second = stack.render(layout, model, { viewport: { scrollTop: 0, height: 900 } });
    assert.ok((Number(second.measurementStats.MeasureInvalidations) || 0) >= 1,
        'DPR change must drop cached measurements');

    // Edge case: staying on the new DPR must not keep invalidating.
    const third = stack.render(layout, model, { viewport: { scrollTop: 0, height: 900 } });
    assert.equal(Number(third.measurementStats.MeasureInvalidations) || 0,
        Number(second.measurementStats.MeasureInvalidations) || 0,
        'stable DPR after the change performs no further invalidation');
});

function createLayout() {
    return {
        pageSettings: { width: 794, height: 1123, marginTop: 72, marginRight: 72, marginBottom: 72, marginLeft: 72 },
    };
}

function buildModel(count) {
    return {
        documentId: 'n1-font-metrics-persistence',
        pageSettings: { width: 600, height: 900, marginTop: 48, marginRight: 48, marginBottom: 48, marginLeft: 48 },
        theme: { bodyFontFamily: 'Arial', bodyFontSize: 12, paragraphSpacingAfter: 8 },
        body: {
            blocks: Array.from({ length: count }, (_, index) => ({
                id: `p${index}`,
                type: 'paragraph',
                order: index + 1,
                paragraphProperties: { alignment: 'left', lineSpacing: 1.1 },
                content: {
                    type: 'paragraph',
                    runs: [{
                        id: `p${index}-run`,
                        type: 'text',
                        text: `Unique paragraph ${index + 1} verse${index + 1} carries deterministic word${index + 1} content here.`,
                        marks: [],
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

    setAttribute(name, value) {
        this.attributes.set(String(name), String(value));
    }

    removeAttribute(name) {
        this.attributes.delete(String(name));
    }

    getAttribute(name) {
        return this.attributes.has(String(name)) ? this.attributes.get(String(name)) : null;
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
