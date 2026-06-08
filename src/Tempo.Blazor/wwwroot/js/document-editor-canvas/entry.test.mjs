import assert from 'node:assert/strict';
import test from 'node:test';
import { CANVAS_LAYER_KINDS, createCanvasDocumentEngine } from './entry.mjs';

test('createCanvasDocumentEngine mounts a canvas-per-visible-page stack without Blazor', () => {
    const doc = createFakeDocument();
    const host = doc.createElement('div');
    const engine = createCanvasDocumentEngine({
        host,
        document: doc,
        pixelRatioProvider: () => 2,
        model: {
            documentId: 'phase-1',
            body: {
                blocks: [
                    {
                        id: 'p1',
                        type: 'paragraph',
                        content: { runs: [{ text: 'Accessible paragraph' }] },
                    },
                ],
            },
        },
        ariaLabel: 'Canvas document',
    });

    const result = engine.render();
    const snapshot = engine.getSnapshot();

    assert.equal(result.ok, true);
    assert.equal(snapshot.mounted, true);
    assert.equal(snapshot.architecture.name, 'CanvasDocumentEngine');
    assert.equal(snapshot.architecture.pageSurfaceStrategy, 'canvas-per-visible-page');
    assert.deepEqual(snapshot.architecture.layerKinds, CANVAS_LAYER_KINDS);
    assert.equal(host.getAttribute('data-canvas-engine-ready'), 'true');
    assert.equal(host.getAttribute('data-canvas-engine-page-strategy'), 'canvas-per-visible-page');
    assert.equal(findAll(host, node => node.tagName === 'CANVAS').length, CANVAS_LAYER_KINDS.length);
    assert.equal(findAll(host, node => node.getAttribute('contenteditable') === 'true').length, 0);
});

test('phase 1 canvas stack applies high-DPI backing store and paints an intentional empty page', () => {
    const doc = createFakeDocument();
    const host = doc.createElement('div');
    const engine = createCanvasDocumentEngine({
        host,
        document: doc,
        pixelRatioProvider: () => 2.5,
        model: {},
    });

    engine.render();

    const page = findOne(host, node => node.getAttribute('data-testid') === 'document-canvas-page');
    const backgroundCanvas = findOne(host, node => node.getAttribute('data-canvas-layer') === 'page-background');
    const context = backgroundCanvas.getContext('2d');

    assert.equal(page.style.width, '794px');
    assert.equal(page.style.height, '1123px');
    assert.equal(backgroundCanvas.width, 1985);
    assert.equal(backgroundCanvas.height, 2808);
    assert.deepEqual(context.transforms.at(-1), [2.5, 0, 0, 2.5, 0, 0]);
    assert.ok(context.calls.some(call => call.name === 'fillRect' && call.args[2] === 794 && call.args[3] === 1123));
    assert.ok(context.calls.some(call => call.name === 'strokeRect'));
});

