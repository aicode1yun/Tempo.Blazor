import assert from 'node:assert/strict';
import test from 'node:test';
import { createCanvasClipboardController } from '../clipboard-controller.mjs';
import { INTERNAL_CLIPBOARD_MIME } from '../html-normalizer.mjs';

test('copies selected text as plain HTML and internal canvas fragment', () => {
    const harness = createHarness();
    harness.selection = range('source', 5, 'source', 19);
    const clipboard = new FakeDataTransfer();

    const result = harness.controller.copy(clipboard);

    assert.equal(result.handled, true);
    assert.equal(clipboard.getData('text/plain'), 'this formatted');
    assert.match(clipboard.getData('text/html'), /<p>/);
    assert.ok(JSON.parse(clipboard.getData(INTERNAL_CLIPBOARD_MIME)).blocks.length > 0);
    assert.equal(harness.controller.getDebugSnapshot().source, 'internal');
});

test('cuts selected text as one undoable transaction', () => {
    const harness = createHarness();
    harness.selection = range('cut', 0, 'cut', 9);
    const clipboard = new FakeDataTransfer();

    const result = harness.controller.cut(clipboard);

    assert.equal(result.handled, true);
    assert.equal(harness.model.body.blocks[1].content.runs[0].text, 'sentence');
    assert.equal(harness.history.transactions.length, 1);
    assert.equal(harness.history.transactions[0].kind, 'clipboard');
    assert.equal(harness.history.transactions[0].commandId, 'cut');
});

test('pastes internal fragment preserving marks at the caret', async () => {
    const harness = createHarness();
    const sourceClipboard = new FakeDataTransfer();
    harness.selection = range('source', 5, 'source', 19);
    harness.controller.copy(sourceClipboard);

    harness.selection = collapsed('target', 14);
    const result = await harness.controller.pasteFromClipboardData(sourceClipboard);

    assert.equal(result.handled, true);
    assert.equal(harness.model.body.blocks[2].content.runs.map(run => run.text).join(''), 'Paste target: this formatted');
    assert.ok(harness.model.body.blocks[2].content.runs.some(run => run.marks.some(mark => mark.type === 'bold')));
    assert.equal(harness.history.transactions.length, 1);
});

test('pastes plain text and URL clipboard payloads through model policies', async () => {
    const harness = createHarness();
    harness.selection = collapsed('target', 14);
    const plain = new FakeDataTransfer();
    plain.setData('text/plain', 'First\nSecond');
    await harness.controller.pasteFromClipboardData(plain);

    assert.equal(harness.model.body.blocks.length, 7);
    assert.ok(harness.model.body.blocks.some(block => block.content?.runs?.[0]?.text === 'Second'));

    const urlHarness = createHarness();
    urlHarness.selection = collapsed('target', 14);
    const url = new FakeDataTransfer();
    url.setData('text/plain', 'https://example.com');
    await urlHarness.controller.pasteFromClipboardData(url);

    const targetRuns = urlHarness.model.body.blocks[2].content.runs;
    assert.equal(targetRuns.map(run => run.text).join(''), 'Paste target: https://example.com');
    assert.ok(targetRuns.some(run => run.marks.some(mark => mark.type === 'link' && mark.link.href === 'https://example.com')));
});

test('pastes rich HTML through sanitizer and stores clipboard debug snapshot', async () => {
    const harness = createHarness();
    harness.selection = collapsed('rich', 19);
    const clipboard = new FakeDataTransfer();
    clipboard.setData('text/html', '<p><strong>Approved</strong> <span style="color:#2563eb">term</span><script>x()</script></p>');
    clipboard.setData('text/plain', 'Approved term');

    const result = await harness.controller.pasteFromClipboardData(clipboard);
    const debug = harness.controller.getDebugSnapshot();

    assert.equal(result.handled, true);
    assert.equal(debug.source, 'html');
    assert.doesNotMatch(debug.normalizedJson, /script/i);
    assert.ok(harness.model.body.blocks[3].content.runs.some(run => run.marks.some(mark => mark.type === 'bold')));
    assert.ok(harness.model.body.blocks[3].content.runs.some(run => run.marks.some(mark => mark.type === 'textColor')));
});

test('pastes images through the configured provider flow', async () => {
    const uploads = [];
    const harness = createHarness({
        uploadImage(file) {
            uploads.push(file);
            return {
                success: true,
                assetId: 'asset-1',
                url: '/images/asset-1',
                fileName: file.name,
                width: 320,
                height: 180,
            };
        },
    });
    harness.selection = collapsed('target', 14);
    const clipboard = new FakeDataTransfer();
    clipboard.items.push({
        kind: 'file',
        getAsFile: () => ({ name: 'clipboard.png', type: 'image/png', size: 12 }),
    });

    const result = await harness.controller.pasteFromClipboardData(clipboard);

    assert.equal(result.handled, true);
    assert.equal(uploads.length, 1);
    assert.ok(harness.model.body.blocks.some(block => block.type === 'image' && block.content.image.assetId === 'asset-1'));
});

