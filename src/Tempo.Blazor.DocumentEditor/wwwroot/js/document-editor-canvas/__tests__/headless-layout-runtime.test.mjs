import assert from 'node:assert/strict';
import test from 'node:test';
import {
    generateHeadlessLayoutSnapshot,
    generateHeadlessLayoutSnapshotJson,
} from '../headless-layout-runtime.mjs';

// Advance table with full ASCII coverage (600 units per glyph, space 250) so any sample text
// resolves deterministically; unitsPerEm 1000 keeps expected pixel math trivial.
function createFontTables() {
    const advances = {};
    for (let codePoint = 0x20; codePoint <= 0x7e; codePoint++) {
        advances[codePoint] = codePoint === 0x20 ? 250 : 600;
    }

    return {
        schemaVersion: 1,
        faces: [{
            family: 'Layout Sans',
            weight: 400,
            style: 'normal',
            unitsPerEm: 1000,
            ascent: 800,
            descent: 200,
            lineGap: 0,
            missingGlyphAdvance: 500,
            advances,
        }],
    };
}

// Serialized DocumentEditorDocument → CanvasDocumentModel wire shape (camelCase), as the C#
// service produces it.
function createModel() {
    return {
        schemaVersion: 1,
        documentId: 'headless-runtime-doc',
        theme: { bodyFontFamily: 'Layout Sans', bodyFontSize: 11, bodyLineHeight: 1.15, paragraphSpacingAfter: 8 },
        pageSettings: {
            width: 794, height: 1123,
            marginTop: 96, marginRight: 96, marginBottom: 96, marginLeft: 96,
            headerDistanceFromTop: 48, footerDistanceFromBottom: 48,
        },
        body: {
            blocks: [
                {
                    id: 'p1',
                    type: 'paragraph',
                    order: 1,
                    content: {
                        type: 'paragraph',
                        runs: [{ id: 'r1', type: 'text', text: 'Hello headless layout runtime.', marks: [] }],
                    },
                },
            ],
        },
    };
}

test('generates a schema v1 snapshot from a camelCase canvas model with advance tables', () => {
    const result = generateHeadlessLayoutSnapshot({ model: createModel(), fontTables: createFontTables() });

    assert.equal(result.schemaVersion, 1);
    assert.equal(result.snapshot.schemaVersion, 1);
    assert.ok(result.snapshot.pageCount >= 1);
    assert.equal(result.pageCount, result.snapshot.pageCount);
    assert.equal(result.snapshot.pages[0].width, 794);
    assert.equal(result.snapshot.pages[0].height, 1123);

    const texts = result.snapshot.pages[0].commands.filter(command => command.type === 'text');
    assert.ok(texts.length > 0, 'body text must reach the snapshot');
    assert.ok(texts.some(command => command.text === 'Hello'), 'word-segmented text runs must be present');

    assert.deepEqual(result.diagnostics.unknownFamilies, []);
    assert.deepEqual(result.diagnostics.missingGlyphs, []);
    assert.equal(result.diagnostics.fontTablesProvided, true);
});

test('the JSON entry point is a pure string-in/string-out seam (Jint contract)', () => {
    const json = generateHeadlessLayoutSnapshotJson(JSON.stringify({
        model: createModel(),
        fontTables: createFontTables(),
    }));

    assert.equal(typeof json, 'string');
    const result = JSON.parse(json);
    assert.equal(result.schemaVersion, 1);
    assert.ok(result.snapshot.pages.length >= 1);
});

test('layout is deterministic across calls', () => {
    const request = () => ({ model: createModel(), fontTables: createFontTables() });
    const first = generateHeadlessLayoutSnapshotJson(JSON.stringify(request()));
    const second = generateHeadlessLayoutSnapshotJson(JSON.stringify(request()));

    assert.equal(first, second);
});

test('unknown font family is reported in diagnostics (C# side fails closed on it)', () => {
    const model = createModel();
    model.theme.bodyFontFamily = 'Missing Face';

    const result = generateHeadlessLayoutSnapshot({ model, fontTables: createFontTables() });

    assert.ok(result.diagnostics.unknownFamilies.includes('Missing Face'));
});

test('without font tables the synthetic fallback engages and is flagged', () => {
    const result = generateHeadlessLayoutSnapshot({ model: createModel() });

    assert.equal(result.diagnostics.fontTablesProvided, false);
    assert.ok(result.snapshot.pages[0].commands.some(command => command.type === 'text'));
});

test('redaction-marked runs are destroyed in the snapshot', () => {
    const model = createModel();
    model.body.blocks[0].content.runs.push({
        id: 'r-secret',
        type: 'text',
        text: 'TAJNE',
        marks: [{ type: 'redaction' }],
    });

    const result = generateHeadlessLayoutSnapshot({ model, fontTables: createFontTables() });
    const serialized = JSON.stringify(result.snapshot);

    assert.equal(serialized.includes('TAJNE'), false, 'redacted characters must never reach the snapshot');
    assert.ok(serialized.includes('█'), 'redacted runs print as block characters');
});

test('empty or malformed model still produces a valid empty-page snapshot (normalization)', () => {
    const result = generateHeadlessLayoutSnapshot({ model: {}, fontTables: createFontTables() });

    assert.equal(result.schemaVersion, 1);
    assert.ok(result.snapshot.pageCount >= 1, 'the normalized empty document lays out a first page');
});