test('accessibility mirror and hidden input bridge are present but not browser contenteditable authorities', () => {
    const doc = createFakeDocument();
    const host = doc.createElement('div');
    const engine = createCanvasDocumentEngine({
        host,
        document: doc,
        model: {
            body: {
                blocks: [
                    {
                        id: 'a11y-p',
                        type: 'paragraph',
                        content: {
                            runs: [
                                { text: 'Screen reader mirror text ' },
                                {
                                    id: 'a11y-math-run',
                                    type: 'math',
                                    math: {
                                        mathId: 'a11y-math',
                                        altText: 'x squared',
                                        content: {
                                            elements: [{
                                                type: 'sup',
                                                base: { elements: [{ type: 'run', text: 'x' }] },
                                                superscript: { elements: [{ type: 'run', text: '2' }] },
                                            }],
                                        },
                                    },
                                },
                            ],
                        },
                    },
                    {
                        id: 'a11y-table',
                        type: 'table',
                        content: {
                            table: {
                                rows: [{
                                    id: 'a11y-table-row',
                                    cells: [{
                                        id: 'a11y-table-cell',
                                        isHeader: false,
                                        blocks: [{
                                            id: 'a11y-table-cell-p',
                                            type: 'paragraph',
                                            content: { runs: [{ text: 'Table mirror text' }] },
                                        }],
                                    }],
                                }],
                            },
                        },
                    },
                ],
            },
        },
    });

    engine.render();

    const mirror = findOne(host, node => node.getAttribute('data-testid') === 'document-canvas-a11y-mirror');
    const input = findOne(host, node => node.getAttribute('data-testid') === 'document-canvas-hidden-input');

    assert.equal(mirror.getAttribute('role'), 'document');
    assert.equal(findOne(mirror, node => node.getAttribute('data-block-id') === 'a11y-p').textContent, 'Screen reader mirror text ');
    const math = findOne(mirror, node => node.getAttribute('data-canvas-a11y-math') === 'true');
    assert.equal(math.getAttribute('role'), 'math');
    assert.equal(math.getAttribute('aria-label'), 'x squared');
    assert.equal(math.getAttribute('data-math-id'), 'a11y-math');
    assert.equal(findOne(mirror, node => node.getAttribute('data-canvas-a11y-table') === 'true').tagName, 'TABLE');
    assert.equal(findOne(mirror, node => node.getAttribute('data-cell-id') === 'a11y-table-cell').tagName, 'TD');
    assert.equal(findOne(mirror, node => node.getAttribute('data-block-id') === 'a11y-table-cell-p').textContent, 'Table mirror text');
    assert.equal(input.tagName, 'TEXTAREA');
    assert.equal(input.getAttribute('role'), 'textbox');
    assert.equal(input.getAttribute('aria-multiline'), 'true');
    assert.equal(input.getAttribute('aria-controls'), 'document-canvas-a11y-mirror');
    assert.equal(input.getAttribute('spellcheck'), 'false');
    assert.equal(findAll(host, node => node.getAttribute('contenteditable') != null).length, 0);
});

test('command dispatcher and history stores are available from the engine snapshot boundary', () => {
    const doc = createFakeDocument();
    const host = doc.createElement('div');
    const engine = createCanvasDocumentEngine({ host, document: doc, model: {} });
    engine.commandDispatcher.register('phase1.probe', payload => ({ received: payload.value }));
    const commandResult = engine.commandDispatcher.execute('phase1.probe', { value: 42 });
    engine.history.push({
        id: 'tx1',
        kind: 'probe',
        before: { model: { version: 1 }, selection: null },
        after: { model: { version: 2 }, selection: null },
    });

    assert.equal(commandResult.handled, true);
    assert.deepEqual(commandResult.result, { received: 42 });
    assert.equal(engine.history.snapshot().canUndo, true);
    assert.equal(engine.history.undo().id, 'tx1');
    assert.equal(engine.history.snapshot().canRedo, true);
});

test('canvas offline state carries serializable collaboration and runtime data', () => {
    const doc = createFakeDocument();
    const host = doc.createElement('div');
    const engine = createCanvasDocumentEngine({
        host,
        document: doc,
        model: {
            documentId: 'phase-20-offline',
            body: {
                blocks: [{
                    id: 'p1',
                    type: 'paragraph',
                    content: { runs: [{ text: 'Offline ready' }] },
                }],
            },
        },
    });

    engine.render();
    const state = engine.getOfflineState();
    const roundTrip = JSON.parse(JSON.stringify(state));

    assert.equal(roundTrip.schemaVersion, 1);
    assert.equal(roundTrip.engine, 'CanvasDocumentEngine');
    assert.equal(roundTrip.model.documentId, 'phase-20-offline');
    assert.equal(roundTrip.collaboration.protocolVersion, 1);
    assert.ok(Number.isFinite(roundTrip.dirtyEpoch));
    assert.ok(Number.isFinite(roundTrip.undoEpoch));
});