test('copies cuts and pastes selected drawing objects as internal fragments with new object ids', async () => {
    const harness = createHarness();
    harness.model.body.blocks.push(drawingBlock('drawing-source', 'drawing-1'));
    harness.selection = objectSelection('drawing-source', 'drawing-1', 'drawing-1-run');
    const clipboard = new FakeDataTransfer();

    const copied = harness.controller.copy(clipboard);
    assert.equal(copied.handled, true);
    const internal = JSON.parse(clipboard.getData(INTERNAL_CLIPBOARD_MIME));
    assert.equal(internal.source, 'internal-object');
    assert.equal(internal.blocks[0].content.runs[0].drawing.objectId, 'drawing-1');

    harness.selection = collapsed('target', 14);
    const pasted = await harness.controller.pasteFromClipboardData(clipboard);
    assert.equal(pasted.handled, true);
    const drawingIds = drawingObjectIds(harness.model);
    assert.equal(drawingIds.includes('drawing-1'), true);
    assert.equal(drawingIds.includes('drawing-1-copy'), true);
    assert.equal(harness.history.transactions.length, 1);
    assert.equal(harness.history.transactions[0].commandId, 'paste-internal');

    const cutHarness = createHarness();
    cutHarness.model.body.blocks.push(drawingBlock('drawing-source', 'drawing-cut'));
    cutHarness.selection = objectSelection('drawing-source', 'drawing-cut', 'drawing-cut-run');
    const cutClipboard = new FakeDataTransfer();
    const cut = cutHarness.controller.cut(cutClipboard);

    assert.equal(cut.handled, true);
    assert.equal(cut.operation, 'cut-object');
    assert.equal(drawingObjectIds(cutHarness.model).includes('drawing-cut'), false);
    assert.equal(cutHarness.history.transactions.length, 1);
    assert.equal(cutHarness.history.transactions[0].commandId, 'cut-object');
});

test('copies and pastes grouped drawing objects with remapped child ids', async () => {
    const harness = createHarness();
    harness.model.body.blocks.push(
        drawingBlock('group-child-a-block', 'group-child-a', { metadata: { groupId: 'group-1' } }),
        drawingBlock('group-child-b-block', 'group-child-b', { metadata: { groupId: 'group-1' } }),
        groupDrawingBlock('group-source', 'group-1', ['group-child-a', 'group-child-b']));
    harness.selection = objectSelection('group-source', 'group-1', 'group-1-run');
    const clipboard = new FakeDataTransfer();

    const copied = harness.controller.copy(clipboard);
    assert.equal(copied.handled, true);
    const internal = JSON.parse(clipboard.getData(INTERNAL_CLIPBOARD_MIME));
    assert.equal(internal.blocks.length, 3);

    harness.selection = collapsed('target', 14);
    const pasted = await harness.controller.pasteFromClipboardData(clipboard);

    assert.equal(pasted.handled, true);
    const group = drawingByObjectId(harness.model, 'group-1-copy');
    const childA = drawingByObjectId(harness.model, 'group-child-a-copy');
    const childB = drawingByObjectId(harness.model, 'group-child-b-copy');
    assert.deepEqual(group.group.childObjectIds, ['group-child-a-copy', 'group-child-b-copy']);
    assert.equal(childA.metadata.groupId, 'group-1-copy');
    assert.equal(childB.metadata.groupId, 'group-1-copy');
});

function createHarness(overrides = {}) {
    const input = new FakeElement();
    const root = new FakeElement();
    const history = {
        transactions: [],
        push(transaction) {
            this.transactions.push(transaction);
            return { canUndo: true, canRedo: false, undoDepth: this.transactions.length, redoDepth: 0 };
        },
    };
    const harness = {
        model: createModel(),
        selection: collapsed('target', 14),
        history,
    };
    harness.controller = createCanvasClipboardController({
        inputBridge: { input },
        root,
        selectionController: {
            getSelection: () => harness.selection,
            setSelection: selection => {
                harness.selection = selection;
            },
        },
        getModel: () => harness.model,
        commit(change) {
            harness.model = change.model;
            harness.selection = change.selection;
            return { ok: true };
        },
        history,
        ...overrides,
    }).mount();
    return harness;
}

