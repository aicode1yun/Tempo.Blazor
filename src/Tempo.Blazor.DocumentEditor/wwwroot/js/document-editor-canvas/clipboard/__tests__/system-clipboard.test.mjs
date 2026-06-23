import assert from 'node:assert/strict';
import test from 'node:test';
import { createCanvasClipboardController } from '../clipboard-controller.mjs';

// B11/B12 (UX fix 2026-06-11): programmatic clipboard for the context menu (no clipboard event). Copy/cut
// write the selected fragment to the system clipboard; paste reads it via the async Clipboard API.

test('copyToSystemClipboard writes html + plain text to the system clipboard', async () => {
    const fake = new FakeClipboard();
    const harness = createHarness(fake);
    harness.selection = range('p1', 0, 'p1', 5); // "Hello"

    const result = await harness.controller.copyToSystemClipboard();

    assert.equal(result.handled, true);
    assert.equal(result.operation, 'copy');
    assert.equal(fake.items.length, 1);
    assert.match(fake.items[0]['text/html'], /Hello/);
    assert.equal(fake.items[0]['text/plain'], 'Hello');
});

test('cutToSystemClipboard writes to the clipboard and deletes the selection in one transaction', async () => {
    const fake = new FakeClipboard();
    const harness = createHarness(fake);
    harness.selection = range('p1', 0, 'p1', 6); // "Hello " removed

    const result = await harness.controller.cutToSystemClipboard();

    assert.equal(result.handled, true);
    assert.equal(harness.model.body.blocks[0].content.runs.map(r => r.text).join(''), 'world');
    assert.equal(fake.items[0]['text/plain'], 'Hello ');
});

test('pasteFromSystemClipboard reads html from the system clipboard and inserts it', async () => {
    const fake = new FakeClipboard();
    fake.preload({ 'text/html': '<p>Pasted</p>', 'text/plain': 'Pasted' });
    const harness = createHarness(fake);
    harness.selection = collapsed('p1', 5); // caret after "Hello"

    const result = await harness.controller.pasteFromSystemClipboard();

    assert.equal(result.handled, true);
    assert.ok(harness.model.body.blocks.some(b => (b.content?.runs || []).some(r => r.text.includes('Pasted'))));
});

test('copyToSystemClipboard reports permission failure when the clipboard write is rejected', async () => {
    const fake = new FakeClipboard();
    fake.failWrites = true;
    const harness = createHarness(fake);
    harness.selection = range('p1', 0, 'p1', 5);

    const result = await harness.controller.copyToSystemClipboard();

    assert.equal(result.handled, false);
    assert.equal(result.reason, 'permission');
});

test('pasteFromSystemClipboard reports permission failure when the read is rejected', async () => {
    const fake = new FakeClipboard();
    fake.failReads = true;
    const harness = createHarness(fake);
    harness.selection = collapsed('p1', 5);

    const result = await harness.controller.pasteFromSystemClipboard();

    assert.equal(result.handled, false);
    assert.equal(result.reason, 'permission');
});

// ---- harness ----

function createHarness(fakeClipboard) {
    const fakeView = {
        navigator: { clipboard: fakeClipboard },
        ClipboardItem: FakeClipboardItem,
        Blob: FakeBlob,
    };
    const element = () => ({
        ownerDocument: { defaultView: fakeView },
        addEventListener() {},
        removeEventListener() {},
    });
    const history = { transactions: [], push(t) { this.transactions.push(t); return { canUndo: true, canRedo: false, undoDepth: this.transactions.length, redoDepth: 0 }; } };
    const harness = {
        model: createModel(),
        selection: collapsed('p1', 0),
        history,
    };
    harness.controller = createCanvasClipboardController({
        input: element(),
        root: element(),
        selectionController: {
            getSelection: () => harness.selection,
            setSelection: selection => { harness.selection = selection; },
        },
        getModel: () => harness.model,
        commit(change) { harness.model = change.model; harness.selection = change.selection; return { ok: true }; },
        history,
    }).mount();
    return harness;
}

function createModel() {
    return {
        documentId: 'b11-system-clipboard',
        version: 0,
        body: {
            blocks: [{
                id: 'p1',
                type: 'paragraph',
                order: 1,
                content: { type: 'paragraph', runs: [{ id: 'p1-run', type: 'text', text: 'Hello world', marks: [] }] },
            }],
        },
    };
}

function range(anchorBlock, anchorOffset, focusBlock, focusOffset) {
    return { anchor: { blockId: anchorBlock, offset: anchorOffset }, focus: { blockId: focusBlock, offset: focusOffset } };
}

function collapsed(blockId, offset) {
    return range(blockId, offset, blockId, offset);
}

class FakeClipboard {
    constructor() {
        this.items = [];
        this.preloaded = null;
        this.failWrites = false;
        this.failReads = false;
    }

    preload(typeMap) {
        this.preloaded = typeMap;
    }

    async write(items) {
        if (this.failWrites) throw new Error('NotAllowedError');
        for (const item of items) {
            this.items.push(await item.resolve());
        }
    }

    async writeText(text) {
        if (this.failWrites) throw new Error('NotAllowedError');
        this.items.push({ 'text/plain': text });
    }

    async read() {
        if (this.failReads) throw new Error('NotAllowedError');
        const map = this.preloaded || {};
        return [{
            types: Object.keys(map),
            async getType(type) { return new FakeBlob([map[type]]); },
        }];
    }

    async readText() {
        if (this.failReads) throw new Error('NotAllowedError');
        return this.preloaded?.['text/plain'] || '';
    }
}

class FakeClipboardItem {
    constructor(parts) {
        this.parts = parts;
    }

    async resolve() {
        const out = {};
        for (const [type, blob] of Object.entries(this.parts)) {
            out[type] = await blob.text();
        }
        return out;
    }
}

class FakeBlob {
    constructor(parts) {
        this.parts = parts || [];
    }

    async text() {
        return this.parts.map(p => String(p)).join('');
    }
}