test('input commits publish latency immediately and coalesce canvas render to the next frame', async () => {
    const doc = createFakeDocument();
    const host = doc.createElement('div');
    const initialModel = {
        documentId: 'phase-22-input-schedule',
        body: {
            blocks: [{
                id: 'p1',
                type: 'paragraph',
                content: { runs: [{ text: 'Before' }] },
            }],
        },
    };
    const nextModel = {
        documentId: 'phase-22-input-schedule',
        body: {
            blocks: [{
                id: 'p1',
                type: 'paragraph',
                content: { runs: [{ text: 'Before after' }] },
            }],
        },
    };
    const engine = createCanvasDocumentEngine({ host, document: doc, model: initialModel });

    engine.render();
    const root = findOne(host, node => node.getAttribute('data-testid') === 'document-canvas-engine-root');
    const renderCountBefore = Number(root.getAttribute('data-canvas-render-count') || '0');
    const commitResult = engine.commitInputChange({
        before: { model: initialModel, selection: null },
        model: nextModel,
        selection: null,
        input: { dirtyBlockIds: ['p1'] },
        result: { dirtyBlockIds: ['p1'] },
    });

    assert.equal(commitResult.scheduled, true);
    assert.equal(root.getAttribute('data-canvas-typing-latency-count'), '1');
    assert.equal(Number(root.getAttribute('data-canvas-render-count') || '0'), renderCountBefore);

    await new Promise(resolve => setTimeout(resolve, 25));

    assert.ok(Number(root.getAttribute('data-canvas-render-count') || '0') > renderCountBefore);
    assert.equal(root.getAttribute('data-canvas-recalc-first-dirty-block-index'), '0');

    await new Promise(resolve => setTimeout(resolve, 220));

    assert.equal(engine.history.snapshot().canUndo, true);
});

test('view zoom commands rerender page surfaces without marking document dirty', () => {
    const doc = createFakeDocument();
    const host = doc.createElement('div');
    const engine = createCanvasDocumentEngine({
        host,
        document: doc,
        model: createTextModel('Zoom command keeps the model clean.', 0),
    });

    engine.render();
    const page = findOne(host, node => node.getAttribute('data-testid') === 'document-canvas-page');
    const root = findOne(host, node => node.getAttribute('data-testid') === 'document-canvas-engine-root');
    assert.equal(page.getAttribute('data-canvas-page-css-width'), '794');

    const result = engine.execCommand('fitWidth', {
        metrics: {
            pageWidth: 794,
            pageHeight: 1123,
            viewportWidth: 650,
            viewportHeight: 900,
            paddingInline: 48,
            paddingBlock: 48,
        },
    });

    assert.equal(result.handled, true);
    assert.equal(result.result.viewChanged, true);
    assert.equal(root.getAttribute('data-canvas-zoom-preset'), 'fitWidth');
    assert.equal(root.getAttribute('data-canvas-zoom-percent'), '76');
    assert.equal(page.getAttribute('data-canvas-page-css-width'), '602');
    assert.ok(Number(page.getAttribute('data-canvas-painted-command-count') || '0') > 0);
    assert.equal(engine.getSnapshot().modelVersion, 0);
});