function createModel() {
    return {
        documentId: 'phase-11',
        version: 0,
        body: {
            blocks: [
                textBlock('source', [
                    run('Copy ', []),
                    run('this formatted', [{ type: 'bold', preserve: {} }]),
                    run(' clause', []),
                ], 10),
                textBlock('cut', [run('Cut this sentence', [])], 20),
                textBlock('target', [run('Paste target: ', [])], 30),
                textBlock('rich', [run('Rich paste target: ', [])], 40),
            ],
        },
        sections: [{ id: 'section-1', blocks: [] }],
    };
}

function textBlock(id, runs, order) {
    return {
        id,
        sectionId: 'section-1',
        type: 'paragraph',
        order,
        paragraphProperties: {},
        content: {
            type: 'paragraph',
            runs: runs.map((item, index) => ({ ...item, id: `${id}-run-${index + 1}` })),
        },
    };
}

function run(text, marks) {
    return {
        type: 'text',
        text,
        marks,
        field: null,
        token: null,
        noteReference: null,
        drawing: null,
        preserve: {},
    };
}

function drawingBlock(blockId, objectId, overrides = {}) {
    return {
        id: blockId,
        sectionId: 'section-1',
        type: 'paragraph',
        order: 50,
        paragraphProperties: {},
        content: {
            type: 'paragraph',
            runs: [{
                id: `${objectId}-run`,
                type: 'drawing',
                text: '',
                marks: [],
                drawing: {
                    objectId,
                    kind: 2,
                    altText: 'Clipboard drawing text box',
                    size: { width: 180, height: 72, lockAspectRatio: false },
                    naturalSize: { width: 180, height: 72, lockAspectRatio: false },
                    layout: {
                        kind: 1,
                        anchor: { blockId: 'target', offset: 0 },
                        position: { x: 48, y: 96 },
                        wrap: { mode: 'InFrontOfText' },
                        transform: { width: 180, height: 72, lockAspectRatio: false },
                        stacking: { zIndex: 4 },
                    },
                    shape: {
                        preset: 'roundRectangle',
                        fill: { color: '#dbeafe', opacity: 1 },
                        stroke: { color: '#2563eb', width: 2 },
                    },
                    textBody: {
                        paragraphs: [{ text: 'Clipboard drawing', alignment: 'center', style: { fontSize: 14 } }],
                    },
                    ...overrides,
                },
            }],
        },
        preserve: {},
    };
}

function groupDrawingBlock(blockId, objectId, childObjectIds) {
    return drawingBlock(blockId, objectId, {
        kind: 6,
        altText: 'Clipboard drawing group',
        group: {
            childObjectIds,
            origin: { x: 48, y: 96 },
            size: { x: 360, y: 120 },
        },
        shape: {
            preset: 'rectangle',
            fill: { type: 'none', color: '#ffffff' },
            stroke: { color: '#64748b', width: 1.5 },
        },
        textBody: null,
    });
}

function objectSelection(blockId, objectId, runId) {
    return {
        anchor: { blockId, offset: 0 },
        focus: { blockId, offset: 0 },
        object: { objectId, blockId, runId, role: 'drawingRun' },
    };
}

function drawingObjectIds(model) {
    return (model.body.blocks || [])
        .flatMap(block => block.content?.runs || [])
        .map(run => run?.drawing?.objectId || '')
        .filter(Boolean);
}

function drawingByObjectId(model, objectId) {
    const drawing = (model.body.blocks || [])
        .flatMap(block => block.content?.runs || [])
        .find(run => run?.drawing?.objectId === objectId)
        ?.drawing;
    assert.ok(drawing, `Expected drawing ${objectId} to exist.`);
    return drawing;
}

function collapsed(blockId, offset) {
    return { anchor: { blockId, offset }, focus: { blockId, offset } };
}

function range(anchorBlockId, anchorOffset, focusBlockId, focusOffset) {
    return {
        anchor: { blockId: anchorBlockId, offset: anchorOffset },
        focus: { blockId: focusBlockId, offset: focusOffset },
    };
}

class FakeDataTransfer {
    constructor() {
        this.values = new Map();
        this.items = [];
        this.files = [];
    }

    setData(type, value) {
        this.values.set(type, String(value));
    }

    getData(type) {
        return this.values.get(type) || '';
    }
}

class FakeElement {
    constructor() {
        this.listeners = new Map();
        this.attributes = new Map();
    }

    addEventListener(type, listener) {
        this.listeners.set(type, listener);
    }

    removeEventListener(type) {
        this.listeners.delete(type);
    }

    setAttribute(name, value) {
        this.attributes.set(name, String(value));
    }
}