test('selection layout enriches existing connector drawing blocks with endpoint handles', () => {
    const doc = createFakeDocument();
    const host = doc.createElement('div');
    const engine = createCanvasDocumentEngine({
        host,
        document: doc,
        model: createConnectorSelectionModel(),
    });

    engine.render();
    const root = findOne(host, node => node.getAttribute('data-testid') === 'document-canvas-engine-root');
    const selectionLayout = engine.getSnapshot().render.selectionLayout;
    const connectorBlock = selectionLayout.blocks.find(block => block.objectId === 'selection-connector');

    assert.equal(connectorBlock.object.kind, 'connector');
    assert.equal(connectorBlock.connector.points.length, 4);
    assert.equal(connectorBlock.object.connector.points.length, 4);

    engine.selectionController.setSelection({
        anchor: { blockId: connectorBlock.blockId, offset: 0 },
        focus: { blockId: connectorBlock.blockId, offset: 0 },
        object: {
            objectId: connectorBlock.objectId,
            blockId: connectorBlock.blockId,
            runId: connectorBlock.runId,
            pageIndex: connectorBlock.pageIndex,
            rect: connectorBlock.rect,
            width: connectorBlock.rect.width,
            height: connectorBlock.rect.height,
            kind: connectorBlock.object.kind,
            connector: connectorBlock.connector,
        },
    });

    assert.equal(root.getAttribute('data-canvas-object-id'), 'selection-connector');
    assert.equal(root.getAttribute('data-canvas-object-connector-handle-count'), '2');
    assert.equal(findAll(host, node => node.getAttribute('data-testid') === 'document-canvas-object-connector-handle-start').length, 1);
    assert.equal(findAll(host, node => node.getAttribute('data-testid') === 'document-canvas-object-connector-handle-end').length, 1);
});

test('autocorrect side effects keep raw typed text as the undo snapshot', async () => {
    const doc = createFakeDocument();
    const host = doc.createElement('div');
    const initialModel = createTextModel('Dash: ', 0);
    const firstDashModel = createTextModel('Dash: -', 1);
    const correctedModel = createTextModel('Dash: —', 2);
    const rawTypedModel = createTextModel('Dash: --', 2);
    const engine = createCanvasDocumentEngine({ host, document: doc, model: initialModel });

    engine.render();
    engine.commitInputChange({
        before: { model: initialModel, selection: collapsedTextSelection(6) },
        model: firstDashModel,
        selection: collapsedTextSelection(7),
        edit: { type: 'insertText', text: '-', source: 'insertText' },
        input: { revision: 1, dirtyBlockIds: ['p1'] },
        result: { changed: true, operation: 'insertText', dirtyBlockIds: ['p1'] },
    });
    engine.commitInputChange({
        before: { model: rawTypedModel, selection: collapsedTextSelection(8) },
        model: correctedModel,
        selection: collapsedTextSelection(7),
        edit: { type: 'insertText', text: '-', source: 'insertText' },
        input: { revision: 2, dirtyBlockIds: ['p1'] },
        result: { changed: true, operation: 'emDash', autoCorrect: true, dirtyBlockIds: ['p1'] },
    });

    await new Promise(resolve => setTimeout(resolve, 220));

    const transaction = engine.history.undo();
    assert.equal(textFromModel(transaction.before.model), 'Dash: --');
    assert.equal(textFromModel(transaction.after.model), 'Dash: —');
});

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

function createTextModel(text, version = 0) {
    return {
        documentId: 'entry-autocorrect',
        version,
        body: {
            blocks: [{
                id: 'p1',
                sectionId: 'section-1',
                type: 'paragraph',
                order: 10,
                paragraphProperties: {},
                content: {
                    type: 'paragraph',
                    runs: [{ id: 'p1-run', type: 'text', text, marks: [] }],
                },
            }],
        },
        sections: [{ id: 'section-1', blocks: [] }],
    };
}

function createConnectorSelectionModel() {
    return {
        documentId: 'entry-connector-selection',
        body: {
            blocks: [
                drawingBlock('source-shape-block', 'selection-source-shape', 1, 120, 72, {
                    preset: 'rectangle',
                    fill: { color: '#dbeafe', opacity: 1 },
                    stroke: { color: '#2563eb', width: 2 },
                }, { x: 96, y: 120 }),
                drawingBlock('target-shape-block', 'selection-target-shape', 1, 120, 72, {
                    preset: 'ellipse',
                    fill: { color: '#fef3c7', opacity: 1 },
                    stroke: { color: '#d97706', width: 2 },
                }, { x: 420, y: 170 }),
                drawingBlock('connector-block', 'selection-connector', 4, 300, 84, {
                    preset: 'bentConnector',
                    fill: { type: 'none', color: '#ffffff' },
                    stroke: { color: '#0f766e', width: 2, endArrow: 'triangle' },
                    routing: 'elbow',
                    startConnection: { objectId: 'selection-source-shape', site: 'right' },
                    endConnection: { objectId: 'selection-target-shape', site: 'left' },
                }, { x: 216, y: 156 }),
            ],
        },
        sections: [{ id: 'section-1', blocks: [] }],
    };
}

function drawingBlock(blockId, objectId, kind, width, height, shape, position) {
    return {
        id: blockId,
        sectionId: 'section-1',
        type: 'paragraph',
        order: 10,
        paragraphProperties: {},
        content: {
            type: 'paragraph',
            runs: [{
                id: `${objectId}-run`,
                type: 'drawing',
                drawing: {
                    objectId,
                    kind,
                    size: { width, height },
                    naturalSize: { width, height },
                    layout: {
                        kind: 1,
                        anchor: { blockId: blockId, offset: 0 },
                        position: { x: Number(position?.x || 0) || 0, y: Number(position?.y || 0) || 0 },
                        wrap: { mode: 6 },
                        transform: { width, height, lockAspectRatio: false },
                        stacking: { zIndex: kind },
                    },
                    shape,
                },
            }],
        },
    };
}

function collapsedTextSelection(offset) {
    return {
        anchor: { blockId: 'p1', offset },
        focus: { blockId: 'p1', offset },
    };
}

function textFromModel(model) {
    return (model?.body?.blocks?.[0]?.content?.runs || []).map(run => run.text || '').join('');
}

class FakeElement {
    constructor(ownerDocument, tagName) {
        this.ownerDocument = ownerDocument;
        this.tagName = tagName;
        this.children = [];
        this.attributes = new Map();
        this.style = {};
        this.parentNode = null;
        this.textContent = '';
        this.className = '';
    }

    appendChild(child) {
        child.parentNode = this;
        this.children.push(child);
        return child;
    }

    append(...children) {
        for (const child of children) {
            this.appendChild(child);
        }
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

    removeAttribute(name) {
        this.attributes.delete(String(name));
    }

    addEventListener() {
    }

    removeEventListener() {
    }

    focus() {
        this.focused = true;
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
        this.transforms = [];
    }

    setTransform(...args) {
        this.transforms.push(args);
        this.calls.push({ name: 'setTransform', args });
    }

    clearRect(...args) {
        this.calls.push({ name: 'clearRect', args });
    }

    fillRect(...args) {
        this.calls.push({ name: 'fillRect', args });
    }

    fillText(...args) {
        this.calls.push({ name: 'fillText', args });
    }

    strokeRect(...args) {
        this.calls.push({ name: 'strokeRect', args });
    }

    save(...args) {
        this.calls.push({ name: 'save', args });
    }

    restore(...args) {
        this.calls.push({ name: 'restore', args });
    }

    beginPath(...args) {
        this.calls.push({ name: 'beginPath', args });
    }

    moveTo(...args) {
        this.calls.push({ name: 'moveTo', args });
    }

    lineTo(...args) {
        this.calls.push({ name: 'lineTo', args });
    }

    stroke(...args) {
        this.calls.push({ name: 'stroke', args });
    }

    setLineDash(...args) {
        this.calls.push({ name: 'setLineDash', args });
    }
}

function findOne(root, predicate) {
    const result = findAll(root, predicate)[0];
    assert.ok(result, 'Expected a matching fake DOM node.');
    return result;
}

function findAll(root, predicate) {
    const results = [];
    visit(root);
    return results;

    function visit(node) {
        if (predicate(node)) {
            results.push(node);
        }

        for (const child of node.children || []) {
            visit(child);
        }
    }
}
